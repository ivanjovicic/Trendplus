using Application.Inventory.Models;
using Xunit;

namespace Api.Tests;

public sealed class InventoryActionSourceKeyTests
{
    [Fact]
    public void Build_UsesCanonicalSchemaAndEscapesDatasetContext()
    {
        var key = InventoryActionSourceKey.Build(
            actionKind: "Dopuna",
            articleId: 501,
            storeId: 12,
            sizeCode: "42/3",
            dataScope: "all",
            periodFrom: "2026-09-01T00:00:00.000Z",
            periodTo: "2026-10-01T00:00:00.000Z",
            snapshotGeneration: "snapshot-17");

        Assert.Equal(
            "inventory|v2|kind=dopuna|article=501|store=12|size=42%2F3|fromStore=all|toStore=all|scope=all|periodFrom=2026-09-01t00%3A00%3A00.000z|periodTo=2026-10-01t00%3A00%3A00.000z|snapshot=snapshot-17",
            key);
    }

    [Fact]
    public void Build_ChangesIdentityWhenDatasetContextChanges()
    {
        static string Build(string scope, string period, string snapshot)
            => InventoryActionSourceKey.Build("dopuna", 501, 12, "42", scope, period, period, snapshot);

        var baseline = Build("all", "rolling-30d", "snapshot-17");

        Assert.NotEqual(baseline, Build("existing", "rolling-30d", "snapshot-17"));
        Assert.NotEqual(baseline, Build("all", "rolling-14d", "snapshot-17"));
        Assert.NotEqual(baseline, Build("all", "rolling-30d", "snapshot-18"));
    }

    [Fact]
    public void Build_MarksUnavailableSnapshotAsUnknown()
    {
        var key = InventoryActionSourceKey.Build("dopuna", 501, 12, null, "all", "rolling-30d", "rolling-30d", null);

        Assert.Contains("snapshot=unknown", key, StringComparison.Ordinal);
    }

    [Fact]
    public void IsV2_LeavesLegacyKeysExplicitlyOutsideCanonicalLookup()
    {
        Assert.True(InventoryActionSourceKey.IsV2("inventory|v2|kind=dopuna|article=501"));
        Assert.False(InventoryActionSourceKey.IsV2("dopuna|SKU-501|12|0"));
        Assert.False(InventoryActionSourceKey.IsV2("inventory:signal_check:501:12"));
    }
}
