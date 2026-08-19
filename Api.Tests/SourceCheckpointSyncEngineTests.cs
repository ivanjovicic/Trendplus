using Api.Services.DataSources;
using Xunit;

namespace Api.Tests;

public sealed class SourceCheckpointSyncEngineTests
{
    private readonly SourceCheckpointSyncEngine _engine = new();
    private static readonly DateTime Now = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CrashBeforeDestinationCommit_LeavesCheckpointUnchanged()
    {
        var store = new InMemorySourceSyncStore { CrashMode = SourceSyncCrashMode.BeforeDestinationCommit };
        var request = Batch("conn-a", "map-a", "artikli", [Row("sku-1", "h1")]);

        Assert.Throws<InvalidOperationException>(() => _engine.Apply(store, request, Now));

        Assert.Null(store.GetCheckpoint(request.Identity));
        Assert.Empty(store.Rows);
    }

    [Fact]
    public void CrashAfterDestinationBeforeCheckpoint_IsRecoverableWithoutDuplicates()
    {
        var store = new InMemorySourceSyncStore { CrashMode = SourceSyncCrashMode.AfterDestinationBeforeCheckpoint };
        var identity = new SourceSyncIdentity("conn-a", "map-a", "artikli");
        var first = Batch("conn-a", "map-a", "artikli", [Row("sku-1", "h1")]);

        Assert.Throws<InvalidOperationException>(() => _engine.Apply(store, first, Now));
        Assert.Null(store.GetCheckpoint(identity));
        Assert.Single(store.Rows);

        store.CrashMode = SourceSyncCrashMode.None;
        var retry = Batch("conn-a", "map-a", "artikli", [Row("sku-1", "h1"), Row("sku-2", "h2")], batchId: Guid.NewGuid());
        var result = _engine.Apply(store, retry, Now.AddMinutes(1));

        Assert.True(result.Success);
        Assert.Equal(1, result.Metrics.Skipped);
        Assert.Equal(1, result.Metrics.Inserted);
        Assert.Equal(0, result.Metrics.Updated);
        Assert.Equal(2, store.Rows.Count);
        Assert.NotNull(store.GetCheckpoint(identity));
    }

    [Fact]
    public void TimestampOverlap_PlusExternalKeyDedup_UpdatesInsteadOfDuplicating()
    {
        var store = new InMemorySourceSyncStore();
        var first = Batch("conn-a", "map-a", "artikli", [
            Row("sku-1", "h1", new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc))
        ]);
        Assert.True(_engine.Apply(store, first, Now).Success);

        var overlap = Batch("conn-a", "map-a", "artikli", [
            Row("sku-1", "h2", new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)),
            Row("sku-2", "h3", new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc))
        ], batchId: Guid.NewGuid());
        var result = _engine.Apply(store, overlap, Now.AddMinutes(1));

        Assert.True(result.Success);
        Assert.Equal(1, result.Metrics.Updated);
        Assert.Equal(1, result.Metrics.Inserted);
        Assert.Equal(2, store.Rows.Count);
        Assert.Equal("h2", store.GetApplied(first.Identity, "sku-1")?.PayloadHash);
        Assert.Equal(new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), result.Checkpoint?.CursorTimestampUtc);
    }

    [Fact]
    public void ConnectionAndMappingIdentities_DoNotCollide()
    {
        var store = new InMemorySourceSyncStore();
        Assert.True(_engine.Apply(store, Batch("conn-a", "map-a", "artikli", [Row("sku-1", "ha")]), Now).Success);
        Assert.True(_engine.Apply(store, Batch("conn-b", "map-a", "artikli", [Row("sku-1", "hb")]), Now).Success);
        Assert.True(_engine.Apply(store, Batch("conn-a", "map-b", "artikli", [Row("sku-1", "hc")]), Now).Success);

        Assert.Equal(3, store.Rows.Count);
        Assert.Equal("ha", store.GetApplied(new SourceSyncIdentity("conn-a", "map-a", "artikli"), "sku-1")?.PayloadHash);
        Assert.Equal("hb", store.GetApplied(new SourceSyncIdentity("conn-b", "map-a", "artikli"), "sku-1")?.PayloadHash);
        Assert.Equal("hc", store.GetApplied(new SourceSyncIdentity("conn-a", "map-b", "artikli"), "sku-1")?.PayloadHash);
    }

    [Fact]
    public void SchemaFingerprintDrift_BlocksMapping_AndDoesNotApplyRows()
    {
        var store = new InMemorySourceSyncStore();
        var first = Batch("conn-a", "map-a", "artikli", [Row("sku-1", "h1")], fingerprint: "sha256:aaa");
        Assert.True(_engine.Apply(store, first, Now).Success);

        var drifted = Batch("conn-a", "map-a", "artikli", [Row("sku-2", "h2")], fingerprint: "sha256:bbb", batchId: Guid.NewGuid());
        var result = _engine.Apply(store, drifted, Now.AddMinutes(1));

        Assert.False(result.Success);
        Assert.Equal(SourceCheckpointSyncEngine.SchemaDriftCategory, result.FailureCategory);
        Assert.Single(store.Rows);
        Assert.Null(store.GetApplied(first.Identity, "sku-2"));
        Assert.Equal("sha256:aaa", store.GetCheckpoint(first.Identity)?.SchemaFingerprint);
        Assert.Equal(SourceCheckpointSyncEngine.SchemaDriftCategory, store.GetCheckpoint(first.Identity)?.FailureCategory);
    }

    [Fact]
    public void MissingIdentity_FailsWithoutWrites()
    {
        var store = new InMemorySourceSyncStore();
        var result = _engine.Apply(store, Batch(" ", "map-a", "artikli", [Row("sku-1", "h1")]), Now);

        Assert.False(result.Success);
        Assert.Equal(SourceCheckpointSyncEngine.IdentityRequiredCategory, result.FailureCategory);
        Assert.Null(store.GetCheckpoint(new SourceSyncIdentity("conn-a", "map-a", "artikli")));
        Assert.Empty(store.Rows);
    }

    [Fact]
    public void MappingProfileId_IsStableForEquivalentFieldOrder()
    {
        var first = SourceMappingProfileId.Compute(
            "Conn-A", "Artikli", "dbo.Items", "Sku", "timestamp_then_id",
            [("name", "Naziv"), ("sku", "Sku")]);
        var second = SourceMappingProfileId.Compute(
            "conn-a", "artikli", "dbo.Items", "SKU", "timestamp_then_id",
            [("sku", "Sku"), ("name", "Naziv")]);

        Assert.Equal(32, first.Length);
        Assert.Equal(first, second);
        Assert.NotEqual(
            first,
            SourceMappingProfileId.Compute(
                "conn-a", "artikli", "dbo.Items", "Sku", "timestamp_then_id",
                [("name", "Naziv"), ("sku", "Other")]));
    }

    [Fact]
    public void Metrics_DistinguishReadInsertedUpdatedSkippedRejected()
    {
        var store = new InMemorySourceSyncStore();
        var seed = Batch("conn-a", "map-a", "artikli", [Row("sku-1", "h1"), Row("sku-keep", "same")]);
        Assert.True(_engine.Apply(store, seed, Now).Success);

        var next = Batch("conn-a", "map-a", "artikli", [
            Row("sku-1", "h2"),
            Row("sku-keep", "same"),
            Row("sku-3", "h3"),
            new SourceSyncRow(null, null, null, "hx", Rejected: true, RejectionReason: "key_missing")
        ], batchId: Guid.NewGuid());
        var result = _engine.Apply(store, next, Now.AddMinutes(1));

        Assert.True(result.Success);
        Assert.Equal(new SourceSyncMetrics(4, 1, 1, 1, 1), result.Metrics);
        Assert.Equal(SourceCheckpointSyncEngine.DedicatedTenantScope, result.Checkpoint?.TenantScope);
    }

    private static SourceSyncBatchRequest Batch(
        string connection,
        string mapping,
        string stream,
        IReadOnlyList<SourceSyncRow> rows,
        string fingerprint = "sha256:stable",
        Guid? batchId = null)
        => new(
            new SourceSyncIdentity(connection, mapping, stream),
            "timestamp_then_id",
            fingerprint,
            60,
            batchId ?? Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            rows);

    private static SourceSyncRow Row(string key, string hash, DateTime? timestamp = null)
        => new(key, timestamp, key, hash);
}
