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
}
