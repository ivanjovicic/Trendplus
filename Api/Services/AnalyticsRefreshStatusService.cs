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

    private static readonly Regex ErrorCountRegex = new(
        @"with\s+(?<count>\d+)\s+errors?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        var jobs = new List<AnalyticsRefreshJobStatusDto>
        {
            BuildJobStatus(
                key: "sales_facts_refresh",
                displayName: "Sales facts refresh",
                workerName: NightlyWorkerName,
                freshHours: 26,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "product_dim_refresh",
                displayName: "Product dim refresh",
                workerName: NightlyWorkerName,
                freshHours: 26,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "supplier_decision_mvs",
                displayName: "Supplier decision MVs",
                workerName: NightlyWorkerName,
                freshHours: 26,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "product_decision_snapshot",
                displayName: "Product decision snapshot",
                workerName: NightlyWorkerName,
                freshHours: 26,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "inventory_recommendations",
                displayName: "Inventory recommendations",
                workerName: NightlyWorkerName,
                freshHours: 26,
                staleHours: 72,
                processType,
                workersEnabledInRuntime,
                nowUtc),
            BuildJobStatus(
                key: "data_quality_snapshot",
                displayName: "Data quality snapshot",
                workerName: DataQualityWorkerName,
                freshHours: 3,
                staleHours: 12,
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
            ProcessType = processType.ToString().ToLowerInvariant(),
            WorkersEnabled = workersEnabledInRuntime,
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
            RefreshedObjects = jobs.Count(j => j.DataFreshnessStatus == "fresh"),
            FailedObjects = jobs.Count(j => j.DataFreshnessStatus == "critical"),
            DurationSeconds = jobs
                .Where(j => j.DurationSeconds.HasValue)
                .Select(j => j.DurationSeconds!.Value)
                .DefaultIfEmpty()
                .Max(),
            DataFreshnessStatus = ResolveOverallFreshness(jobs),
            Jobs = jobs
        };

        if (processType == ProcessType.Web && workersEnabledInRuntime)
        {
            status.WorkerProcessWarning =
                "Worker nije aktivan u ovom procesu. Podaci se nece automatski osvezavati osim ako postoji poseban worker deploy.";
        }

        return status;
    }

    private AnalyticsRefreshJobStatusDto BuildJobStatus(
        string key,
        string displayName,
        string workerName,
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
        var failedObjects = ResolveFailedObjects(workerHealth?.LastError);

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
            RefreshedObjects = freshnessStatus == "fresh" ? 1 : 0,
            FailedObjects = failedObjects,
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

    private static int ResolveFailedObjects(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return 0;
        }

        var match = ErrorCountRegex.Match(errorMessage);
        if (match.Success && int.TryParse(match.Groups["count"].Value, out var count))
        {
            return count;
        }

        return 1;
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

    private static string ResolveOverallFreshness(IReadOnlyCollection<AnalyticsRefreshJobStatusDto> jobs)
    {
        if (jobs.Any(j => j.DataFreshnessStatus == "critical"))
        {
            return "critical";
        }

        if (jobs.Any(j => j.DataFreshnessStatus == "stale"))
        {
            return "stale";
        }

        if (jobs.Any(j => j.DataFreshnessStatus == "fresh"))
        {
            return "fresh";
        }

        return "unknown";
    }
}
