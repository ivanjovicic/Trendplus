namespace Api.Services.Startup;

public sealed class StartupReadinessState
{
    public sealed record DatabaseInitializationSnapshot(
        string State,
        bool Required,
        string? FailureCategory,
        string? FailureStage,
        int Attempts,
        DateTimeOffset? LastAttemptAtUtc,
        DateTimeOffset? CompletedAtUtc);

    public sealed class DatabaseProbeState
    {
        public bool Ok { get; set; }
        public long? LatencyMs { get; set; }
        public string? Error { get; set; }
    }

    private volatile bool _isReady;
    private volatile bool _databaseInitializationRequired;
    private volatile bool _databaseInitializationCompleted;
    private readonly object _databaseInitializationSync = new();
    private string _databaseInitializationState = "not_required";
    private string? _databaseInitializationFailureCategory;
    private string? _databaseInitializationFailureStage;
    private int _databaseInitializationAttempts;
    private DateTimeOffset? _databaseInitializationLastAttemptAtUtc;
    private DateTimeOffset? _databaseInitializationCompletedAtUtc;

    public bool IsReady => _isReady;

    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadyAtUtc { get; private set; }
    public DateTimeOffset? LastProbeAtUtc { get; private set; }
    public string Reason { get; private set; } = "startup";
    public DatabaseProbeState DefaultDb { get; } = new();
    public DatabaseProbeState AnalyticsDb { get; } = new();
    public DatabaseInitializationSnapshot DatabaseInitialization
    {
        get
        {
            lock (_databaseInitializationSync)
            {
                return new DatabaseInitializationSnapshot(
                    _databaseInitializationState,
                    _databaseInitializationRequired,
                    _databaseInitializationFailureCategory,
                    _databaseInitializationFailureStage,
                    _databaseInitializationAttempts,
                    _databaseInitializationLastAttemptAtUtc,
                    _databaseInitializationCompletedAtUtc);
            }
        }
    }

    public void MarkReady()
    {
        if (_databaseInitializationRequired && !_databaseInitializationCompleted)
        {
            _isReady = false;
            ReadyAtUtc = null;
            Reason = "database_initialization";
            return;
        }

        _isReady = true;
        ReadyAtUtc ??= DateTimeOffset.UtcNow;
        Reason = DatabaseInitialization.State == "completed_with_errors"
            ? "ready_degraded_schema"
            : "ready";
    }

    public void RequireDatabaseInitialization()
    {
        lock (_databaseInitializationSync)
        {
            _databaseInitializationRequired = true;
            _databaseInitializationCompleted = false;
            _databaseInitializationState = "pending";
            _databaseInitializationFailureCategory = null;
            _databaseInitializationFailureStage = null;
            _databaseInitializationAttempts = 0;
            _databaseInitializationLastAttemptAtUtc = null;
            _databaseInitializationCompletedAtUtc = null;
        }
        MarkNotReady("database_initialization");
    }

    public void MarkDatabaseInitializationNotRequired()
    {
        lock (_databaseInitializationSync)
        {
            _databaseInitializationRequired = false;
            _databaseInitializationCompleted = true;
            _databaseInitializationState = "not_required";
            _databaseInitializationFailureCategory = null;
            _databaseInitializationFailureStage = null;
            _databaseInitializationCompletedAtUtc = DateTimeOffset.UtcNow;
        }

        if (DefaultDb.Ok && AnalyticsDb.Ok)
        {
            MarkReady();
        }
    }

    public void BeginDatabaseInitializationAttempt()
    {
        lock (_databaseInitializationSync)
        {
            _databaseInitializationRequired = true;
            _databaseInitializationCompleted = false;
            _databaseInitializationState = "pending";
            _databaseInitializationAttempts++;
            _databaseInitializationLastAttemptAtUtc = DateTimeOffset.UtcNow;
            _databaseInitializationCompletedAtUtc = null;
            _databaseInitializationFailureCategory = null;
            _databaseInitializationFailureStage = null;
        }
    }

    public void RecordDatabaseInitializationFailure(string category, string stage)
    {
        lock (_databaseInitializationSync)
        {
            _databaseInitializationFailureCategory = category;
            _databaseInitializationFailureStage = stage;
        }
    }

    public void MarkDatabaseInitializationCompleted(Infrastructure.Seed.DatabaseInitializationOutcome? outcome = null)
    {
        outcome ??= Infrastructure.Seed.DatabaseInitializationOutcome.Succeeded;
        lock (_databaseInitializationSync)
        {
            _databaseInitializationRequired = true;
            _databaseInitializationCompleted = true;
            _databaseInitializationState = outcome.CompletedWithErrors ? "completed_with_errors" : "succeeded";
            _databaseInitializationFailureCategory = outcome.FailureCategory;
            _databaseInitializationFailureStage = outcome.FailureStage;
            _databaseInitializationCompletedAtUtc = DateTimeOffset.UtcNow;
        }

        if (DefaultDb.Ok && AnalyticsDb.Ok)
        {
            MarkReady();
        }
    }

    public void MarkDatabaseInitializationFailed()
    {
        lock (_databaseInitializationSync)
        {
            _databaseInitializationRequired = true;
            _databaseInitializationCompleted = false;
            _databaseInitializationState = "failed";
            _databaseInitializationCompletedAtUtc = DateTimeOffset.UtcNow;
        }

        MarkNotReady("database_initialization_failed");
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
