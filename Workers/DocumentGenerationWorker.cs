using Application.Documents.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Workers;

public sealed class DocumentGenerationWorker : BackgroundService
{
    private const string WorkerName = "DocumentGenerationWorker";
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentGenerationWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly WorkerRuntimePolicyService _runtimePolicyService;
    private readonly DocumentExportOptions _options;

    public DocumentGenerationWorker(
        IServiceProvider serviceProvider,
        ILogger<DocumentGenerationWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        WorkerRuntimePolicyService runtimePolicyService,
        IOptions<DocumentExportOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _runtimePolicyService = runtimePolicyService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _healthService.ReportRunning(WorkerName, "Starting up...");
        var pauseCheckInterval = TimeSpan.FromSeconds(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled)
            {
                _healthService.ReportStopped(WorkerName, "Paused - workers switch disabled.");
                await Task.Delay(pauseCheckInterval, stoppingToken);
                continue;
            }

            var policy = await _runtimePolicyService.GetPolicyAsync(WorkerName, stoppingToken);
            var manualRunRequested = false;
            if (!policy.CanRunNow)
            {
                _healthService.ReportStopped(WorkerName, policy.PauseReason ?? "Paused - worker policy disabled execution.");
                await Task.Delay(pauseCheckInterval, stoppingToken);
                continue;
            }

            if (policy.ManualRunRequested && !string.IsNullOrWhiteSpace(policy.ManualRunToken))
            {
                manualRunRequested = await _runtimePolicyService.TryConsumeManualRunRequestAsync(
                    WorkerName,
                    policy.ManualRunToken,
                    stoppingToken);

                if (!manualRunRequested)
                {
                    await Task.Delay(pauseCheckInterval, stoppingToken);
                    continue;
                }
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queueStore = scope.ServiceProvider.GetRequiredService<IDocumentQueueStore>();
                var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
                var claimedIds = await queueStore.ClaimNextQueuedAsync(_options.WorkerBatchSize, stoppingToken);

                if (claimedIds.Count == 0)
                {
                    _healthService.ReportHealthy(WorkerName, "No queued documents.");
                    var idleDelay = manualRunRequested ? pauseCheckInterval : TimeSpan.FromSeconds(10);
                    await Task.Delay(idleDelay, stoppingToken);
                    continue;
                }

                foreach (var documentId in claimedIds)
                {
                    await documentService.ProcessQueuedDocumentAsync(documentId, stoppingToken);
                }

                _healthService.ReportHealthy(WorkerName, $"Processed {claimedIds.Count} queued documents.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document generation worker iteration failed");
                _healthService.ReportError(WorkerName, ex);
                var retryDelay = manualRunRequested ? pauseCheckInterval : TimeSpan.FromSeconds(15);
                await Task.Delay(retryDelay, stoppingToken);
            }
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
    }
}
