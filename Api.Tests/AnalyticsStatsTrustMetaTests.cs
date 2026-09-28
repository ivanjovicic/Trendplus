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
        Assert.Null(meta.LastRefreshAtUtc);
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
        Assert.Null(meta.LastRefreshAtUtc);
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
        Assert.Null(meta.LastRefreshAtUtc);
        Assert.Equal(generatedAt, meta.GeneratedAtUtc);
    }

    [Theory(DisplayName = "Supplier stats trust meta applies the 15/25 percent unknown-revenue gate")]
    [InlineData(14.9, "good", null, true)]
    [InlineData(15.0, "warning", "SUPPLIER_UNKNOWN_SHARE_WARNING", true)]
    [InlineData(24.9, "warning", "SUPPLIER_UNKNOWN_SHARE_WARNING", true)]
    [InlineData(25.0, "critical", "SUPPLIER_UNKNOWN_SHARE_CRITICAL", false)]
    public void BuildStatsTrustMeta_SupplierUnknownRevenueBoundaries(
        double unknownRevenueSharePct,
        string expectedQuality,
        string? expectedWarningCode,
        bool expectedRecommendationAllowed)
    {
        var meta = AllEndpoints.BuildStatsTrustMeta(
            rowCount: 12,
            emptyReason: "no_supplier_sales",
            emptyMessage: "Nema podataka za prodaju po dobavljaču.",
            missingCostRevenueSharePct: 0,
            unknownRevenueSharePct: unknownRevenueSharePct,
            comparableSplitCoveragePct: 100,
            generatedAtUtc: DateTime.UtcNow,
            supplierPolicy: true);

        Assert.Equal(expectedQuality, meta.DataQualityStatus);
        Assert.Equal(expectedWarningCode, meta.WarningCode);
        Assert.Equal(expectedRecommendationAllowed, meta.RecommendationAllowed);
    }

    [Fact(DisplayName = "Supplier split coverage alone is not a critical page gate")]
    public void BuildStatsTrustMeta_SupplierMissingSplitIsWarningOnly()
    {
        var meta = AllEndpoints.BuildStatsTrustMeta(
            rowCount: 12,
            emptyReason: "no_supplier_sales",
            emptyMessage: "Nema podataka za prodaju po dobavljaču.",
            missingCostRevenueSharePct: 0,
            unknownRevenueSharePct: 0,
            comparableSplitCoveragePct: 32,
            generatedAtUtc: DateTime.UtcNow,
            supplierPolicy: true);

        Assert.Equal("warning", meta.DataQualityStatus);
        Assert.Equal("STATS_TRUST_DEGRADED", meta.WarningCode);
        Assert.True(meta.RecommendationAllowed);
    }

    [Fact(DisplayName = "Supplier missing split denominator is warning-only when trust denominators are known")]
    public void BuildStatsTrustMeta_SupplierMissingSplitDenominatorDoesNotBlock()
    {
        var meta = AllEndpoints.BuildStatsTrustMeta(
            rowCount: 12,
            emptyReason: "no_supplier_sales",
            emptyMessage: "Nema podataka za prodaju po dobavljaču.",
            missingCostRevenueSharePct: 0,
            unknownRevenueSharePct: 0,
            comparableSplitCoveragePct: null,
            generatedAtUtc: DateTime.UtcNow,
            supplierPolicy: true);

        Assert.Equal("warning", meta.DataQualityStatus);
        Assert.Equal("STATS_TRUST_DEGRADED", meta.WarningCode);
        Assert.True(meta.RecommendationAllowed);
    }
}
