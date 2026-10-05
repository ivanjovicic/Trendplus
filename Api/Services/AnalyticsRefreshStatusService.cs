using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Config;
using Domain.Model.Analytics;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trendplus2.Dtos;

namespace Api.Services;

public sealed class AnalyticsRefreshStatusService
{
    private const string NightlyWorkerName = "NightlyAnalyticsRefreshWorker";
    private const string DataQualityWorkerName = "AnalyticsDataQualityHealthWorker";
    private const string NightlyHistoryJobKey = "nightly_analytics_refresh";
    private const string DataQualityHistoryJobKey = "data_quality_snapshot";

    private static readonly Regex DurationRegex = new(
        @"(?:completed in|Duration:\s*)(?<seconds>\d+(?:\.\d+)?)s",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly TimeSpan ActiveWorkerHeartbeatThreshold = TimeSpan.FromMinutes(15);
    private const int DefaultStuckRunningThresholdMinutes = 120;
    private const string StuckRunningStatusReason = "Refresh je započet, ali nije završen u očekivanom vremenu.";
    private const string PartialRefreshStatusReason = "Refresh je završen delimično; podaci nisu kompletni.";

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AnalyticsDbContext _analyticsDbContext;
    private readonly TrendplusDbContext? _trendDb;
    private readonly WorkerHealthService _workerHealthService;
    private readonly WorkerRuntimeControlService _workerRuntimeControlService;
    private readonly AnalyticsCacheAdminService _cacheAdmin;
    private readonly ILogger<AnalyticsRefreshStatusService> _logger;
    private readonly AnalyticsFreshnessOptions _freshnessOptions;

    public AnalyticsRefreshStatusService(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        AnalyticsDbContext analyticsDbContext,
        WorkerHealthService workerHealthService,
        WorkerRuntimeControlService workerRuntimeControlService,
        AnalyticsCacheAdminService cacheAdmin,
        ILogger<AnalyticsRefreshStatusService> logger,
        TrendplusDbContext? trendDb = null,
        IOptions<AnalyticsFreshnessOptions>? freshnessOptions = null)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _analyticsDbContext = analyticsDbContext;
        _trendDb = trendDb;
        _workerHealthService = workerHealthService;
        _workerRuntimeControlService = workerRuntimeControlService;
        _cacheAdmin = cacheAdmin;
        _logger = logger;
        _freshnessOptions = freshnessOptions?.Value ?? new AnalyticsFreshnessOptions();
    }

    public AnalyticsRefreshStatusDto GetStatus()
        => GetStatusAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task<AnalyticsRefreshStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var processType = WorkerRuntimeConfig.ResolveProcessType(_configuration, out _);
        var configuredWorkersEnabled = _configuration.GetValue<bool?>("Workers:Enabled");
        var workersEnabled = WorkerRuntimeConfig.ResolveWorkersEnabled(
            configuredWorkersEnabled,
            processType,
            _hostEnvironment.IsDevelopment());
        var workersEnabledInRuntime = workersEnabled && _workerRuntimeControlService.IsEnabled;
        var processMode = ResolveProcessMode(processType);
        var stuckRunningThreshold = ResolveStuckRunningThreshold();

        var runs = await LoadRecentRunsAsync(ct);
        var cacheState = await _cacheAdmin.GetStateAsync(ct);
        var (cacheMode, isDistributed) = _cacheAdmin.ResolveCacheMode();
        var cacheWarning = ResolveCacheWarning(cacheMode, isDistributed);

        var jobs = new List<AnalyticsRefreshJobStatusDto>
        {
            BuildJobStatus(
                key: "sales_facts_refresh",
                displayName: "Sales facts refresh",
                workerName: NightlyWorkerName,
                historyFallbackJobKey: NightlyHistoryJobKey,
                refreshedObjectNames: ["sales_facts_mv"],
                freshHours: _freshnessOptions.WarningAfterHours,
                staleHours: _freshnessOptions.CriticalAfterHours,
                stuckRunningThreshold,
                processType,
                workersEnabledInRuntime,
                nowUtc,
                runs),
            BuildJobStatus(
                key: "product_dim_refresh",
                displayName: "Product dim refresh",
                workerName: NightlyWorkerName,
                historyFallbackJobKey: NightlyHistoryJobKey,
                refreshedObjectNames: ["product_dim_mv"],
                freshHours: _freshnessOptions.WarningAfterHours,
                staleHours: _freshnessOptions.CriticalAfterHours,
                stuckRunningThreshold,
                processType,
                workersEnabledInRuntime,
                nowUtc,
                runs),
            BuildJobStatus(
                key: "supplier_decision_mvs",
                displayName: "Supplier decision MVs",
                workerName: NightlyWorkerName,
                historyFallbackJobKey: NightlyHistoryJobKey,
                refreshedObjectNames:
                [
                    "mv_supplier_decision_score_cache_90d",
                    "mv_supplier_decision_score_cache_180d",
                    "mv_supplier_decision_score_cache"
                ],
                freshHours: _freshnessOptions.WarningAfterHours,
                staleHours: _freshnessOptions.CriticalAfterHours,
                stuckRunningThreshold,
                processType,
                workersEnabledInRuntime,
                nowUtc,
                runs),
            BuildJobStatus(
                key: "product_decision_snapshot",
                displayName: "Product decision snapshot",
                workerName: NightlyWorkerName,
                historyFallbackJobKey: NightlyHistoryJobKey,
                refreshedObjectNames: ["mv_product_decision_snapshot"],
                freshHours: _freshnessOptions.WarningAfterHours,
                staleHours: _freshnessOptions.CriticalAfterHours,
                stuckRunningThreshold,
                processType,
                workersEnabledInRuntime,
                nowUtc,
                runs),
            BuildJobStatus(
                key: "inventory_recommendations",
                displayName: "Inventory recommendations",
                workerName: NightlyWorkerName,
                historyFallbackJobKey: NightlyHistoryJobKey,
                refreshedObjectNames: ["mv_inventory_recommendations"],
                freshHours: _freshnessOptions.WarningAfterHours,
                staleHours: _freshnessOptions.CriticalAfterHours,
                stuckRunningThreshold,
                processType,
                workersEnabledInRuntime,
                nowUtc,
                runs),
            BuildJobStatus(
                key: DataQualityHistoryJobKey,
                displayName: "Data quality snapshot",
                workerName: DataQualityWorkerName,
                historyFallbackJobKey: null,
                refreshedObjectNames: ["analytics_data_quality_history"],
                freshHours: _freshnessOptions.WarningAfterHours,
                staleHours: _freshnessOptions.CriticalAfterHours,
                stuckRunningThreshold,
                processType,
                workersEnabledInRuntime,
                nowUtc,
                runs)
        };

        var lastSuccess = jobs
            .Where(j => j.LastSuccessfulRefreshAtUtc.HasValue)
            .Select(j => j.LastSuccessfulRefreshAtUtc!.Value)
            .DefaultIfEmpty()
            .Max();
        var lastAttempt = jobs
            .Where(j => j.LastAttemptAtUtc.HasValue)
            .Select(j => j.LastAttemptAtUtc!.Value)
            .DefaultIfEmpty()
            .Max();
        var lastFailure = jobs
            .Where(j => j.LastFailureAtUtc.HasValue)
            .Select(j => j.LastFailureAtUtc!.Value)
            .DefaultIfEmpty()
            .Max();

        var hasLastSuccess = lastSuccess != default;
        var hasLastAttempt = lastAttempt != default;
        var hasLastFailure = lastFailure != default;
        var hasStuckRunningJob = jobs.Any(job =>
            job.IsRunning &&
            string.Equals(job.DataFreshnessStatus, "critical", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(job.StatusReason, StuckRunningStatusReason, StringComparison.Ordinal));

        var measuredDurations = jobs
            .Select(job => NormalizeDurationSeconds(job.DurationSeconds))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();
        var durableImport = await LoadDurableImportEvidenceAsync(ct);

        var status = new AnalyticsRefreshStatusDto
        {
            ProcessMode = processMode,
            WorkersEnabled = workersEnabled,
            LastSuccessfulRefreshAtUtc = hasLastSuccess ? lastSuccess : null,
            LastSuccessfulImportAtUtc = durableImport.LastSuccessfulImportAtUtc,
            ObservedSalesPeriodFromUtc = durableImport.ObservedSalesPeriodFromUtc,
            ObservedSalesPeriodToUtc = durableImport.ObservedSalesPeriodToUtc,
            LastAttemptAtUtc = hasLastAttempt ? lastAttempt : null,
            LastFailureAtUtc = hasLastFailure ? lastFailure : null,
            IsRunning = jobs.Any(j => j.IsRunning),
            LastErrorMessage = jobs
                .OrderByDescending(j => j.LastFailureAtUtc ?? DateTime.MinValue)
                .Select(j => j.LastErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message)),
            CurrentStep = jobs
                .Select(j => j.CurrentStep)
                .FirstOrDefault(step => !string.IsNullOrWhiteSpace(step)),
            RefreshedObjects = jobs
                .SelectMany(j => j.RefreshedObjects)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            FailedObjects = jobs
                .SelectMany(j => j.FailedObjects)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            DurationSeconds = measuredDurations.Count > 0 ? measuredDurations.Max() : null,
            DataFreshnessStatus = hasStuckRunningJob
                ? "critical"
                : jobs.Any(job => string.Equals(job.StatusReason, PartialRefreshStatusReason, StringComparison.Ordinal))
                    ? "critical"
                : _trendDb is null
                    ? AnalyticsFreshnessPolicy.Resolve(
                        hasLastSuccess ? lastSuccess : null,
                        hasLastFailure ? lastFailure : null,
                        nowUtc,
                        _freshnessOptions)
                    : AnalyticsFreshnessPolicy.Resolve(
                        durableImport.LastSuccessfulImportAtUtc,
                        durableImport.LastFailureAtUtc,
                        nowUtc,
                        _freshnessOptions),
            CacheMode = cacheMode,
            IsDistributed = isDistributed,
            LastAnalyticsCacheClearAtUtc = cacheState.LastAnalyticsCacheClearAtUtc,
            LastReportCacheClearAtUtc = cacheState.LastReportCacheClearAtUtc,
            CacheWarning = cacheWarning,
            Jobs = jobs,
            RecentRuns = runs
                .OrderByDescending(r => r.StartedAtUtc)
                .Take(10)
                .Select(MapRecentRun)
                .ToList()
        };

        if (processMode == "web" && workersEnabled && !HasAnyAnalyticsWorkerActive(nowUtc))
        {
            status.WorkerWarning =
                "Worker nije aktivan u ovom procesu. Automatsko osvezavanje nije aktivno; potreban je poseban worker deploy.";
        }

        return status;
    }

    private AnalyticsRefreshJobStatusDto BuildJobStatus(
        string key,
        string displayName,
        string workerName,
        string? historyFallbackJobKey,
        IReadOnlyList<string> refreshedObjectNames,
        int freshHours,
        int staleHours,
        TimeSpan stuckRunningThreshold,
        ProcessType processType,
        bool workersEnabledInRuntime,
        DateTime nowUtc,
        IReadOnlyList<AnalyticsRefreshRun> runs)
    {
        var workerDefinition = WorkerRegistryCatalog.Find(workerName);
        var canRegisterInProcess = workerDefinition is not null &&
            WorkerRuntimeConfig.IsRegisteredInCurrentProcess(
                workerDefinition,
                processType,
                registerAccessImportWorkerInWebProcess: false);
        var workerIsExpectedToRun = canRegisterInProcess && workersEnabledInRuntime;
        var latestJobRun = FindLatestRun(runs, key, historyFallbackJobKey, workerName);

        if (latestJobRun is not null)
        {
            var successRun = FindLatestSuccessfulRun(runs, key, historyFallbackJobKey, workerName);
            var failureRun = FindLatestFailedRun(runs, key, historyFallbackJobKey, workerName);
            var lastSuccess = successRun?.FinishedAtUtc ?? successRun?.StartedAtUtc;
            var lastFailure = failureRun?.FinishedAtUtc ?? failureRun?.StartedAtUtc;
            var lastAttempt = latestJobRun.StartedAtUtc;
            var isRunning = string.Equals(latestJobRun.Status, "running", StringComparison.OrdinalIgnoreCase);
            var isPartial = string.Equals(latestJobRun.Status, "partial", StringComparison.OrdinalIgnoreCase);
            var isStuckRunning = isRunning && latestJobRun.StartedAtUtc <= nowUtc.Subtract(stuckRunningThreshold);

            var refreshedObjects = ParseObjects(latestJobRun.RefreshedObjectsJson);
            var failedObjects = ParseObjects(latestJobRun.FailedObjectsJson);

            if (refreshedObjects.Count == 0 &&
                !isRunning &&
                string.Equals(latestJobRun.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                refreshedObjects = refreshedObjectNames.ToList();
            }

            if (failedObjects.Count == 0 &&
                string.Equals(latestJobRun.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                failedObjects = refreshedObjectNames.ToList();
            }

            var freshnessStatus = ResolveFreshnessStatus(
                lastSuccess,
                lastFailure,
                ResolveWorkerStatusFromRun(latestJobRun.Status),
                nowUtc,
                freshHours,
                staleHours);
            if (isStuckRunning)
            {
                freshnessStatus = "critical";
            }

            return new AnalyticsRefreshJobStatusDto
            {
                Key = key,
                DisplayName = displayName,
                WorkerName = workerName,
                LastSuccessfulRefreshAtUtc = lastSuccess,
                LastAttemptAtUtc = lastAttempt,
                LastFailureAtUtc = lastFailure,
                IsRunning = isRunning,
                LastErrorMessage = latestJobRun.ErrorMessage,
                CurrentStep = isStuckRunning
                    ? StuckRunningStatusReason
                    : isRunning ? "Refresh u toku..." : null,
                RefreshedObjects = refreshedObjects,
                FailedObjects = failedObjects,
                DurationSeconds = NormalizeDurationSeconds(latestJobRun.DurationSeconds),
                DataFreshnessStatus = freshnessStatus,
                StatusReason = isStuckRunning
                    ? StuckRunningStatusReason
                    : isPartial ? PartialRefreshStatusReason : null
            };
        }

        var workerHealth = _workerHealthService.GetStatus(workerName);
        var isRunningFromFallback = workerHealth?.Status == WorkerStatusType.Running;
        string? reason;
        if (!workerIsExpectedToRun)
        {
            reason = processType == ProcessType.Web
                ? "Worker nije registrovan u web procesu."
                : "Worker je iskljucen runtime kontrolom.";
        }
        else
        {
            reason = "Nema durable istorije osvezavanja za ovaj posao.";
        }

        return new AnalyticsRefreshJobStatusDto
        {
            Key = key,
            DisplayName = displayName,
            WorkerName = workerName,
            LastSuccessfulRefreshAtUtc = null,
            LastAttemptAtUtc = workerHealth?.LastHeartbeat,
            LastFailureAtUtc = workerHealth?.LastErrorTime,
            IsRunning = isRunningFromFallback,
            LastErrorMessage = workerHealth?.LastError,
            CurrentStep = workerHealth?.Message,
            RefreshedObjects = [],
            FailedObjects = [],
            DurationSeconds = ResolveDurationSeconds(workerHealth?.Message, workerHealth?.LastError),
            DataFreshnessStatus = "unknown",
            StatusReason = reason
        };
    }

    private string? ResolveCacheWarning(string cacheMode, bool isDistributed)
    {
        if (_hostEnvironment.IsProduction() &&
            string.Equals(cacheMode, "in-memory", StringComparison.OrdinalIgnoreCase))
        {
            return "Analytics cache je in-memory. U multi-instance okruženju podaci mogu biti nekonzistentni između instanci.";
        }

        if (!isDistributed)
        {
            return _cacheAdmin.GetTopologyWarning();
        }

        return null;
    }

    private async Task<List<AnalyticsRefreshRun>> LoadRecentRunsAsync(CancellationToken ct)
    {
        try
        {
            return await _analyticsDbContext.AnalyticsRefreshRuns
                .AsNoTracking()
                .Where(run =>
                    run.WorkerName == NightlyWorkerName ||
                    run.WorkerName == DataQualityWorkerName ||
                    run.JobKey == NightlyHistoryJobKey ||
                    run.JobKey == DataQualityHistoryJobKey)
                .OrderByDescending(run => run.StartedAtUtc)
                .Take(300)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load durable analytics refresh history. Falling back to worker health.");
            return [];
        }
    }

    private async Task<DurableImportEvidence> LoadDurableImportEvidenceAsync(CancellationToken ct)
    {
        if (_trendDb is null)
        {
            return DurableImportEvidence.Empty;
        }

        try
        {
            var accessBatches = _trendDb.DataImportBatches
                .AsNoTracking()
                .Where(batch => batch.SourceSystem.ToLower() == "access" && batch.IncludeAnalytics);

            var lastSuccessfulImportAtUtc = await accessBatches
                .Where(batch =>
                    batch.Status.ToLower() == "completed" &&
                    batch.CompletedAtUtc.HasValue &&
                    batch.TotalErrors == 0 &&
                    batch.RowsRejected == 0)
                .OrderByDescending(batch => batch.CompletedAtUtc)
                .ThenByDescending(batch => batch.Id)
                .Select(batch => batch.CompletedAtUtc)
                .FirstOrDefaultAsync(ct);

            var lastFailureAtUtc = await accessBatches
                .Where(batch =>
                    batch.Status.ToLower() == "failed" ||
                    batch.Status.ToLower() == "interrupted" ||
                    (batch.Status.ToLower() == "completed" &&
                        (batch.TotalErrors > 0 || batch.RowsRejected > 0)))
                .OrderByDescending(batch => batch.CompletedAtUtc ?? batch.StartedAtUtc)
                .ThenByDescending(batch => batch.Id)
                .Select(batch => (DateTime?)(batch.CompletedAtUtc ?? batch.StartedAtUtc))
                .FirstOrDefaultAsync(ct);

            var accessSalesWithLines = _trendDb.ProdajaZaglavlja
                .AsNoTracking()
                .Where(header =>
                    header.DataOrigin.ToLower() == "access" &&
                    _trendDb.ProdajaStavke.Any(line => line.IdProdaja == header.Id));

            var observedSalesPeriodFromUtc = await accessSalesWithLines
                .Select(header => (DateTime?)header.DatumProdaje)
                .MinAsync(ct);
            var observedSalesPeriodToUtc = await accessSalesWithLines
                .Select(header => (DateTime?)header.DatumProdaje)
                .MaxAsync(ct);

            return new DurableImportEvidence(
                lastSuccessfulImportAtUtc,
                lastFailureAtUtc,
                observedSalesPeriodFromUtc,
                observedSalesPeriodToUtc);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load durable Access import evidence for analytics freshness.");
            return DurableImportEvidence.Empty;
        }
    }

    private static AnalyticsRefreshRun? FindLatestRun(
        IReadOnlyList<AnalyticsRefreshRun> runs,
        string jobKey,
        string? historyFallbackJobKey,
        string workerName)
    {
        return runs
            .Where(run =>
                string.Equals(run.WorkerName, workerName, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(run.JobKey, jobKey, StringComparison.OrdinalIgnoreCase)
                 || (!string.IsNullOrWhiteSpace(historyFallbackJobKey)
                     && string.Equals(run.JobKey, historyFallbackJobKey, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstOrDefault();
    }

    private static AnalyticsRefreshRun? FindLatestSuccessfulRun(
        IReadOnlyList<AnalyticsRefreshRun> runs,
        string jobKey,
        string? historyFallbackJobKey,
        string workerName)
    {
        return runs
            .Where(run =>
                string.Equals(run.WorkerName, workerName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(run.Status, "succeeded", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(run.JobKey, jobKey, StringComparison.OrdinalIgnoreCase)
                 || (!string.IsNullOrWhiteSpace(historyFallbackJobKey)
                     && string.Equals(run.JobKey, historyFallbackJobKey, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstOrDefault();
    }

    private static AnalyticsRefreshRun? FindLatestFailedRun(
        IReadOnlyList<AnalyticsRefreshRun> runs,
        string jobKey,
        string? historyFallbackJobKey,
        string workerName)
    {
        return runs
            .Where(run =>
                string.Equals(run.WorkerName, workerName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(run.Status, "failed", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(run.JobKey, jobKey, StringComparison.OrdinalIgnoreCase)
                 || (!string.IsNullOrWhiteSpace(historyFallbackJobKey)
                     && string.Equals(run.JobKey, historyFallbackJobKey, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstOrDefault();
    }

    private static List<string> ParseObjects(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return parsed
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static AnalyticsRefreshRunDto MapRecentRun(AnalyticsRefreshRun run)
    {
        return new AnalyticsRefreshRunDto
        {
            Id = run.Id,
            JobKey = run.JobKey,
            JobName = run.JobName,
            Status = run.Status,
            StartedAtUtc = run.StartedAtUtc,
            FinishedAtUtc = run.FinishedAtUtc,
            DurationSeconds = NormalizeDurationSeconds(run.DurationSeconds),
            RefreshedObjects = ParseObjects(run.RefreshedObjectsJson),
            FailedObjects = ParseObjects(run.FailedObjectsJson),
            ErrorCode = run.ErrorCode,
            ErrorMessage = run.ErrorMessage,
            CorrelationId = run.CorrelationId,
            TriggeredBy = run.TriggeredBy,
            ProcessMode = run.ProcessMode,
            WorkerName = run.WorkerName,
            CreatedAtUtc = run.CreatedAtUtc
        };
    }

    private static WorkerStatusType? ResolveWorkerStatusFromRun(string? status)
    {
        if (string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
        {
            return WorkerStatusType.Running;
        }

        if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return WorkerStatusType.Error;
        }

        if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return WorkerStatusType.Healthy;
        }

        if (string.Equals(status, "partial", StringComparison.OrdinalIgnoreCase))
        {
            return WorkerStatusType.Error;
        }

        return null;
    }

    private static double? ResolveDurationSeconds(string? message, string? errorMessage)
    {
        var source = string.IsNullOrWhiteSpace(message) ? errorMessage : message;
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var match = DurationRegex.Match(source);
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(
            match.Groups["seconds"].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var seconds)
            ? NormalizeDurationSeconds(seconds)
            : null;
    }

    private static double? NormalizeDurationSeconds(double? value)
    {
        if (!value.HasValue || !double.IsFinite(value.Value) || value.Value < 0)
        {
            return null;
        }

        return value.Value;
    }

    private static string ResolveFreshnessStatus(
        DateTime? lastSuccess,
        DateTime? lastFailure,
        WorkerStatusType? status,
        DateTime nowUtc,
        int freshHours,
        int staleHours)
    {
        if (status == WorkerStatusType.Error)
        {
            return "critical";
        }

        if (lastSuccess is null)
        {
            return "unknown";
        }

        if (lastFailure.HasValue && lastFailure.Value > lastSuccess.Value)
        {
            return "critical";
        }

        var age = nowUtc - lastSuccess.Value;
        if (age < TimeSpan.FromHours(Math.Max(1, freshHours)))
        {
            return "fresh";
        }

        if (age < TimeSpan.FromHours(Math.Max(freshHours + 1, staleHours)))
        {
            return "stale";
        }

        return "critical";
    }

    private bool HasAnyAnalyticsWorkerActive(DateTime nowUtc)
    {
        var workerNames = new[] { NightlyWorkerName, DataQualityWorkerName };
        foreach (var workerName in workerNames)
        {
            var status = _workerHealthService.GetStatus(workerName);
            if (status is null)
            {
                continue;
            }

            var isActiveStatus = status.Status is WorkerStatusType.Running or WorkerStatusType.Healthy;
            var hasRecentHeartbeat = status.LastHeartbeat >= nowUtc.Subtract(ActiveWorkerHeartbeatThreshold);

            if (isActiveStatus && hasRecentHeartbeat)
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveProcessMode(ProcessType processType)
    {
        return processType switch
        {
            ProcessType.Web => "web",
            ProcessType.Worker => "worker",
            _ => "unknown"
        };
    }

    private TimeSpan ResolveStuckRunningThreshold()
    {
        var thresholdMinutes = _configuration.GetValue<int?>("Analytics:RefreshStatus:StuckRunningThresholdMinutes")
            ?? DefaultStuckRunningThresholdMinutes;
        return TimeSpan.FromMinutes(Math.Max(5, thresholdMinutes));
    }

    private sealed record DurableImportEvidence(
        DateTime? LastSuccessfulImportAtUtc,
        DateTime? LastFailureAtUtc,
        DateTime? ObservedSalesPeriodFromUtc,
        DateTime? ObservedSalesPeriodToUtc)
    {
        public static DurableImportEvidence Empty { get; } = new(null, null, null, null);
    }
}
