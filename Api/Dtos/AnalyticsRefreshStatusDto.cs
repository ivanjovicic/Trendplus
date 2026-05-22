namespace Trendplus2.Dtos;

public class AnalyticsRefreshStatusDto
{
    public DateTime? LastSuccessfulRefreshAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastFailureAtUtc { get; set; }
    public bool IsRunning { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? CurrentStep { get; set; }
    public int RefreshedObjects { get; set; }
    public int FailedObjects { get; set; }
    public double? DurationSeconds { get; set; }
    public string DataFreshnessStatus { get; set; } = "unknown";
    public string ProcessType { get; set; } = "web";
    public bool WorkersEnabled { get; set; }
    public string? WorkerProcessWarning { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public List<AnalyticsRefreshJobStatusDto> Jobs { get; set; } = new();
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
    public int RefreshedObjects { get; set; }
    public int FailedObjects { get; set; }
    public double? DurationSeconds { get; set; }
    public string DataFreshnessStatus { get; set; } = "unknown";
    public string? StatusReason { get; set; }
}
