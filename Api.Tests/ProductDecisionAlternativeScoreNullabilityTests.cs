using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class ProductDecisionAlternativeScoreNullabilityTests
{
    [Fact]
    public void MarkdownScore_IgnoresNullTrendAndMargin()
    {
        var unavailable = BaseRow(marginPct: null, trendPct: null);
        var measuredZeroish = BaseRow(marginPct: 0m, trendPct: 0m);
        var measuredPoor = BaseRow(marginPct: 2m, trendPct: -12m);

        var unavailableScore = CachedAnalyticsEndpoints.ResolveMarkdownAlternativeScore(unavailable, "warning", lowConfidencePenalty: 0);
        var zeroScore = CachedAnalyticsEndpoints.ResolveMarkdownAlternativeScore(measuredZeroish, "warning", lowConfidencePenalty: 0);
        var poorScore = CachedAnalyticsEndpoints.ResolveMarkdownAlternativeScore(measuredPoor, "warning", lowConfidencePenalty: 0);

        // Null margin/trend must not receive the poor-margin / declining-trend bonuses
        // that a measured 0% margin or 0% trend would (and declining trend adds more).
        Assert.True(unavailableScore < zeroScore);
        Assert.True(unavailableScore < poorScore);
        Assert.Equal(unavailableScore + 10, zeroScore); // only the margin < 12 bonus from measured 0
        Assert.Equal(unavailableScore + 10 + 16, poorScore); // margin < 12 and trend < -5
    }

    [Fact]
    public void DoNotOrderScore_IgnoresNullTrend()
    {
        var unavailable = BaseRow(marginPct: null, trendPct: null);
        var declining = BaseRow(marginPct: null, trendPct: -1m);

        var unavailableScore = CachedAnalyticsEndpoints.ResolveDoNotOrderAlternativeScore(unavailable, "warning", lowConfidencePenalty: 0);
        var decliningScore = CachedAnalyticsEndpoints.ResolveDoNotOrderAlternativeScore(declining, "warning", lowConfidencePenalty: 0);

        Assert.Equal(unavailableScore + 10, decliningScore);
    }

    [Fact]
    public void AlternativeReasonCodes_DoNotEmitPoorMarginForNullMargin()
    {
        var unavailable = BaseRow(marginPct: null, trendPct: null);
        var poor = BaseRow(marginPct: 2m, trendPct: -12m);

        var unavailableCodes = CachedAnalyticsEndpoints.BuildProductDecisionAlternativeReasonCodes(
            "MARKDOWN", unavailable, Array.Empty<string>());
        var poorCodes = CachedAnalyticsEndpoints.BuildProductDecisionAlternativeReasonCodes(
            "MARKDOWN", poor, Array.Empty<string>());

        Assert.DoesNotContain("poor_margin", unavailableCodes);
        Assert.Contains("poor_margin", poorCodes);
    }

    private static ProductDecisionCenterRowDto BaseRow(decimal? marginPct, decimal? trendPct) => new()
    {
        ProductId = 501,
        Sku = "SKU-501",
        ProductName = "Probe",
        SupplierId = 9,
        SupplierName = "Supplier Z",
        Revenue = 8000m,
        UnitsSold = 4,
        VelocityUnitsPerDay = 0.1m,
        MarginPct = marginPct,
        CurrentStock = 40,
        MinStock = 5,
        DaysSinceLastSale = 60,
        TrendPct = trendPct,
        SlowStockCapital = 12000m,
        RecommendationStatus = "WATCH",
        RecommendationLabel = "Prati",
        RecommendationReason = "probe",
        ReasonCodes = ["insufficient_history"],
        DataQualityStatus = "warning",
        RecommendationAllowed = false,
        ConfidencePct = 42,
        ReliabilityPct = 40,
    };
}
