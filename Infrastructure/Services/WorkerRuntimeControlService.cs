namespace Infrastructure.Services;

/// <summary>
/// Global runtime switch for background workers.
/// Workers stay registered, but can pause/resume execution without app restart.
/// </summary>
public class WorkerRuntimeControlService
{
    private readonly object _lock = new();
    private bool _enabled;

    public WorkerRuntimeControlService(bool initialEnabled, string? initialSource = null)
    {
        _enabled = initialEnabled;
        LastChangedUtc = DateTime.UtcNow;
        LastChangedBy = string.IsNullOrWhiteSpace(initialSource) ? "startup" : initialSource;
    }

    public bool IsEnabled
    {
        get
        {
            lock (_lock)
            {
                return _enabled;
            }
        }
    }

    public DateTime LastChangedUtc { get; private set; }
    public string LastChangedBy { get; private set; } = "startup";

    public bool SetEnabled(bool enabled, string? changedBy = null)
    {
        lock (_lock)
        {
            if (_enabled == enabled)
            {
                return false;
            }

            _enabled = enabled;
            LastChangedUtc = DateTime.UtcNow;
            LastChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "api" : changedBy!;
            return true;
        }
    }

    public WorkerRuntimeControlStateDto GetState(string environmentName)
    {
        lock (_lock)
        {
            return new WorkerRuntimeControlStateDto
            {
                Enabled = _enabled,
                Environment = environmentName,
                LastChangedUtc = LastChangedUtc,
                LastChangedBy = LastChangedBy
            };
        }
    }
}

public class WorkerRuntimeControlStateDto
{
    public bool Enabled { get; set; }
    public string Environment { get; set; } = string.Empty;
    public DateTime LastChangedUtc { get; set; }
    public string LastChangedBy { get; set; } = string.Empty;
}

