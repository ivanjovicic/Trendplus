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
    private readonly DocumentExportOptions _options;

    public DocumentGenerationWorker(
        IServiceProvider serviceProvider,
        ILogger<DocumentGenerationWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        IOptions<DocumentExportOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _healthService.ReportRunning(WorkerName, "Starting up...");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled)
            {
                _healthService.ReportStopped(WorkerName, "Paused - workers switch disabled.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
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
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
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
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
    }
}
