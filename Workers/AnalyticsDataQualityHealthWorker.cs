using Infrastructure.Configuration;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Workers;

public sealed class AnalyticsDataQualityHealthWorker : BackgroundService
{
    private const string WorkerName = "AnalyticsDataQualityHealthWorker";
    private const string RefreshJobKey = "data_quality_snapshot";
    private const string RefreshJobName = "Data quality snapshot";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsDataQualityHealthWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly WorkerRuntimePolicyService _runtimePolicyService;
    private readonly AnalyticsDataQualityHealthOptions _options;

    public AnalyticsDataQualityHealthWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsDataQualityHealthWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        WorkerRuntimePolicyService runtimePolicyService,
        IOptions<AnalyticsDataQualityHealthOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _runtimePolicyService = runtimePolicyService;
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
                        ? "Pauziran - workers switch je isključen."
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

            var policy = await _runtimePolicyService.GetPolicyAsync(WorkerName, stoppingToken);
            var manualRunRequested = false;
            if (!policy.CanRunNow)
            {
                if (!paused)
                {
                    var reason = policy.PauseReason ?? "Pauziran - worker policy disabled execution.";
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

            if (policy.ManualRunRequested && !string.IsNullOrWhiteSpace(policy.ManualRunToken))
            {
                manualRunRequested = await _runtimePolicyService.TryConsumeManualRunRequestAsync(
                    WorkerName,
                    policy.ManualRunToken,
                    stoppingToken);

                if (!manualRunRequested)
                {
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
            }

            if (paused)
            {
                _healthService.ReportRunning(WorkerName, "Nastavljen rad nakon uključivanja workers switch-a.");
                paused = false;
            }

            var correlationId = System.Diagnostics.Activity.Current?.Id;
            var runId = await UseRefreshRunRecorderAsync(recorder => recorder.StartRunAsync(
                jobKey: RefreshJobKey,
                jobName: RefreshJobName,
                triggeredBy: manualRunRequested ? "manual" : "system",
                processMode: "worker",
                workerName: WorkerName,
                correlationId: correlationId,
                stoppingToken));

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<AnalyticsDataQualityHealthService>();
                var historyService = scope.ServiceProvider.GetRequiredService<AnalyticsDataQualityHistoryService>();
                var snapshot = await service.CaptureAsync(_options.LookbackDays, null, stoppingToken);
                await historyService.SaveSnapshotAsync(snapshot, null, stoppingToken);

                try
                {
                    var cacheAdmin = scope.ServiceProvider.GetService<AnalyticsCacheAdminService>();
                    if (cacheAdmin is not null)
                    {
                        var invalidatedFamilies = new[]
                        {
                            AnalyticsCachePolicy.DashboardFamily,
                            AnalyticsCachePolicy.DataQualityFamily,
                            AnalyticsCachePolicy.ReportsFamily
                        };
                        var cacheState = await cacheAdmin.ClearFamiliesAsync(
                            invalidatedFamilies,
                            stoppingToken);
                        _logger.LogInformation(
                            "Data quality refresh invalidated cache families {Families}. ReportCacheVersion={ReportCacheVersion} LastReportClearAtUtc={LastReportCacheClearAtUtc:O}",
                            invalidatedFamilies,
                            cacheState.ReportCacheVersion,
                            cacheState.LastReportCacheClearAtUtc);
                    }
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Data quality refresh cache invalidation failed.");
                }

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
                if (hasWarnings)
                {
                    await UseRefreshRunRecorderAsync(recorder => recorder.MarkPartialAsync(
                        runId,
                        ["analytics_data_quality_history"],
                        [],
                        $"Data quality warning: {summary}",
                        correlationId,
                        stoppingToken));
                }
                else
                {
                    await UseRefreshRunRecorderAsync(recorder => recorder.MarkSucceededAsync(
                        runId,
                        ["analytics_data_quality_history"],
                        correlationId,
                        stoppingToken));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await UseRefreshRunRecorderAsync(recorder => recorder.MarkFailedAsync(
                    runId,
                    "cancelled",
                    "Analytics data-quality run was cancelled.",
                    ["analytics_data_quality_history"],
                    correlationId,
                    CancellationToken.None));
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analytics data-quality health worker failed.");
                _healthService.ReportError(WorkerName, ex);
                await UseRefreshRunRecorderAsync(recorder => recorder.MarkFailedAsync(
                    runId,
                    "data_quality_worker_failed",
                    ex.Message,
                    ["analytics_data_quality_history"],
                    correlationId,
                    stoppingToken));
            }

            try
            {
                var delay = manualRunRequested
                    ? TimeSpan.FromSeconds(Math.Max(5, _options.PauseCheckSeconds))
                    : TimeSpan.FromMinutes(Math.Max(5, _options.PollIntervalMinutes));
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
        _logger.LogInformation("{WorkerName} stopped.", WorkerName);
    }

    private async Task<T> UseRefreshRunRecorderAsync<T>(Func<AnalyticsRefreshRunRecorder, Task<T>> action)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var recorder = scope.ServiceProvider.GetRequiredService<AnalyticsRefreshRunRecorder>();
        return await action(recorder);
    }

    private async Task UseRefreshRunRecorderAsync(Func<AnalyticsRefreshRunRecorder, Task> action)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var recorder = scope.ServiceProvider.GetRequiredService<AnalyticsRefreshRunRecorder>();
        await action(recorder);
    }
}


