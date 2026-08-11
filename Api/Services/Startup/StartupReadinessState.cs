namespace Api.Services.Startup;

public sealed class StartupReadinessState
{
    public sealed class DatabaseProbeState
    {
        public bool Ok { get; set; }
        public long? LatencyMs { get; set; }
        public string? Error { get; set; }
    }

    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadyAtUtc { get; private set; }
    public DateTimeOffset? LastProbeAtUtc { get; private set; }
    public string Reason { get; private set; } = "startup";
    public DatabaseProbeState DefaultDb { get; } = new();
    public DatabaseProbeState AnalyticsDb { get; } = new();

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

    public void ReportProbe(DatabaseProbeState defaultDb, DatabaseProbeState analyticsDb)
    {
        DefaultDb.Ok = defaultDb.Ok;
        DefaultDb.LatencyMs = defaultDb.LatencyMs;
        DefaultDb.Error = defaultDb.Error;

        AnalyticsDb.Ok = analyticsDb.Ok;
        AnalyticsDb.LatencyMs = analyticsDb.LatencyMs;
        AnalyticsDb.Error = analyticsDb.Error;

        LastProbeAtUtc = DateTimeOffset.UtcNow;
    }
}
