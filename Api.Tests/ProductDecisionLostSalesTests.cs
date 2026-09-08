using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class ProductDecisionLostSalesTests
{
    [Fact]
    public void CalculateLostSalesEstimate_FastMoverRanksAboveSlowMoverForSameGap()
    {
        var slowMover = CachedAnalyticsEndpoints.CalculateLostSalesEstimate(
            stockGap: 5,
            minimumStock: 10,
            velocityUnitsPerDay: 1m,
            averageUnitPrice: 100m,
            impactWindowDays: 14);
        var fastMover = CachedAnalyticsEndpoints.CalculateLostSalesEstimate(
            stockGap: 5,
            minimumStock: 10,
            velocityUnitsPerDay: 2m,
            averageUnitPrice: 100m,
            impactWindowDays: 14);

        Assert.Equal(700m, slowMover);
        Assert.Equal(1_400m, fastMover);
        Assert.True(fastMover > slowMover);
    }

    [Fact]
    public void CalculateLostSalesEstimate_UsesShortfallRatioAndImpactWindow()
    {
        var result = CachedAnalyticsEndpoints.CalculateLostSalesEstimate(
            stockGap: 2,
            minimumStock: 4,
            velocityUnitsPerDay: 3m,
            averageUnitPrice: 50m,
            impactWindowDays: 14);

        Assert.Equal(1_050m, result);
    }

    [Fact]
    public void CalculateLostSalesEstimate_ReturnsZeroWithoutDemandOrStockGapEvidence()
    {
        Assert.Equal(0m, CachedAnalyticsEndpoints.CalculateLostSalesEstimate(0, 10, 2m, 100m, 14));
        Assert.Equal(0m, CachedAnalyticsEndpoints.CalculateLostSalesEstimate(5, 10, 0m, 100m, 14));
        Assert.Equal(0m, CachedAnalyticsEndpoints.CalculateLostSalesEstimate(5, 10, 2m, 0m, 14));
        Assert.Equal(0m, CachedAnalyticsEndpoints.CalculateLostSalesEstimate(5, 0, 2m, 100m, 14));
    }
}
