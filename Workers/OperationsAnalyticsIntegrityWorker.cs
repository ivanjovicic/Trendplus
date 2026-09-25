using Infrastructure.Configuration;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Workers;

public sealed class OperationsAnalyticsIntegrityWorker : BackgroundService
{
    private const string WorkerName = "OperationsAnalyticsIntegrityWorker";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationsAnalyticsIntegrityWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly OperationsAnalyticsIntegrityOptions _options;

    public OperationsAnalyticsIntegrityWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OperationsAnalyticsIntegrityWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        IOptions<OperationsAnalyticsIntegrityOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{WorkerName} starting...", WorkerName);
        _healthService.ReportRunning(WorkerName, "Starting Operations analytics integrity probes.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _healthService.ReportStopped(WorkerName, "Cancelled during startup delay.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled || !_options.Enabled)
            {
                _healthService.ReportStopped(WorkerName, "Paused by worker runtime toggle or configuration.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.PauseCheckSeconds)), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var integrityService = scope.ServiceProvider.GetRequiredService<IOperationsAnalyticsIntegrityService>();
                var snapshot = await integrityService.RunBoundedProbeAsync(stoppingToken);
                _healthService.ReportRunning(
                    WorkerName,
                    $"Last probe {snapshot.Status} at {snapshot.CheckedAtUtc:O} ({snapshot.EvidenceId}).");
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "{WorkerName} probe iteration failed.", WorkerName);
                _healthService.ReportRunning(WorkerName, "Probe iteration failed; registry remains degraded/unverified.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(5, _options.PollIntervalMinutes)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _healthService.ReportStopped(WorkerName, "Stopped.");
    }
}
