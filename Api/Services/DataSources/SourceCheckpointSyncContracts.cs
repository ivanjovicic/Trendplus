namespace Api.Services.DataSources;

public sealed record SourceSyncIdentity(string ConnectionId, string MappingProfileId, string SourceStream);

public sealed record SourceSyncRow(
    string? ExternalKey,
    DateTime? CursorTimestampUtc,
    string? TieBreaker,
    string PayloadHash,
    bool Rejected = false,
    string? RejectionReason = null);

public sealed record SourceSyncBatchRequest(
    SourceSyncIdentity Identity,
    string CursorMode,
    string SchemaFingerprint,
    int OverlapSeconds,
    Guid BatchId,
    IReadOnlyList<SourceSyncRow> Rows);

public sealed record SourceSyncMetrics(
    int Read,
    int Inserted,
    int Updated,
    int Skipped,
    int Rejected);

public sealed record SourceSyncCheckpointRecord(
    SourceSyncIdentity Identity,
    string CursorMode,
    DateTime? CursorTimestampUtc,
    string? ExternalKeyTieBreaker,
    int OverlapSeconds,
    string? SchemaFingerprint,
    Guid? LastStartedBatchId,
    Guid? LastCompletedBatchId,
    DateTime? LastSuccessfulSyncUtc,
    string? FailureCategory,
    string? LastError,
    string TenantScope,
    DateTime UpdatedAtUtc);

public sealed record SourceSyncAppliedRowRecord(
    SourceSyncIdentity Identity,
    string ExternalKey,
    string PayloadHash,
    DateTime? CursorTimestampUtc,
    Guid LastBatchId,
    string ApplyStatus,
    string? RejectionReason,
    DateTime UpdatedAtUtc);

public sealed record SourceSyncBatchResult(
    bool Success,
    string? FailureCategory,
    string? FailureMessage,
    SourceSyncMetrics Metrics,
    SourceSyncCheckpointRecord? Checkpoint);

public interface ISourceSyncStore
{
    SourceSyncCheckpointRecord? GetCheckpoint(SourceSyncIdentity identity);
    SourceSyncAppliedRowRecord? GetApplied(SourceSyncIdentity identity, string externalKey);
    void StageApplied(SourceSyncAppliedRowRecord row);
    void StageCheckpoint(SourceSyncCheckpointRecord checkpoint);
    void Commit();
    void Rollback();
}

public enum SourceSyncCrashMode
{
    None,
    BeforeDestinationCommit,
    AfterDestinationBeforeCheckpoint
}
