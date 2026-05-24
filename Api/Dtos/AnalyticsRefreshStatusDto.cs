namespace Trendplus2.Dtos;

public class AnalyticsRefreshStatusDto
{
    public DateTime? LastSuccessfulRefreshAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastFailureAtUtc { get; set; }
    public bool IsRunning { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? CurrentStep { get; set; }
    public List<string> RefreshedObjects { get; set; } = new();
    public List<string> FailedObjects { get; set; } = new();
    public double? DurationSeconds { get; set; }
    public string DataFreshnessStatus { get; set; } = "unknown";
    public string ProcessMode { get; set; } = "unknown";
    public string ProcessType
    {
        get => ProcessMode;
        set => ProcessMode = value;
    }
    public bool WorkersEnabled { get; set; }
    public string? WorkerWarning { get; set; }
    public string? WorkerProcessWarning
    {
        get => WorkerWarning;
        set => WorkerWarning = value;
    }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public List<AnalyticsRefreshJobStatusDto> Jobs { get; set; } = new();
    public List<AnalyticsRefreshRunDto> RecentRuns { get; set; } = new();
}

public class AnalyticsRefreshJobStatusDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public DateTime? LastSuccessfulRefreshAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastFailureAtUtc { get; set; }
    public bool IsRunning { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? CurrentStep { get; set; }
    public List<string> RefreshedObjects { get; set; } = new();
    public List<string> FailedObjects { get; set; } = new();
    public double? DurationSeconds { get; set; }
    public string DataFreshnessStatus { get; set; } = "unknown";
    public string? StatusReason { get; set; }
}

public class AnalyticsRefreshRunDto
{
    public long Id { get; set; }
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public double? DurationSeconds { get; set; }
    public List<string> RefreshedObjects { get; set; } = new();
    public List<string> FailedObjects { get; set; } = new();
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CorrelationId { get; set; }
    public string TriggeredBy { get; set; } = "system";
    public string ProcessMode { get; set; } = "unknown";
    public string? WorkerName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
