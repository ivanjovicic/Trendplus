using Api.Config;
using Infrastructure.Configuration;
using Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Api.Services;

public sealed class WorkerRegistryService
{
    private readonly IConfiguration _configuration;
    private readonly WorkerConfigurationService _workerConfigurationService;
    private readonly WorkerHealthService _workerHealthService;
    private readonly WorkerRuntimeControlService _runtimeControlService;
    private readonly AccessImportOptions _accessImportOptions;
    private readonly TrendIngestionOptions _trendIngestionOptions;
    private readonly NightlyAnalyticsRefreshOptions _nightlyOptions;
    private readonly OpenTrainingModelTrainingOptions _openTrainingOptions;
    private readonly AnalyticsDataQualityHealthOptions _qualityHealthOptions;

    public WorkerRegistryService(
        IConfiguration configuration,
        WorkerConfigurationService workerConfigurationService,
        WorkerHealthService workerHealthService,
        WorkerRuntimeControlService runtimeControlService,
        IOptions<AccessImportOptions> accessImportOptions,
        IOptions<TrendIngestionOptions> trendIngestionOptions,
        IOptions<NightlyAnalyticsRefreshOptions> nightlyOptions,
        IOptions<OpenTrainingModelTrainingOptions> openTrainingOptions,
        IOptions<AnalyticsDataQualityHealthOptions> qualityHealthOptions)
    {
        _configuration = configuration;
        _workerConfigurationService = workerConfigurationService;
        _workerHealthService = workerHealthService;
        _runtimeControlService = runtimeControlService;
        _accessImportOptions = accessImportOptions.Value;
        _trendIngestionOptions = trendIngestionOptions.Value;
        _nightlyOptions = nightlyOptions.Value;
        _openTrainingOptions = openTrainingOptions.Value;
        _qualityHealthOptions = qualityHealthOptions.Value;
    }

    public async Task<WorkerConfigurationResponseDto> GetConfigurationAsync(CancellationToken ct = default)
    {
        var processType = WorkerRuntimeConfig.ResolveProcessType(_configuration, out _);
        var workersEnabledFromConfig = _configuration.GetValue<bool?>("Workers:Enabled");
        var registerAccessImportWorkerInWebProcess = WorkerRuntimeConfig.ResolveAccessImportWorkerInWebProcess(
            _accessImportOptions.RegisterWorkerInWebProcess,
            _accessImportOptions.WorkerEnabled,
            workersEnabledFromConfig == false,
            processType);

        var definitions = WorkerRegistryCatalog.Definitions;
        var settingsByWorker = await _workerConfigurationService.GetSettingsMapAsync(
            definitions.Select(d => d.WorkerName),
            ct);
        var healthByWorker = _workerHealthService
            .GetAllStatuses()
            .ToDictionary(h => h.WorkerName, StringComparer.OrdinalIgnoreCase);

        var workers = new List<WorkerConfigurationItemDto>(definitions.Count);
        var nowUtc = DateTime.UtcNow;

        foreach (var definition in definitions.OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            settingsByWorker.TryGetValue(definition.WorkerName, out var settings);
            healthByWorker.TryGetValue(definition.WorkerName, out var health);

            var isRegisteredInCurrentProcess = WorkerRuntimeConfig.IsRegisteredInCurrentProcess(
                definition,
                processType,
                registerAccessImportWorkerInWebProcess);
            var scheduleEnabled = settings?.IsScheduleEnabled ?? true;
            var isManuallyStopped = settings?.IsManuallyStopped ?? false;

            // Web-eligible workers (RegistersInWebProcess=true, no AccessImport flag) are only
            // registered as IHostedService when Workers__Enabled=true. If the global switch is off,
            // the worker is not actually running → show ConfiguredButNotRunning, not Unknown.
            var webEligibleButWorkersDisabled =
                processType == ProcessType.Web
                && definition.RegistersInWebProcess
                && !definition.RequiresWebAccessImportFlag
                && !_runtimeControlService.IsEnabled;

            var configuredButNotRunning = !isRegisteredInCurrentProcess || webEligibleButWorkersDisabled;

            var status = ResolveStatus(health, isManuallyStopped, scheduleEnabled, configuredButNotRunning);
            var nextRunAt = ResolveNextRunAt(definition.WorkerName, nowUtc, scheduleEnabled, configuredButNotRunning);
            var lastHeartbeat = health?.LastHeartbeat;

            workers.Add(new WorkerConfigurationItemDto
            {
                WorkerName = definition.WorkerName,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                WorkerType = definition.WorkerType,
                IsRuntimeControllable = definition.IsRuntimeControllable,
                IsScheduleControllable = definition.IsScheduleControllable,
                RuntimeControlReason = definition.RuntimeControlDisabledReason,
                ScheduleControlReason = definition.ScheduleControlDisabledReason,
                Status = status,
                ScheduleEnabled = scheduleEnabled,
                IsManuallyStopped = isManuallyStopped,
                IsRegisteredInCurrentProcess = isRegisteredInCurrentProcess,
                IsConfiguredButNotRunning = configuredButNotRunning,
                LastHeartbeat = lastHeartbeat,
                LastRunAt = lastHeartbeat,
                NextRunAt = nextRunAt,
                LastSuccessAt = health?.Status == WorkerStatusType.Healthy ? health.LastHeartbeat : null,
                LastFailureAt = health?.LastErrorTime,
                LastError = health?.LastError
            });
        }

        return new WorkerConfigurationResponseDto
        {
            ProcessType = processType.ToString().ToLowerInvariant(),
            WorkersEnabledGlobally = _runtimeControlService.IsEnabled,
            RuntimeToggleAllowed = _runtimeControlService.IsRuntimeToggleAllowed,
            Total = workers.Count,
            Workers = workers
        };
    }

    private string ResolveStatus(
        WorkerStatus? health,
        bool isManuallyStopped,
        bool scheduleEnabled,
        bool configuredButNotRunning)
    {
        if (health is not null)
            return health.Status.ToString();

        if (configuredButNotRunning)
            return "ConfiguredButNotRunning";

        if (isManuallyStopped)
            return "Stopped";

        if (!scheduleEnabled)
            return "ScheduleDisabled";

        return "Unknown";
    }

    private DateTime? ResolveNextRunAt(
        string workerName,
        DateTime nowUtc,
        bool scheduleEnabled,
        bool configuredButNotRunning)
    {
        if (!scheduleEnabled || configuredButNotRunning)
            return null;

        return workerName switch
        {
            "AccessImportBackgroundWorker" => nowUtc.AddSeconds(Math.Max(1, _accessImportOptions.PollingIntervalSeconds)),
            "SyncWorker" => nowUtc.AddSeconds(60),
            "OutboxProcessorWorker" => nowUtc.AddSeconds(30),
            "AnalyticsAggregationWorker" => nowUtc.AddMinutes(5),
            "AnalyticsDataQualityHealthWorker" => nowUtc.AddMinutes(Math.Max(5, _qualityHealthOptions.PollIntervalMinutes)),
            "NightlyAnalyticsRefreshWorker" => ResolveNextNightlyRun(nowUtc, _nightlyOptions.RunAtUtc),
            "OpenTrainingModelTrainingWorker" => nowUtc.AddSeconds(Math.Max(1, _openTrainingOptions.PollSeconds)),
            "TrendIngestionWorker" => ResolveNextDailyRunAtHour(nowUtc, _trendIngestionOptions.RunAtHourUtc),
            "DocumentGenerationWorker" => nowUtc.AddSeconds(10),
            "InventoryReportSchedulerWorker" => nowUtc.AddMinutes(1),
            _ => null
        };
    }

    private static DateTime ResolveNextDailyRunAtHour(DateTime nowUtc, int hourUtc)
    {
        var safeHour = Math.Clamp(hourUtc, 0, 23);
        var todayRun = nowUtc.Date.AddHours(safeHour);
        return nowUtc < todayRun ? todayRun : todayRun.AddDays(1);
    }

    private static DateTime ResolveNextNightlyRun(DateTime nowUtc, string runAtUtc)
    {
        var parsed = TimeSpan.Zero;
        if (!TimeSpan.TryParse(runAtUtc, out parsed))
        {
            parsed = TimeSpan.FromMinutes(10);
        }

        var todayRun = nowUtc.Date.Add(parsed);
        return nowUtc < todayRun ? todayRun : todayRun.AddDays(1);
    }
}

public sealed class WorkerConfigurationResponseDto
{
    public string ProcessType { get; set; } = "web";
    public bool WorkersEnabledGlobally { get; set; }
    public bool RuntimeToggleAllowed { get; set; }
    public int Total { get; set; }
    public List<WorkerConfigurationItemDto> Workers { get; set; } = new();
}

public sealed class WorkerConfigurationItemDto
{
    public string WorkerName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WorkerType { get; set; } = string.Empty;
    public bool IsRuntimeControllable { get; set; }
    public bool IsScheduleControllable { get; set; }
    public string? RuntimeControlReason { get; set; }
    public string? ScheduleControlReason { get; set; }
    public string Status { get; set; } = "Unknown";
    public bool ScheduleEnabled { get; set; }
    public bool IsManuallyStopped { get; set; }
    public bool IsRegisteredInCurrentProcess { get; set; }
    public bool IsConfiguredButNotRunning { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public string? LastError { get; set; }
}
