namespace Infrastructure.Services;

/// <summary>
/// Global runtime switch for background workers.
/// Workers stay registered, but can pause/resume execution without app restart.
/// </summary>
public class WorkerRuntimeControlService
{
    private readonly object _lock = new();
    private bool _enabled;
    private readonly bool _runtimeToggleAllowed;

    public WorkerRuntimeControlService(bool initialEnabled, bool runtimeToggleAllowed, string? initialSource = null)
    {
        _enabled = initialEnabled;
        _runtimeToggleAllowed = runtimeToggleAllowed;
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
    public bool IsRuntimeToggleAllowed => _runtimeToggleAllowed;

    public bool SetEnabled(bool enabled, string? changedBy = null)
    {
        if (!_runtimeToggleAllowed && enabled)
        {
            return false;
        }

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
                RuntimeToggleAllowed = _runtimeToggleAllowed,
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
    public bool RuntimeToggleAllowed { get; set; }
    public DateTime LastChangedUtc { get; set; }
    public string LastChangedBy { get; set; } = string.Empty;
}
