namespace Infrastructure.Services;

/// <summary>
/// Tracks the health status of background workers.
/// Singleton service that workers update and API endpoints can query.
/// </summary>
public class WorkerHealthService
{
    private readonly Dictionary<string, WorkerStatus> _workerStatuses = new();
    private readonly object _lock = new();

    public void ReportHealthy(string workerName, string? message = null)
    {
        lock (_lock)
        {
            _workerStatuses[workerName] = new WorkerStatus
            {
                WorkerName = workerName,
                Status = WorkerStatusType.Healthy,
                LastHeartbeat = DateTime.UtcNow,
                Message = message,
                LastError = null,
                ErrorCount = 0
            };
        }
    }

    public void ReportRunning(string workerName, string? message = null)
    {
        lock (_lock)
        {
            if (_workerStatuses.TryGetValue(workerName, out var existing))
            {
                existing.Status = WorkerStatusType.Running;
                existing.LastHeartbeat = DateTime.UtcNow;
                existing.Message = message;
            }
            else
            {
                _workerStatuses[workerName] = new WorkerStatus
                {
                    WorkerName = workerName,
                    Status = WorkerStatusType.Running,
                    LastHeartbeat = DateTime.UtcNow,
                    Message = message
                };
            }
        }
    }

    public void ReportError(string workerName, Exception ex)
    {
        lock (_lock)
        {
            if (_workerStatuses.TryGetValue(workerName, out var existing))
            {
                existing.Status = WorkerStatusType.Error;
                existing.LastHeartbeat = DateTime.UtcNow;
                existing.LastError = $"{ex.GetType().Name}: {ex.Message}";
                existing.LastErrorTime = DateTime.UtcNow;
                existing.ErrorCount++;
            }
            else
            {
                _workerStatuses[workerName] = new WorkerStatus
                {
                    WorkerName = workerName,
                    Status = WorkerStatusType.Error,
                    LastHeartbeat = DateTime.UtcNow,
                    LastError = $"{ex.GetType().Name}: {ex.Message}",
                    LastErrorTime = DateTime.UtcNow,
                    ErrorCount = 1
                };
            }
        }
    }

    public void ReportStopped(string workerName, string? reason = null)
    {
        lock (_lock)
        {
            if (_workerStatuses.TryGetValue(workerName, out var existing))
            {
                existing.Status = WorkerStatusType.Stopped;
                existing.LastHeartbeat = DateTime.UtcNow;
                existing.Message = reason;
            }
            else
            {
                _workerStatuses[workerName] = new WorkerStatus
                {
                    WorkerName = workerName,
                    Status = WorkerStatusType.Stopped,
                    LastHeartbeat = DateTime.UtcNow,
                    Message = reason
                };
            }
        }
    }

    public WorkerStatus? GetStatus(string workerName)
    {
        lock (_lock)
        {
            return _workerStatuses.TryGetValue(workerName, out var status) ? status : null;
        }
    }

    public IReadOnlyList<WorkerStatus> GetAllStatuses()
    {
        lock (_lock)
        {
            return _workerStatuses.Values.ToList();
        }
    }

    public WorkerHealthSummary GetHealthSummary()
    {
        lock (_lock)
        {
            var statuses = _workerStatuses.Values.ToList();
            var staleThreshold = DateTime.UtcNow.AddMinutes(-10); // Worker is stale if no heartbeat in 10 minutes

            return new WorkerHealthSummary
            {
                TotalWorkers = statuses.Count,
                HealthyWorkers = statuses.Count(s => s.Status == WorkerStatusType.Healthy && s.LastHeartbeat > staleThreshold),
                RunningWorkers = statuses.Count(s => s.Status == WorkerStatusType.Running),
                ErrorWorkers = statuses.Count(s => s.Status == WorkerStatusType.Error),
                StoppedWorkers = statuses.Count(s => s.Status == WorkerStatusType.Stopped),
                StaleWorkers = statuses.Count(s => s.LastHeartbeat <= staleThreshold),
                Workers = statuses.Select(s => new WorkerStatusDto
                {
                    WorkerName = s.WorkerName,
                    Status = s.Status.ToString(),
                    LastHeartbeat = s.LastHeartbeat,
                    Message = s.Message,
                    LastError = s.LastError,
                    LastErrorTime = s.LastErrorTime,
                    ErrorCount = s.ErrorCount,
                    IsStale = s.LastHeartbeat <= staleThreshold
                }).ToList(),
                HasCriticalIssues = statuses.Any(s => s.Status == WorkerStatusType.Error || s.LastHeartbeat <= staleThreshold)
            };
        }
    }
}

public class WorkerStatus
{
    public string WorkerName { get; set; } = string.Empty;
    public WorkerStatusType Status { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public string? Message { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorTime { get; set; }
    public int ErrorCount { get; set; }
}

public enum WorkerStatusType
{
    Unknown,
    Starting,
    Running,
    Healthy,
    Error,
    Stopped
}

public class WorkerHealthSummary
{
    public int TotalWorkers { get; set; }
    public int HealthyWorkers { get; set; }
    public int RunningWorkers { get; set; }
    public int ErrorWorkers { get; set; }
    public int StoppedWorkers { get; set; }
    public int StaleWorkers { get; set; }
    public bool HasCriticalIssues { get; set; }
    public List<WorkerStatusDto> Workers { get; set; } = new();
}

public class WorkerStatusDto
{
    public string WorkerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastHeartbeat { get; set; }
    public string? Message { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorTime { get; set; }
    public int ErrorCount { get; set; }
    public bool IsStale { get; set; }
}
