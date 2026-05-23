using System.Globalization;
using System.Text.RegularExpressions;
using Api.Config;
using Infrastructure.Services;
using Trendplus2.Dtos;

namespace Api.Services;

public sealed class AnalyticsRefreshStatusService
{
    private const string NightlyWorkerName = "NightlyAnalyticsRefreshWorker";
    private const string DataQualityWorkerName = "AnalyticsDataQualityHealthWorker";

    private static readonly Regex LastSuccessRegex = new(
        @"Last success:\s*(?<value>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}Z|n\/a)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DurationRegex = new(
        @"(?:completed in|Duration:\s*)(?<seconds>\d+(?:\.\d+)?)s",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly TimeSpan ActiveWorkerHeartbeatThreshold = TimeSpan.FromMinutes(15);

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly WorkerHealthService _workerHealthService;
    private readonly WorkerRuntimeControlService _workerRuntimeControlService;

    public AnalyticsRefreshStatusService(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        WorkerHealthService workerHealthService,
        WorkerRuntimeControlService workerRuntimeControlService)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _workerHealthService = workerHealthService;
        _workerRuntimeControlService = workerRuntimeControlService;
    }

    public AnalyticsRefreshStatusDto GetStatus()
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

        var jobs = new List<AnalyticsRefreshJobStatusDto>
        {
            BuildJobStatus(
                key: "sales_facts_refresh",
                displayName: "Sales facts refresh",
                workerName: NightlyWorkerName,
                refreshedObjectNames: ["sales_facts_mv"],
                freshHours: 24,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "product_dim_refresh",
                displayName: "Product dim refresh",
                workerName: NightlyWorkerName,
                refreshedObjectNames: ["product_dim_mv"],
                freshHours: 24,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "supplier_decision_mvs",
                displayName: "Supplier decision MVs",
                workerName: NightlyWorkerName,
                refreshedObjectNames:
                [
                    "mv_supplier_decision_score_cache_90d",
                    "mv_supplier_decision_score_cache_180d",
                    "mv_supplier_decision_score_cache"
                ],
                freshHours: 24,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "product_decision_snapshot",
                displayName: "Product decision snapshot",
                workerName: NightlyWorkerName,
                refreshedObjectNames: ["mv_product_decision_snapshot"],
                freshHours: 24,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "inventory_recommendations",
                displayName: "Inventory recommendations",
                workerName: NightlyWorkerName,
                refreshedObjectNames: ["mv_inventory_recommendations"],
                freshHours: 24,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "data_quality_snapshot",
                displayName: "Data quality snapshot",
                workerName: DataQualityWorkerName,
                refreshedObjectNames: ["analytics_data_quality_history"],
                freshHours: 24,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc)
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

        var status = new AnalyticsRefreshStatusDto
        {
            ProcessMode = processMode,
            WorkersEnabled = workersEnabled,
            LastSuccessfulRefreshAtUtc = hasLastSuccess ? lastSuccess : null,
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
            DurationSeconds = jobs
                .Where(j => j.DurationSeconds.HasValue)
                .Select(j => j.DurationSeconds!.Value)
                .DefaultIfEmpty()
                .Max(),
            DataFreshnessStatus = ResolveOverallFreshness(
                hasLastSuccess ? lastSuccess : null,
                hasLastFailure ? lastFailure : null,
                nowUtc),
            Jobs = jobs
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
        IReadOnlyList<string> refreshedObjectNames,
        int freshHours,
        int staleHours,
        ProcessType processType,
        bool workersEnabledInRuntime,
        DateTime nowUtc)
    {
        var workerDefinition = WorkerRegistryCatalog.Find(workerName);
        var workerHealth = _workerHealthService.GetStatus(workerName);
        var canRegisterInProcess = workerDefinition is not null &&
            WorkerRuntimeConfig.IsRegisteredInCurrentProcess(
                workerDefinition,
                processType,
                registerAccessImportWorkerInWebProcess: false);
        var workerIsExpectedToRun = canRegisterInProcess && workersEnabledInRuntime;
        var isRunning = workerHealth?.Status == WorkerStatusType.Running;

        var lastSuccess = ResolveLastSuccess(workerName, workerHealth);
        var lastAttempt = workerHealth?.LastHeartbeat;
        var lastFailure = workerHealth?.LastErrorTime;
        var durationSeconds = ResolveDurationSeconds(workerHealth?.Message, workerHealth?.LastError);
        var refreshSucceeded = lastSuccess.HasValue && (!lastFailure.HasValue || lastSuccess.Value >= lastFailure.Value);
        var refreshFailed = lastFailure.HasValue && (!lastSuccess.HasValue || lastFailure.Value > lastSuccess.Value);

        var freshnessStatus = ResolveFreshnessStatus(
            lastSuccess,
            lastFailure,
            workerHealth?.Status,
            nowUtc,
            freshHours,
            staleHours);

        string? reason = null;
        if (!workerIsExpectedToRun)
        {
            freshnessStatus = "unknown";
            reason = processType == ProcessType.Web
                ? "Worker nije registrovan u web procesu."
                : "Worker je iskljucen runtime kontrolom.";
        }
        else if (lastSuccess is null && lastFailure is null)
        {
            freshnessStatus = "unknown";
            reason = "Nema istorije osvezavanja za ovaj posao.";
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
            LastErrorMessage = workerHealth?.LastError,
            CurrentStep = workerHealth?.Message,
            RefreshedObjects = refreshSucceeded ? refreshedObjectNames.ToList() : [],
            FailedObjects = refreshFailed ? refreshedObjectNames.ToList() : [],
            DurationSeconds = durationSeconds,
            DataFreshnessStatus = freshnessStatus,
            StatusReason = reason
        };
    }

    private static DateTime? ResolveLastSuccess(string workerName, WorkerStatus? workerHealth)
    {
        if (workerHealth is null)
        {
            return null;
        }

        if (string.Equals(workerName, NightlyWorkerName, StringComparison.OrdinalIgnoreCase))
        {
            var message = workerHealth.Message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                var match = LastSuccessRegex.Match(message);
                if (match.Success)
                {
                    var value = match.Groups["value"].Value.Trim();
                    if (string.Equals(value, "n/a", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    if (DateTime.TryParseExact(
                        value,
                        "yyyy-MM-dd HH:mm:ss'Z'",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsed))
                    {
                        return parsed;
                    }
                }
            }
        }

        if (workerHealth.Status == WorkerStatusType.Healthy && workerHealth.LastErrorTime is null)
        {
            return workerHealth.LastHeartbeat;
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
            ? seconds
            : null;
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
        if (age <= TimeSpan.FromHours(Math.Max(1, freshHours)))
        {
            return "fresh";
        }

        if (age <= TimeSpan.FromHours(Math.Max(freshHours + 1, staleHours)))
        {
            return "stale";
        }

        return "critical";
    }

    private static string ResolveOverallFreshness(DateTime? lastSuccess, DateTime? lastFailure, DateTime nowUtc)
    {
        if (!lastSuccess.HasValue)
        {
            return "unknown";
        }

        if (lastFailure.HasValue && lastFailure.Value > lastSuccess.Value)
        {
            return "critical";
        }

        var age = nowUtc - lastSuccess.Value;
        if (age <= TimeSpan.FromHours(24))
        {
            return "fresh";
        }

        if (age <= TimeSpan.FromHours(72))
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
}
