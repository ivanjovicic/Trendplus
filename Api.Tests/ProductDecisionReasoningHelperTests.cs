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

    [Fact]
    public void RecommendationLabel_MapsFamilyCodesForOperatorFacingScope()
    {
        Assert.Equal("Dopuni", ProductDecisionReasoningHelper.RecommendationLabel("REPLENISH"));
        Assert.Equal("Pojačaj", ProductDecisionReasoningHelper.RecommendationLabel("boost"));
        Assert.Equal("sve porodice", ProductDecisionReasoningHelper.RecommendationLabel(null));
        Assert.Equal("CUSTOM", ProductDecisionReasoningHelper.RecommendationLabel("CUSTOM"));
    }
}
