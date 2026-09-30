using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class ProductDecisionCenterSummaryDenominatorTests
{
    [Fact]
    public void BuildProductDecisionCenterSummary_UsesReturnedRowsForCounts_AndAnalyzedRowsForMoney()
    {
        var returnedRows = new[]
        {
            new ProductDecisionCenterRowDto
            {
                ProductId = 1,
                RecommendationStatus = "FIX_DATA",
                LostSalesEstimate = 0m,
                SlowStockCapital = 0m
            }
        };

        var summary = CachedAnalyticsEndpoints.BuildProductDecisionCenterSummary(
            returnedRows,
            analyzedLostSalesEstimate: 12_500.456m,
            analyzedSlowStockCapital: 8_000.1m);

        Assert.Equal(0, summary.ReplenishCount);
        Assert.Equal(0, summary.MarkdownCount);
        Assert.Equal(0, summary.HighPotentialCount);
        Assert.Equal(1, summary.BadDataCount);
        Assert.Equal(12_500.46m, summary.LostSalesEstimate);
        Assert.Equal(8_000.1m, summary.SlowStockCapital);
        Assert.Equal(ProductDecisionDenominatorScope.ReturnedRows, summary.CountDenominatorScope);
        Assert.Equal(ProductDecisionDenominatorScope.AnalyzedRows, summary.MoneyDenominatorScope);
    }

    [Fact]
    public void BuildProductDecisionCenterSummary_DoesNotInventMoneyFromReturnedRowsAlone()
    {
        var returnedRows = new[]
        {
            new ProductDecisionCenterRowDto
            {
                ProductId = 2,
                RecommendationStatus = "REPLENISH",
                LostSalesEstimate = 999m,
                SlowStockCapital = 111m
            }
        };

        var summary = CachedAnalyticsEndpoints.BuildProductDecisionCenterSummary(
            returnedRows,
            analyzedLostSalesEstimate: 0m,
            analyzedSlowStockCapital: 0m);

        Assert.Equal(1, summary.ReplenishCount);
        Assert.Equal(0m, summary.LostSalesEstimate);
        Assert.Equal(0m, summary.SlowStockCapital);
        Assert.Equal(ProductDecisionDenominatorScope.AnalyzedRows, summary.MoneyDenominatorScope);
    }

    [Fact]
    public void BuildProductDecisionCenterRowWindow_TreatsIgnoredAsHiddenByTopLimit()
    {
        var window = CachedAnalyticsEndpoints.BuildProductDecisionCenterRowWindow(
            analyzedRowCount: 5,
            returnedRowCount: 2);

        Assert.Equal(2, window.TotalRows);
        Assert.Equal(5, window.AnalyzedRows);
        Assert.Equal(3, window.IgnoredRowsCount);
        Assert.Equal(ProductDecisionDenominatorScope.HiddenByTopLimit, window.IgnoredRowsMeaning);
    }

    [Fact]
    public void BuildProductDecisionCenterRowWindow_WhenNoTruncation_KeepsZeroIgnoredWithExplicitMeaning()
    {
        var window = CachedAnalyticsEndpoints.BuildProductDecisionCenterRowWindow(
            analyzedRowCount: 3,
            returnedRowCount: 3);

        Assert.Equal(0, window.IgnoredRowsCount);
        Assert.Equal(ProductDecisionDenominatorScope.HiddenByTopLimit, window.IgnoredRowsMeaning);
    }

    [Fact]
    public void BuildProductDecisionCenterSummary_SeparatesActionabilityFromInsufficientCoverage()
    {
        var returnedRows = new[]
        {
            new ProductDecisionCenterRowDto
            {
                ProductId = 1,
                RecommendationStatus = "REPLENISH",
                RecommendationAllowed = true,
                DataQualityStatus = "good",
                StockCoverStatus = "low_cover",
                SellThroughStatus = "good"
            },
            new ProductDecisionCenterRowDto
            {
                ProductId = 2,
                RecommendationStatus = "FIX_DATA",
                RecommendationAllowed = false,
                DataQualityStatus = "insufficient_data",
                StockCoverStatus = "insufficient_data"
            },
            new ProductDecisionCenterRowDto
            {
                ProductId = 3,
                RecommendationStatus = "DO_NOT_ORDER",
                RecommendationAllowed = false,
                DataQualityStatus = "warning",
                StockCoverStatus = "slow_stock"
            },
            new ProductDecisionCenterRowDto
            {
                ProductId = 4,
                RecommendationStatus = "BOOST",
                RecommendationAllowed = true,
                DataQualityStatus = "good",
                StockCoverStatus = "out_of_stock_risk"
            }
        };

        var summary = CachedAnalyticsEndpoints.BuildProductDecisionCenterSummary(
            returnedRows,
            analyzedLostSalesEstimate: 0m,
            analyzedSlowStockCapital: 0m);

        Assert.Equal(2, summary.ActionableCount);
        Assert.Equal(2, summary.BlockedCount);
        Assert.Equal(1, summary.InsufficientEvidenceCount);
        Assert.Equal(1, summary.DoNotOrderCount);
        Assert.Equal(2, summary.StockCoverRiskCount);
        Assert.Equal(1, summary.InsufficientStockCoverageCount);
        Assert.Equal(2, summary.LowCoverCount);
        Assert.Equal(1, summary.SlowStockCount);
        Assert.Equal(1, summary.GoodSellThroughCount);
    }
}
