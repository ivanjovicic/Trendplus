using Application.Analytics;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class ProductDecisionReasoningHelperTests
{
    [Fact(DisplayName = "Missing cost -> FIX_DATA + missing_cost")]
    public void MissingCost_MapsToFixDataWithMissingCostCode()
    {
        var result = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: true,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 100_000m,
            UnitsSold: 50,
            VelocityUnitsPerDay: 1.2m,
            MarginPct: 18m,
            MarginCoveragePct: 30m,
            TrendPct: 12m,
            StockGap: 2,
            CurrentStock: 3,
            MinStock: 5,
            DaysSinceLastSale: 2));

        Assert.Equal("FIX_DATA", result.RecommendationStatus);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.MissingCost, result.ReasonCodes);
    }

    [Fact(DisplayName = "Low stock + high velocity -> REPLENISH + low_stock/high_velocity")]
    public void LowStockHighVelocity_MapsToReplenishWithCanonicalCodes()
    {
        var result = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 80_000m,
            UnitsSold: 40,
            VelocityUnitsPerDay: 0.95m,
            MarginPct: 20m,
            MarginCoveragePct: 90m,
            TrendPct: 8m,
            StockGap: 6,
            CurrentStock: 1,
            MinStock: 7,
            DaysSinceLastSale: 1));

        Assert.Equal("REPLENISH", result.RecommendationStatus);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.LowStock, result.ReasonCodes);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.HighVelocity, result.ReasonCodes);
    }

    [Fact(DisplayName = "Stale stock -> MARKDOWN + stale_stock")]
    public void StaleStock_MapsToMarkdownWithStaleStockCode()
    {
        var result = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 30_000m,
            UnitsSold: 5,
            VelocityUnitsPerDay: 0.10m,
            MarginPct: 7m,
            MarginCoveragePct: 85m,
            TrendPct: -20m,
            StockGap: 0,
            CurrentStock: 25,
            MinStock: 5,
            DaysSinceLastSale: 65));

        Assert.Equal("MARKDOWN", result.RecommendationStatus);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.StaleStock, result.ReasonCodes);
    }

    [Fact(DisplayName = "Weak signal -> WATCH or INSUFFICIENT_DATA")]
    public void WeakSignal_MapsToWatchOrInsufficientData()
    {
        var result = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 0m,
            UnitsSold: 1,
            VelocityUnitsPerDay: 0.02m,
            MarginPct: null,
            MarginCoveragePct: 0m,
            TrendPct: null,
            StockGap: 0,
            CurrentStock: 4,
            MinStock: 4,
            DaysSinceLastSale: null));

        Assert.True(result.RecommendationStatus is "WATCH" or "INSUFFICIENT_DATA");
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.InsufficientHistory, result.ReasonCodes);
    }

    [Fact(DisplayName = "Small sales sample -> explicit history blockers")]
    public void SmallSalesSample_ExplainsWhyRecommendationIsBlocked()
    {
        var result = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 12_780m,
            UnitsSold: 2,
            VelocityUnitsPerDay: 0.07m,
            MarginPct: 36.7m,
            MarginCoveragePct: 100m,
            TrendPct: -60m,
            StockGap: 0,
            CurrentStock: 2,
            MinStock: 0,
            DaysSinceLastSale: 4));

        Assert.Equal("INSUFFICIENT_DATA", result.RecommendationStatus);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.InsufficientHistory, result.ReasonCodes);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.LowSampleSize, result.ReasonCodes);
        Assert.DoesNotContain(ProductDecisionReasoningHelper.ReasonCodes.NoSalesInPeriod, result.ReasonCodes);
        Assert.Equal(3, ProductDecisionReasoningHelper.MinimumUnitsForRecommendation);
    }

    [Fact(DisplayName = "Missing or zero previous baseline never becomes +100% trend")]
    public void ComputeTrendPct_MissingOrZeroBaseline_StaysUnavailable()
    {
        Assert.Null(ProductDecisionReasoningHelper.ComputeTrendPct(50_000m, previousRevenue: null));
        Assert.Null(ProductDecisionReasoningHelper.ComputeTrendPct(50_000m, previousRevenue: 0m));
        Assert.Equal(100m, ProductDecisionReasoningHelper.ComputeTrendPct(200m, previousRevenue: 100m));
        Assert.Equal(0m, ProductDecisionReasoningHelper.ComputeTrendPct(100m, previousRevenue: 100m));
        Assert.Equal(-50m, ProductDecisionReasoningHelper.ComputeTrendPct(50m, previousRevenue: 100m));
    }

    [Fact]
    public void RecommendationLabel_MapsFamilyCodesForOperatorFacingScope()
    {
        Assert.Equal("Dopuni", ProductDecisionReasoningHelper.RecommendationLabel("REPLENISH"));
        Assert.Equal("Pojačaj", ProductDecisionReasoningHelper.RecommendationLabel("boost"));
        Assert.Equal("sve porodice", ProductDecisionReasoningHelper.RecommendationLabel(null));
        Assert.Equal("CUSTOM", ProductDecisionReasoningHelper.RecommendationLabel("CUSTOM"));
    }

    // FAILING-FIRST TESTS: Null evidence must not enable recommendations
    
    [Fact(DisplayName = "Null trend should not enable BOOST even with good margin and velocity")]
    public void NullTrend_ShouldNotEnableBoost()
    {
        // FAILING TEST: Currently this passes BOOST because null trend coalesces to 0
        // Expected: INSUFFICIENT_DATA (missing denominator for trend signal)
        var result = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 200_000m,
            UnitsSold: 80,
            VelocityUnitsPerDay: 1.5m,
            MarginPct: 28m,  // good margin
            MarginCoveragePct: 95m,
            TrendPct: null,  // MISSING EVIDENCE
            StockGap: 15,
            CurrentStock: 2,
            MinStock: 5,
            DaysSinceLastSale: 3));

        // Should NOT allow BOOST with missing trend evidence
        Assert.Equal("INSUFFICIENT_DATA", result.RecommendationStatus);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.InsufficientHistory, result.ReasonCodes);
    }

    [Fact(DisplayName = "Null margin should not enable BOOST")]
    public void NullMargin_ShouldNotEnableBoost()
    {
        // FAILING TEST: Currently this might pass REPLENISH because null margin coalesces to 0
        // Expected: Should reject null margin as insufficient evidence
        var result = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 250_000m,
            UnitsSold: 90,
            VelocityUnitsPerDay: 1.8m,
            MarginPct: null,  // MISSING EVIDENCE
            MarginCoveragePct: 95m,
            TrendPct: 15m,  // good trend
            StockGap: 20,
            CurrentStock: 1,
            MinStock: 10,
            DaysSinceLastSale: 2));

        // Should NOT create actionable recommendation with missing margin evidence
        Assert.Equal("INSUFFICIENT_DATA", result.RecommendationStatus);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.InsufficientHistory, result.ReasonCodes);
    }

    [Fact(DisplayName = "Null trend with null margin should definitely not enable BOOST")]
    public void BothNullTrendAndMargin_ShouldNotEnableBOOST()
    {
        // FAILING TEST: Multiple missing evidence should clearly fail closed
        var result = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 300_000m,
            UnitsSold: 100,
            VelocityUnitsPerDay: 2.0m,
            MarginPct: null,     // MISSING
            MarginCoveragePct: 95m,
            TrendPct: null,      // MISSING
            StockGap: 30,
            CurrentStock: 1,
            MinStock: 15,
            DaysSinceLastSale: 1));

        // Should absolutely NOT recommend with both trend and margin missing
        Assert.Equal("INSUFFICIENT_DATA", result.RecommendationStatus);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.InsufficientHistory, result.ReasonCodes);
    }

    [Fact(DisplayName = "Genuine zero trend should be distinct from null trend")]
    public void ZeroTrendVsNullTrend_MustBeDistinct()
    {
        // With genuine zero trend (measured, not missing)
        var withZeroTrend = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 150_000m,
            UnitsSold: 60,
            VelocityUnitsPerDay: 1.2m,
            MarginPct: 20m,
            MarginCoveragePct: 90m,
            TrendPct: 0m,  // Genuine ZERO (measured neutral trend)
            StockGap: 8,
            CurrentStock: 2,
            MinStock: 5,
            DaysSinceLastSale: 5));

        // With null trend (missing evidence)
        var withNullTrend = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
            MissingSupplier: false,
            MissingCost: false,
            MissingCategory: false,
            MissingVariantData: false,
            Revenue: 150_000m,
            UnitsSold: 60,
            VelocityUnitsPerDay: 1.2m,
            MarginPct: 20m,
            MarginCoveragePct: 90m,
            TrendPct: null,  // Missing evidence
            StockGap: 8,
            CurrentStock: 2,
            MinStock: 5,
            DaysSinceLastSale: 5));

        Assert.Equal("REPLENISH", withZeroTrend.RecommendationStatus);
        Assert.Equal("INSUFFICIENT_DATA", withNullTrend.RecommendationStatus);
        Assert.NotEqual(withZeroTrend.RecommendationStatus, withNullTrend.RecommendationStatus);
    }
}

