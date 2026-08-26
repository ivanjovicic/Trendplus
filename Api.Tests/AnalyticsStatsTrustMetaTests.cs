using Trendplus2.Endpoints;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class AnalyticsStatsTrustMetaTests
{
    [Fact(DisplayName = "Stats trust meta → empty rows become explicit insufficient_data success")]
    public void BuildStatsTrustMeta_EmptyRows_ReturnsExplicitEmptySuccess()
    {
        var generatedAt = new DateTime(2026, 8, 26, 10, 15, 0, DateTimeKind.Utc);

        var meta = AllEndpoints.BuildStatsTrustMeta(
            rowCount: 0,
            emptyReason: "no_supplier_sales",
            emptyMessage: "Nema podataka za prodaju po dobavljaču.",
            missingCostRevenueSharePct: null,
            unknownRevenueSharePct: null,
            comparableSplitCoveragePct: null,
            generatedAtUtc: generatedAt);

        Assert.True(meta.Success);
        Assert.Equal("no_supplier_sales", meta.EmptyReason);
        Assert.Equal("Nema podataka za prodaju po dobavljaču.", meta.Message);
        Assert.Equal("insufficient_data", meta.DataQualityStatus);
        Assert.False(meta.IsPartial);
        Assert.Equal(generatedAt, meta.LastRefreshAtUtc);
        Assert.Equal(generatedAt, meta.GeneratedAtUtc);
    }

    [Fact(DisplayName = "Stats trust meta → degraded coverage becomes partial warning")]
    public void BuildStatsTrustMeta_DegradedCoverage_ReturnsWarning()
    {
        var generatedAt = new DateTime(2026, 8, 26, 10, 20, 0, DateTimeKind.Utc);

        var meta = AllEndpoints.BuildStatsTrustMeta(
            rowCount: 12,
            emptyReason: "no_supplier_sales",
            emptyMessage: "Nema podataka za prodaju po dobavljaču.",
            missingCostRevenueSharePct: 14,
            unknownRevenueSharePct: 4,
            comparableSplitCoveragePct: 55,
            generatedAtUtc: generatedAt);

        Assert.True(meta.Success);
        Assert.True(meta.IsPartial);
        Assert.Equal("STATS_TRUST_DEGRADED", meta.WarningCode);
        Assert.Equal("warning", meta.DataQualityStatus);
        Assert.Equal(generatedAt, meta.LastRefreshAtUtc);
        Assert.Equal(generatedAt, meta.GeneratedAtUtc);
    }

    [Fact(DisplayName = "Stats trust meta → severe coverage loss becomes critical warning")]
    public void BuildStatsTrustMeta_CriticalCoverage_ReturnsCriticalWarning()
    {
        var generatedAt = new DateTime(2026, 8, 26, 10, 25, 0, DateTimeKind.Utc);

        var meta = AllEndpoints.BuildStatsTrustMeta(
            rowCount: 18,
            emptyReason: "no_color_sales",
            emptyMessage: "Nema podataka za prodaju po boji artikla.",
            missingCostRevenueSharePct: 58,
            unknownRevenueSharePct: 23,
            comparableSplitCoveragePct: 32,
            generatedAtUtc: generatedAt);

        Assert.True(meta.Success);
        Assert.True(meta.IsPartial);
        Assert.Equal("STATS_TRUST_CRITICAL", meta.WarningCode);
        Assert.Equal("critical", meta.DataQualityStatus);
        Assert.Equal(generatedAt, meta.LastRefreshAtUtc);
        Assert.Equal(generatedAt, meta.GeneratedAtUtc);
    }
}
