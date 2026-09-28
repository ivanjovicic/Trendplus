using Xunit;

namespace Api.Tests;

public sealed class AnalyticsSaleLineSnapshotBindingTests
{
    [Fact]
    public void SupplierAndShoeTypeReadsResolveSnapshotsBySaleLineId()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var supplierHub = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");
        var comparison = ReadRepoFile("Api/Services/AnalyticsCostSnapshotService.cs");

        Assert.Contains("ToDictionaryAsync(s => s.ProdajaStavkaId, s => s.ResolvedUnitCost, ct)", source);
        Assert.Contains("ProdajaStavkaId = ps.Id", source);
        Assert.Contains("snapshotCostBySaleLineId.TryGetValue(s.ProdajaStavkaId", source);
        Assert.Contains("snapshotCostBySaleLineId2.TryGetValue(s.ProdajaStavkaId", source);
        Assert.DoesNotContain(".GroupBy(s => s.ArtikalId)", source);
        Assert.Contains("ToDictionaryAsync(snapshot => snapshot.ProdajaStavkaId, snapshot => snapshot.ResolvedUnitCost, ct)", supplierHub);
        Assert.Contains("SaleLineId = saleLine.Id", supplierHub);
        Assert.Contains("snapshotCostBySaleLineId.TryGetValue(row.SaleLineId", supplierHub);
        Assert.DoesNotContain(".GroupBy(snapshot => snapshot.ArtikalId)", supplierHub);
        Assert.Contains("SnapshotCostBySaleLineId", comparison);
        Assert.Contains("snapshotCostBySaleLineId.TryGetValue(saleLineIdSelector(line)", comparison);
        Assert.DoesNotContain(".GroupBy(s => s.ArtikalId)", comparison);
    }

    [Fact]
    public void DetailReadsResolveSnapshotsBySaleLineId()
    {
        var source = ReadRepoFile("Api/Services/AnalyticsDetailReadService.cs");

        Assert.Contains("ProdajaStavkaId = ps.Id", source);
        Assert.Contains("ToDictionaryAsync(s => s.ProdajaStavkaId, s => s.ResolvedUnitCost, ct)", source);
        Assert.Contains("SnapshotCostsBySaleLineId.TryGetValue(row.ProdajaStavkaId", source);
        Assert.DoesNotContain(".GroupBy(s => s.ArtikalId)", source);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
