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
    public bool SkipInvalidForeignKeys { get; set; }
    // Deleted-row rollback archives are opt-in so routine cleanup cannot grow storage silently.
    public bool ArchiveDeletedRows { get; set; }
    public long ArchiveDeletedRowsMaxBytes { get; set; } = 16 * 1024 * 1024;
    public long ArchiveDeletedRowsMaxRows { get; set; } = 10000;
    // If true, the importer will attempt to insert missing `prodaja_zaglavlje` rows
    // found in the Access file before importing `prodaja_stavke`. Default: false (opt-in).
    public bool AutoInsertMissingParents { get; set; } = false;
    /// <summary>
    /// Logical Access databases are explicitly registered here.  A namespace is an
    /// operator-owned immutable identifier (normally a UUID), never a filename,
    /// filesystem path or file hash.  A normal upload is rejected until its value
    /// is registered.
    /// </summary>
    public List<AccessSourceNamespaceRegistration> SourceNamespaces { get; set; } = [];
    public AccessIncrementalOptions Incremental { get; set; } = new();
}

public sealed class AccessSourceNamespaceRegistration
{
    /// <summary>Immutable logical database identity supplied by the operator.</summary>
    public string Namespace { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    /// <summary>Only registered namespaces may use the routine incremental path.</summary>
    public bool AllowRoutineIncremental { get; set; } = true;
}

public sealed class AccessIncrementalOptions
{
    // Routine uploads are incremental by default.  ID-only profiles deliberately
    // re-read known source identities and compare their hashes; they are not an
    // unsafe `id > cursor` shortcut.
    public bool Enabled { get; set; } = true;

    // shadow | incremental. `full` remains accepted for historical configuration
    // compatibility but does not authorize snapshot deletion or overwrite.
    public string Mode { get; set; } = "incremental";

    public int DefaultBatchSize { get; set; } = 2000;
    public int DefaultOverlapSeconds { get; set; } = 60;

    // Per-table incremental profiles. Physical source-table identity is appended
    // when the cursor is persisted, so aliases such as Prodaja and Prodaja2025
    // cannot share an ID cursor.
    public List<AccessIncrementalTableProfile> Profiles { get; set; } =
    [
        new() { TableKey = "artikli", CursorMode = "id_or_composite" },
        new() { TableKey = "prodaja_zaglavlje", CursorMode = "id_or_composite" },
        new() { TableKey = "prodaja_stavke", CursorMode = "id_or_composite" },
        new() { TableKey = "dnevnik_promena", CursorMode = "id_or_composite" },
        new() { TableKey = "povracaj_zaglavlje", CursorMode = "id_or_composite" },
        new() { TableKey = "povracaj_stavke", CursorMode = "id_or_composite" }
    ];
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
