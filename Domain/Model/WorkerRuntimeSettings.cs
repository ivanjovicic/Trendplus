namespace Domain.Model;

/// <summary>
/// Per-worker runtime configuration and state tracking.
/// Stores schedule enable/disable and manual stop state for each worker.
/// </summary>
public class WorkerRuntimeSettings
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique worker name (e.g., "AccessImportBackgroundWorker", "AnalyticsAggregationWorker")
    /// </summary>
    public string WorkerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the worker's scheduled execution is enabled.
    /// When disabled, the worker should not run automatically at its planned schedule,
    /// but manual "start now" should still be allowed unless explicitly blocked.
    /// </summary>
    public bool IsScheduleEnabled { get; set; } = true;
    
    /// <summary>
    /// Whether the worker has been manually stopped by an admin.
    /// When true, the worker should not run even if schedule is enabled or global workers are enabled.
    /// </summary>
    public bool IsManuallyStopped { get; set; } = false;
    
    /// <summary>
    /// Last time this worker configuration was updated.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who last updated this worker configuration (e.g., admin username or "api").
    /// </summary>
    public string? UpdatedBy { get; set; }
    
    /// <summary>
    /// Notes or reason for the current state.
    /// </summary>
    public string? Notes { get; set; }
}
