namespace Api.Services.Startup;

public sealed class StartupReadinessState
{
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadyAtUtc { get; private set; }
    public string Reason { get; private set; } = "startup";

    public void MarkReady()
    {
        _isReady = true;
        ReadyAtUtc ??= DateTimeOffset.UtcNow;
        Reason = "ready";
    }

    public void MarkNotReady(string reason)
    {
        _isReady = false;
        ReadyAtUtc = null;
        Reason = string.IsNullOrWhiteSpace(reason) ? "not_ready" : reason;
    }
}
