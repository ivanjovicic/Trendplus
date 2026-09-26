using Domain.Model;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class ColorNivelacijaScopeTests
{
    [Fact]
    public void ApplyColorNivelacijaEventScope_RequiresExactStoreAndOrigin()
    {
        var events = new[]
        {
            new DnevnikPromena { Id = 1, ArtikalId = 101, IDObjekat = 7, DataOrigin = "access" },
            new DnevnikPromena { Id = 2, ArtikalId = 102, IDObjekat = 7, DataOrigin = "existing" },
            new DnevnikPromena { Id = 3, ArtikalId = 103, IDObjekat = null, DataOrigin = "existing" },
            new DnevnikPromena { Id = 4, ArtikalId = 104, IDObjekat = 8, DataOrigin = "access" },
        }.AsQueryable();

        var importedAtStore = ApplyScope(events, 7, "imported");
        var existingAtStore = ApplyScope(events, 7, "existing");
        var allStores = ApplyScope(events, null, "all");

        Assert.Equal([1], importedAtStore);
        Assert.Equal([2], existingAtStore);
        Assert.Equal([1, 2, 3, 4], allStores);
    }

    private static int[] ApplyScope(
        IQueryable<DnevnikPromena> events,
        int? storeId,
        string scope)
    {
        return AllEndpoints
            .ApplyColorNivelacijaEventScope(events, storeId, scope)
            .Select(item => item.Id)
            .OrderBy(id => id)
            .ToArray();
    }
}
