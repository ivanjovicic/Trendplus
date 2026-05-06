namespace Api.Config;

public sealed class AccessImportOptions
{
    public const string Section = "AccessImport";

    public bool WorkerEnabled { get; set; } = true;
    public bool RegisterWorkerInWebProcess { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 2;
    public int MaxConcurrentJobs { get; set; } = 1;
    public int CliTimeoutSeconds { get; set; } = 60;
    public int PreviewSampleTake { get; set; } = 50;
    public int MaxMetadataParallelism { get; set; } = 2;
    public int DbSaveBatchSize { get; set; } = 1000;
    public int StatusUpdateThrottleSeconds { get; set; } = 5;
    public int HeartbeatIntervalSeconds { get; set; } = 5;
    public int MaxRetryCount { get; set; } = 1;
    public bool EnableAutoRetryForTransientFailures { get; set; } = false;
    public string StorageRoot { get; set; } = "tmp/access-import-jobs";
    public bool EnableSnapshotCopy { get; set; } = true;
    public int PendingBatchStaleMinutes { get; set; } = 10;
    public int PendingBatchRecoveryIntervalSeconds { get; set; } = 60;
    public int RunningBatchStaleMinutes { get; set; } = 240;
    public bool EnableRuntimeBatchSchemaBootstrap { get; set; } = false;
    public bool EnableMdbSql { get; set; }
    public bool EnableFastWritePath { get; set; } = true;
    public bool PreventConcurrentRuns { get; set; } = true;
    public bool SkipInvalidForeignKeys { get; set; } = true;
    // If true, the importer will attempt to insert missing `prodaja_zaglavlje` rows
    // found in the Access file before importing `prodaja_stavke`. Default: false (opt-in).
    public bool AutoInsertMissingParents { get; set; } = false;
    public AccessIncrementalOptions Incremental { get; set; } = new();
}

public sealed class AccessIncrementalOptions
{
    // Global feature toggle.
    public bool Enabled { get; set; }

    // full | shadow | incremental
    public string Mode { get; set; } = "full";

    public int DefaultBatchSize { get; set; } = 2000;
    public int DefaultOverlapSeconds { get; set; } = 60;

    // Per-table incremental profiles.
    public List<AccessIncrementalTableProfile> Profiles { get; set; } = [];
}

public sealed class AccessIncrementalTableProfile
{
    public bool Enabled { get; set; } = true;

    public string TableKey { get; set; } = string.Empty;

    // timestamp | id | none | timestamp_then_id | id_or_composite
    public string CursorMode { get; set; } = "id";

    public int? BatchSize { get; set; }
    public int? OverlapSeconds { get; set; }

    public List<string> CursorTimestampAliases { get; set; } = [];
    public List<string> CursorIdAliases { get; set; } = [];
}
