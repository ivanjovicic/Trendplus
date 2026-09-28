using Infrastructure.Services;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class DataScopeConsistencyContractTests
{
    [Fact]
    public void TopOffendersSql_ScopesSales30dBySaleHeader_AndArticlesByDataOrigin()
    {
        var sql = AnalyticsDataQualityHealthService.TopOffendersSql;

        Assert.Contains("WITH sales_30d AS", sql, StringComparison.Ordinal);
        Assert.Contains("FROM prodaja_stavke ps", sql, StringComparison.Ordinal);
        Assert.Contains("JOIN prodaja_zaglavlje p ON p.id = ps.id_prodaja", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE p.datum_prodaje >= @salesFromUtc", sql, StringComparison.Ordinal);
        Assert.Contains("p.datum_prodaje < @salesToExclusiveUtc", sql, StringComparison.Ordinal);

        var salesCteEnd = sql.IndexOf("quality_source AS", StringComparison.Ordinal);
        Assert.True(salesCteEnd > 0);
        var salesCte = sql[..salesCteEnd];

        // RQ06/RQ91/RQ165: sales revenue impact follows sale-header data_origin (EF snake_case mapping).
        Assert.Contains("p.data_origin = 'access'", salesCte, StringComparison.Ordinal);
        Assert.DoesNotContain("p.\"DataOrigin\"", salesCte, StringComparison.Ordinal);
        Assert.Contains("@dataScope = 'imported'", salesCte, StringComparison.Ordinal);
        Assert.Contains("@dataScope = 'existing'", salesCte, StringComparison.Ordinal);

        // Article membership still uses article "DataOrigin".
        Assert.Contains("a.\"DataOrigin\" = 'access'", sql, StringComparison.Ordinal);
        Assert.Contains("@dataScope = 'imported'", sql, StringComparison.Ordinal);
        Assert.Contains("@dataScope = 'existing'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CaptureAsync_NoSales_StillReportsZeroRevenue_NotFakeShares()
    {
        // Revenue shares follow sale-header origin (RQ165); zero revenue must not invent percentages.
        Assert.True(typeof(AnalyticsDataQualityHealthSnapshot).GetProperty(nameof(AnalyticsDataQualityHealthSnapshot.HasRevenueEvidence)) is not null);
        Assert.True(typeof(AnalyticsDataQualityHealthSnapshot).GetProperty(nameof(AnalyticsDataQualityHealthSnapshot.TotalRevenue)) is not null);
    }

    [Fact]
    public void SupplierShoeTypeOracleScopesSalesByHeaderOrigin()
    {
        var source = ReadRepoFile("Api.Tests/Analytics/SupplierShoeTypeRawFactOracle.cs");

        Assert.Contains("pz.data_origin = 'access'", source, StringComparison.Ordinal);
        Assert.Contains("pz.data_origin = 'existing'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("a.\"DataOrigin\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("INNER JOIN \"Artikli\" a", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DailySalesUsesCanonicalHeaderDataScopePolicy()
    {
        var source = ReadRepoFile("Api/Services/DailySalesStatsService.cs");

        Assert.Contains("SalesDataScopePolicy.HeaderPredicate(normalizedScope)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("a.DataOrigin == \"access\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("a.DataOrigin == \"existing\"", source, StringComparison.Ordinal);
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
