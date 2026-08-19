using Domain.Model;
using Infrastructure.DbContexts;

namespace Api.Services.DataSources;

/// <summary>
/// PostgreSQL-backed source checkpoint store. Destination rows and checkpoint
/// advance in the same SaveChanges/transaction boundary.
/// Dedicated deployments keep TenantScope = n/a_dedicated.
/// </summary>
public sealed class EfSourceSyncStore : ISourceSyncStore
{
    private readonly TrendplusDbContext _db;
    private readonly Dictionary<string, SourceSyncAppliedRowRecord> _stagedRows = new(StringComparer.Ordinal);
    private SourceSyncCheckpointRecord? _stagedCheckpoint;

    public EfSourceSyncStore(TrendplusDbContext db)
    {
        _db = db;
    }

    public SourceSyncCheckpointRecord? GetCheckpoint(SourceSyncIdentity identity)
    {
        var entity = _db.SourceSyncCheckpoints.Find(
            identity.ConnectionId,
            identity.MappingProfileId,
            identity.SourceStream);
        return entity is null ? null : MapCheckpoint(entity);
    }

    public SourceSyncAppliedRowRecord? GetApplied(SourceSyncIdentity identity, string externalKey)
    {
        if (_stagedRows.TryGetValue(RowKey(identity, externalKey), out var staged))
            return staged;

        var entity = _db.SourceSyncAppliedRows.Find(
            identity.ConnectionId,
            identity.MappingProfileId,
            identity.SourceStream,
            externalKey);
        return entity is null ? null : MapApplied(entity);
    }

    public void StageApplied(SourceSyncAppliedRowRecord row)
        => _stagedRows[RowKey(row.Identity, row.ExternalKey)] = row;

    public void StageCheckpoint(SourceSyncCheckpointRecord checkpoint)
        => _stagedCheckpoint = checkpoint;

    public void Commit()
    {
        using var transaction = _db.Database.BeginTransaction();
        try
        {
            foreach (var row in _stagedRows.Values)
                UpsertApplied(row);

            if (_stagedCheckpoint is not null)
                UpsertCheckpoint(_stagedCheckpoint);

            _db.SaveChanges();
            transaction.Commit();
            _stagedRows.Clear();
            _stagedCheckpoint = null;
        }
        catch
        {
            transaction.Rollback();
            Rollback();
            throw;
        }
    }

    public void Rollback()
    {
        _stagedRows.Clear();
        _stagedCheckpoint = null;
    }

    private void UpsertApplied(SourceSyncAppliedRowRecord row)
    {
        var existing = _db.SourceSyncAppliedRows.Find(
            row.Identity.ConnectionId,
            row.Identity.MappingProfileId,
            row.Identity.SourceStream,
            row.ExternalKey);

        if (existing is null)
        {
            _db.SourceSyncAppliedRows.Add(new SourceSyncAppliedRow
            {
                ConnectionId = row.Identity.ConnectionId,
                MappingProfileId = row.Identity.MappingProfileId,
                SourceStream = row.Identity.SourceStream,
                ExternalKey = row.ExternalKey,
                PayloadHash = row.PayloadHash,
                CursorTimestampUtc = row.CursorTimestampUtc,
                LastBatchId = row.LastBatchId,
                ApplyStatus = row.ApplyStatus,
                RejectionReason = row.RejectionReason,
                UpdatedAtUtc = row.UpdatedAtUtc
            });
            return;
        }

        existing.PayloadHash = row.PayloadHash;
        existing.CursorTimestampUtc = row.CursorTimestampUtc;
        existing.LastBatchId = row.LastBatchId;
        existing.ApplyStatus = row.ApplyStatus;
        existing.RejectionReason = row.RejectionReason;
        existing.UpdatedAtUtc = row.UpdatedAtUtc;
    }

    private void UpsertCheckpoint(SourceSyncCheckpointRecord checkpoint)
    {
        var existing = _db.SourceSyncCheckpoints.Find(
            checkpoint.Identity.ConnectionId,
            checkpoint.Identity.MappingProfileId,
            checkpoint.Identity.SourceStream);

        if (existing is null)
        {
            _db.SourceSyncCheckpoints.Add(new SourceSyncCheckpoint
            {
                ConnectionId = checkpoint.Identity.ConnectionId,
                MappingProfileId = checkpoint.Identity.MappingProfileId,
                SourceStream = checkpoint.Identity.SourceStream,
                CursorMode = checkpoint.CursorMode,
                CursorTimestampUtc = checkpoint.CursorTimestampUtc,
                ExternalKeyTieBreaker = checkpoint.ExternalKeyTieBreaker,
                OverlapSeconds = checkpoint.OverlapSeconds,
                SchemaFingerprint = checkpoint.SchemaFingerprint,
                LastStartedBatchId = checkpoint.LastStartedBatchId,
                LastCompletedBatchId = checkpoint.LastCompletedBatchId,
                LastSuccessfulSyncUtc = checkpoint.LastSuccessfulSyncUtc,
                FailureCategory = checkpoint.FailureCategory,
                LastError = checkpoint.LastError,
                TenantScope = checkpoint.TenantScope,
                UpdatedAtUtc = checkpoint.UpdatedAtUtc
            });
            return;
        }

        existing.CursorMode = checkpoint.CursorMode;
        existing.CursorTimestampUtc = checkpoint.CursorTimestampUtc;
        existing.ExternalKeyTieBreaker = checkpoint.ExternalKeyTieBreaker;
        existing.OverlapSeconds = checkpoint.OverlapSeconds;
        existing.SchemaFingerprint = checkpoint.SchemaFingerprint;
        existing.LastStartedBatchId = checkpoint.LastStartedBatchId;
        existing.LastCompletedBatchId = checkpoint.LastCompletedBatchId;
        existing.LastSuccessfulSyncUtc = checkpoint.LastSuccessfulSyncUtc;
        existing.FailureCategory = checkpoint.FailureCategory;
        existing.LastError = checkpoint.LastError;
        existing.TenantScope = checkpoint.TenantScope;
        existing.UpdatedAtUtc = checkpoint.UpdatedAtUtc;
    }

    private static SourceSyncCheckpointRecord MapCheckpoint(SourceSyncCheckpoint entity)
        => new(
            new SourceSyncIdentity(entity.ConnectionId, entity.MappingProfileId, entity.SourceStream),
            entity.CursorMode,
            entity.CursorTimestampUtc,
            entity.ExternalKeyTieBreaker,
            entity.OverlapSeconds,
            entity.SchemaFingerprint,
            entity.LastStartedBatchId,
            entity.LastCompletedBatchId,
            entity.LastSuccessfulSyncUtc,
            entity.FailureCategory,
            entity.LastError,
            entity.TenantScope,
            entity.UpdatedAtUtc);

    private static SourceSyncAppliedRowRecord MapApplied(SourceSyncAppliedRow entity)
        => new(
            new SourceSyncIdentity(entity.ConnectionId, entity.MappingProfileId, entity.SourceStream),
            entity.ExternalKey,
            entity.PayloadHash,
            entity.CursorTimestampUtc,
            entity.LastBatchId,
            entity.ApplyStatus,
            entity.RejectionReason,
            entity.UpdatedAtUtc);

    private static string RowKey(SourceSyncIdentity identity, string externalKey)
        => $"{identity.ConnectionId}\n{identity.MappingProfileId}\n{identity.SourceStream}\n{externalKey}";
}
