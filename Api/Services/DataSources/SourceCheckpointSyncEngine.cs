namespace Api.Services.DataSources;

/// <summary>
/// Applies a mapped source batch to a destination store and advances the checkpoint
/// only after destination effects are staged for the same commit.
/// Missing/rejected rows never become fake zeros or duplicate identities.
/// </summary>
public sealed class SourceCheckpointSyncEngine
{
    public const string DedicatedTenantScope = "n/a_dedicated";
    public const string SchemaDriftCategory = "schema_drift";
    public const string IdentityRequiredCategory = "identity_required";

    public SourceSyncBatchResult Apply(ISourceSyncStore store, SourceSyncBatchRequest request, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(request);

        var identity = Normalize(request.Identity);
        if (string.IsNullOrWhiteSpace(identity.ConnectionId)
            || string.IsNullOrWhiteSpace(identity.MappingProfileId)
            || string.IsNullOrWhiteSpace(identity.SourceStream))
        {
            return Failed(IdentityRequiredCategory, "Connection, mapping profile and source stream are required.", Count(request.Rows));
        }

        var existing = store.GetCheckpoint(identity);
        if (existing?.SchemaFingerprint is { Length: > 0 }
            && !string.Equals(existing.SchemaFingerprint, request.SchemaFingerprint, StringComparison.Ordinal))
        {
            var blocked = existing with
            {
                FailureCategory = SchemaDriftCategory,
                LastError = "Schema fingerprint changed; mapping is blocked until the profile is revised.",
                LastStartedBatchId = request.BatchId,
                UpdatedAtUtc = utcNow
            };
            store.StageCheckpoint(blocked);
            try
            {
                store.Commit();
            }
            catch
            {
                store.Rollback();
                throw;
            }

            return new SourceSyncBatchResult(
                false,
                SchemaDriftCategory,
                blocked.LastError,
                Count(request.Rows),
                blocked);
        }

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var rejected = 0;
        DateTime? maxTimestamp = existing?.CursorTimestampUtc;
        string? maxTie = existing?.ExternalKeyTieBreaker;

        foreach (var row in request.Rows)
        {
            var key = row.ExternalKey?.Trim();
            if (string.IsNullOrWhiteSpace(key) || row.Rejected)
            {
                rejected++;
                continue;
            }

            var previous = store.GetApplied(identity, key);
            string status;
            if (previous is null)
            {
                status = "inserted";
                inserted++;
            }
            else if (string.Equals(previous.PayloadHash, row.PayloadHash, StringComparison.Ordinal))
            {
                status = "skipped";
                skipped++;
            }
            else
            {
                status = "updated";
                updated++;
            }

            store.StageApplied(new SourceSyncAppliedRowRecord(
                identity,
                key,
                row.PayloadHash,
                row.CursorTimestampUtc,
                request.BatchId,
                status,
                null,
                utcNow));

            if (status is "inserted" or "updated")
                AdvanceCursor(ref maxTimestamp, ref maxTie, row.CursorTimestampUtc, row.TieBreaker ?? key);
        }

        var checkpoint = new SourceSyncCheckpointRecord(
            identity,
            string.IsNullOrWhiteSpace(request.CursorMode) ? existing?.CursorMode ?? "id" : request.CursorMode.Trim().ToLowerInvariant(),
            maxTimestamp,
            maxTie,
            request.OverlapSeconds > 0 ? request.OverlapSeconds : existing?.OverlapSeconds ?? 60,
            request.SchemaFingerprint,
            request.BatchId,
            request.BatchId,
            utcNow,
            null,
            null,
            DedicatedTenantScope,
            utcNow);

        store.StageCheckpoint(checkpoint);
        try
        {
            store.Commit();
        }
        catch
        {
            store.Rollback();
            throw;
        }

        return new SourceSyncBatchResult(
            true,
            null,
            null,
            new SourceSyncMetrics(request.Rows.Count, inserted, updated, skipped, rejected),
            checkpoint);
    }

    private static SourceSyncMetrics Count(IReadOnlyList<SourceSyncRow> rows)
        => new(rows.Count, 0, 0, 0, 0);

    private static SourceSyncBatchResult Failed(string category, string message, SourceSyncMetrics metrics)
        => new(false, category, message, metrics, null);

    private static SourceSyncIdentity Normalize(SourceSyncIdentity identity)
        => new(
            Norm(identity.ConnectionId),
            Norm(identity.MappingProfileId),
            Norm(identity.SourceStream));

    private static string Norm(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static void AdvanceCursor(
        ref DateTime? maxTimestamp,
        ref string? maxTie,
        DateTime? rowTimestamp,
        string tie)
    {
        if (rowTimestamp is null)
        {
            if (maxTimestamp is null && (maxTie is null || string.CompareOrdinal(tie, maxTie) > 0))
                maxTie = tie;
            return;
        }

        if (maxTimestamp is null || rowTimestamp > maxTimestamp)
        {
            maxTimestamp = rowTimestamp;
            maxTie = tie;
            return;
        }

        if (rowTimestamp == maxTimestamp && (maxTie is null || string.CompareOrdinal(tie, maxTie) > 0))
            maxTie = tie;
    }
}
