namespace Infrastructure.Services;

/// <summary>
/// Projects worker health/control state onto the OBS08 worker SLA evidence contract.
/// Uninstrumented fields stay null/unknown. This mapper must not invent healthy zeros.
/// </summary>
public static class WorkerSlaEvidenceMapper
{
    public const string ContractId = "OBS08";
    public const string QueueDepthUnknownCode = "w5_queue_depth_not_instrumented";
    public const string OldestWorkUnknownCode = "w5_oldest_work_age_not_instrumented";
    public const string LastSuccessUnknownCode = "last_successful_run_unknown";
    public const string RetryDlqUnknownCode = "w6_retry_dead_letter_not_instrumented";
    public const string InventoryMissingCode = "worker_inventory_missing";
    public const string GlobalPausedCode = "workers_globally_paused";

    public static WorkerSlaEvidenceSnapshot Capture(
        WorkerHealthSummary summary,
        bool workersGloballyEnabled,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var workers = summary.Workers ?? [];
        var warningCodes = new List<string>
        {
            QueueDepthUnknownCode,
            OldestWorkUnknownCode,
            LastSuccessUnknownCode,
            RetryDlqUnknownCode
        };

        if (workers.Count == 0)
            warningCodes.Add(InventoryMissingCode);

        if (!workersGloballyEnabled)
            warningCodes.Add(GlobalPausedCode);

        return new WorkerSlaEvidenceSnapshot
        {
            ContractId = ContractId,
            CapturedAtUtc = utcNow,
            WorkersGloballyEnabled = workersGloballyEnabled,
            InventoryStatus = workers.Count == 0 ? "unknown" : "partial",
            DataQualityStatus = workers.Count == 0 || !workersGloballyEnabled ? "unknown" : "partial",
            WarningCodes = warningCodes,
            Workers = workers.Select(worker => MapWorker(worker, workersGloballyEnabled, utcNow)).ToList()
        };
    }

    private static WorkerSlaEvidenceRecord MapWorker(
        WorkerStatusDto worker,
        bool workersGloballyEnabled,
        DateTime utcNow)
    {
        var heartbeatUnknown = worker.LastHeartbeat == default;
        var heartbeatAgeSeconds = heartbeatUnknown
            ? (int?)null
            : Math.Max(0, (int)Math.Round((utcNow - worker.LastHeartbeat).TotalSeconds));

        string executionState;
        if (!workersGloballyEnabled)
            executionState = "paused";
        else if (string.Equals(worker.Status, nameof(WorkerStatusType.Stopped), StringComparison.OrdinalIgnoreCase))
            executionState = "paused";
        else if (heartbeatUnknown || worker.IsStale)
            executionState = "unknown";
        else if (string.Equals(worker.Status, nameof(WorkerStatusType.Running), StringComparison.OrdinalIgnoreCase))
            executionState = "running";
        else if (string.Equals(worker.Status, nameof(WorkerStatusType.Error), StringComparison.OrdinalIgnoreCase))
            executionState = "failed";
        else
            executionState = "enabled";

        return new WorkerSlaEvidenceRecord
        {
            WorkerName = worker.WorkerName,
            WorkersGloballyEnabled = workersGloballyEnabled,
            ExecutionState = executionState,
            PauseReason = !workersGloballyEnabled
                ? "Global workers switch is off."
                : string.Equals(worker.Status, nameof(WorkerStatusType.Stopped), StringComparison.OrdinalIgnoreCase)
                    ? worker.Message
                    : null,
            LastHeartbeatAtUtc = heartbeatUnknown ? null : worker.LastHeartbeat,
            HeartbeatAgeSeconds = heartbeatAgeSeconds,
            QueueDepth = null,
            OldestWorkAgeSeconds = null,
            RunDurationSeconds = null,
            SuccessCount = null,
            FailureCount = null,
            RetryCount = null,
            DeadLetterCount = null,
            LastSuccessfulRunAtUtc = null,
            LastSuccessfulRunAgeSeconds = null,
            LastErrorPresent = !string.IsNullOrWhiteSpace(worker.LastError),
            SourceJobId = null,
            SourceSystem = null,
            CorrelationId = null,
            WarningCodes =
            [
                QueueDepthUnknownCode,
                OldestWorkUnknownCode,
                LastSuccessUnknownCode,
                RetryDlqUnknownCode
            ],
            DataQualityStatus = "partial"
        };
    }
}

public sealed class WorkerSlaEvidenceSnapshot
{
    public string ContractId { get; init; } = WorkerSlaEvidenceMapper.ContractId;
    public DateTime CapturedAtUtc { get; init; }
    public bool WorkersGloballyEnabled { get; init; }
    public string InventoryStatus { get; init; } = "unknown";
    public string DataQualityStatus { get; init; } = "unknown";
    public IReadOnlyList<string> WarningCodes { get; init; } = [];
    public IReadOnlyList<WorkerSlaEvidenceRecord> Workers { get; init; } = [];
}

public sealed class WorkerSlaEvidenceRecord
{
    public string WorkerName { get; init; } = string.Empty;
    public bool WorkersGloballyEnabled { get; init; }
    public string ExecutionState { get; init; } = "unknown";
    public string? PauseReason { get; init; }
    public DateTime? LastHeartbeatAtUtc { get; init; }
    public int? HeartbeatAgeSeconds { get; init; }
    public int? QueueDepth { get; init; }
    public int? OldestWorkAgeSeconds { get; init; }
    public int? RunDurationSeconds { get; init; }
    public int? SuccessCount { get; init; }
    public int? FailureCount { get; init; }
    public int? RetryCount { get; init; }
    public int? DeadLetterCount { get; init; }
    public DateTime? LastSuccessfulRunAtUtc { get; init; }
    public int? LastSuccessfulRunAgeSeconds { get; init; }
    public bool LastErrorPresent { get; init; }
    public string? SourceJobId { get; init; }
    public string? SourceSystem { get; init; }
    public string? CorrelationId { get; init; }
    public IReadOnlyList<string> WarningCodes { get; init; } = [];
    public string DataQualityStatus { get; init; } = "unknown";
}
