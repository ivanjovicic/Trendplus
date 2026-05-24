namespace Domain.Model.Analytics;

public class AnalyticsRefreshRun
{
    public long Id { get; set; }

    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string Status { get; set; } = "running";

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
    public double? DurationSeconds { get; set; }

    public string? RefreshedObjectsJson { get; set; }
    public string? FailedObjectsJson { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CorrelationId { get; set; }

    public string TriggeredBy { get; set; } = "system";
    public string ProcessMode { get; set; } = "unknown";
    public string? WorkerName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
