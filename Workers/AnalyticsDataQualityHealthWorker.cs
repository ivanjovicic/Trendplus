using Infrastructure.Configuration;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Workers;

public sealed class AnalyticsDataQualityHealthWorker : BackgroundService
{
    private const string WorkerName = "AnalyticsDataQualityHealthWorker";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsDataQualityHealthWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly AnalyticsDataQualityHealthOptions _options;

    public AnalyticsDataQualityHealthWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsDataQualityHealthWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        IOptions<AnalyticsDataQualityHealthOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var paused = false;
        _logger.LogInformation("{WorkerName} starting...", WorkerName);
        _healthService.ReportRunning(WorkerName, "Starting analytics data-quality health checks.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _healthService.ReportStopped(WorkerName, "Cancelled during startup delay");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled || !_options.Enabled)
            {
                if (!paused)
                {
                    var reason = !_controlService.IsEnabled
                        ? "Pauziran - workers switch je iskljucen."
                        : "Pauziran - AnalyticsDataQualityHealth je disabled u konfiguraciji.";
                    _logger.LogInformation("{WorkerName} paused. Reason: {Reason}", WorkerName, reason);
                    _healthService.ReportStopped(WorkerName, reason);
                    paused = true;
                }

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

            if (paused)
            {
                _healthService.ReportRunning(WorkerName, "Nastavljen rad nakon ukljucivanja workers switch-a.");
                paused = false;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<AnalyticsDataQualityHealthService>();
                var snapshot = await service.CaptureAsync(_options.LookbackDays, stoppingToken);

                var summary =
                    $"Lookback={snapshot.LookbackDays}d | OrphanArticles={snapshot.OrphanArticleCount} | " +
                    $"MissingCostRevenueShare={snapshot.MissingCostRevenueSharePct:0.##}% | " +
                    $"UnknownSupplierRevenueShare={snapshot.UnknownSupplierRevenueSharePct:0.##}%";

                var hasWarnings =
                    snapshot.OrphanArticleCount >= _options.WarningOrphanArticleCount
                    || snapshot.MissingCostRevenueSharePct >= _options.WarningMissingCostRevenueSharePct
                    || snapshot.UnknownSupplierRevenueSharePct >= _options.WarningUnknownSupplierRevenueSharePct;

                if (hasWarnings)
                {
                    _logger.LogWarning(
                        "Analytics data-quality health warning. {Summary} Window={WindowFrom:yyyy-MM-dd}..{WindowTo:yyyy-MM-dd}",
                        summary,
                        snapshot.WindowFromUtc,
                        snapshot.WindowToUtc);
                }
                else
                {
                    _logger.LogInformation(
                        "Analytics data-quality health OK. {Summary} Window={WindowFrom:yyyy-MM-dd}..{WindowTo:yyyy-MM-dd}",
                        summary,
                        snapshot.WindowFromUtc,
                        snapshot.WindowToUtc);
                }

                _healthService.ReportHealthy(WorkerName, summary);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analytics data-quality health worker failed.");
                _healthService.ReportError(WorkerName, ex);
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

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
        _logger.LogInformation("{WorkerName} stopped.", WorkerName);
    }
}
