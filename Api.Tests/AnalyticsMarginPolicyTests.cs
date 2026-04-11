using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsMarginPolicyTests
{
    [Fact]
    public void ResolveUnitCost_PrefersSaleLineCost_WhenReliable()
    {
        var resolved = AnalyticsMarginPolicy.ResolveUnitCost(
            saleLineCost: 820m,
            productCostRsd: 900m,
            productCostLegacy: 950m);

        Assert.Equal(820m, resolved);
    }

    [Fact]
    public void ResolveUnitCost_FallsBackToRsdThenLegacy_AndIgnoresInvalidValues()
    {
        var resolvedFromRsd = AnalyticsMarginPolicy.ResolveUnitCost(
            saleLineCost: 0m,
            productCostRsd: 780m,
            productCostLegacy: 910m);

        var resolvedFromLegacy = AnalyticsMarginPolicy.ResolveUnitCost(
            saleLineCost: null,
            productCostRsd: -15m,
            productCostLegacy: 640m);

        var unresolved = AnalyticsMarginPolicy.ResolveUnitCost(
            saleLineCost: 0m,
            productCostRsd: null,
            productCostLegacy: -1m);

        Assert.Equal(780m, resolvedFromRsd);
        Assert.Equal(640m, resolvedFromLegacy);
        Assert.Null(unresolved);
    }

    [Fact]
    public void ResolveUnitCostWithSource_DistinguishesHistoricalFromFallbackCost()
    {
        var historical = AnalyticsMarginPolicy.ResolveUnitCostWithSource(
            saleLineCost: 820m,
            productCostRsd: 900m,
            productCostLegacy: 950m);
        var fallback = AnalyticsMarginPolicy.ResolveUnitCostWithSource(
            saleLineCost: null,
            productCostRsd: 780m,
            productCostLegacy: 910m);

        Assert.Equal(820m, historical.UnitCost);
        Assert.Equal(MarginCostSource.Historical, historical.Source);
        Assert.Equal(780m, fallback.UnitCost);
        Assert.Equal(MarginCostSource.ProductFallbackRsd, fallback.Source);
    }

    [Fact]
    public void MarginAccumulator_BuildsContributionAndCoverage_ForKnownCostRows()
    {
        var accumulator = new MarginAccumulator();

        accumulator.Add(revenue: 500m, quantity: 2m, unitCost: 200m);
        accumulator.Add(revenue: 300m, quantity: 1m, unitCost: 100m);

        var snapshot = accumulator.Build(totalRevenue: 1_000m);

        Assert.Equal(800m, snapshot.RevenueWithCost);
        Assert.Equal(500m, snapshot.TotalCost);
        Assert.Equal(300m, snapshot.MarginContribution);
        Assert.Equal(37.5d, snapshot.MarginPct);
        Assert.Equal(80d, snapshot.MarginDataCoveragePct);
        Assert.Equal(800m, snapshot.HistoricalCostRevenue);
        Assert.Equal(0m, snapshot.EstimatedCostRevenue);
        Assert.Equal(80d, snapshot.HistoricalMarginCoveragePct);
        Assert.Equal(0d, snapshot.FallbackCostCoveragePct);
    }

    [Fact]
    public void MarginAccumulator_ExcludesUnknownZeroAndNegativeCostRows_FromCoverage()
    {
        var accumulator = new MarginAccumulator();

        accumulator.Add(revenue: 500m, quantity: 2m, unitCost: null);
        accumulator.Add(revenue: 150m, quantity: 1m, unitCost: 0m);
        accumulator.Add(revenue: 125m, quantity: 1m, unitCost: -10m);
        accumulator.Add(revenue: 200m, quantity: 1m, unitCost: 100m);

        var snapshot = accumulator.Build(totalRevenue: 975m);

        Assert.Equal(200m, snapshot.RevenueWithCost);
        Assert.Equal(100m, snapshot.TotalCost);
        Assert.Equal(100m, snapshot.MarginContribution);
        Assert.Equal(50d, snapshot.MarginPct);
        Assert.Equal(20.51d, snapshot.MarginDataCoveragePct);
        Assert.Equal(200m, snapshot.HistoricalCostRevenue);
        Assert.Equal(0m, snapshot.EstimatedCostRevenue);
    }

    [Fact]
    public void MarginAccumulator_HandlesReturnsWithNegativeQuantity()
    {
        var accumulator = new MarginAccumulator();

        accumulator.Add(revenue: 500m, quantity: 2m, unitCost: 200m);
        accumulator.Add(revenue: -250m, quantity: -1m, unitCost: 200m);

        var snapshot = accumulator.Build(totalRevenue: 250m);

        Assert.Equal(250m, snapshot.RevenueWithCost);
        Assert.Equal(200m, snapshot.TotalCost);
        Assert.Equal(50m, snapshot.MarginContribution);
        Assert.Equal(20d, snapshot.MarginPct);
        Assert.Equal(100d, snapshot.MarginDataCoveragePct);
        Assert.Equal(250m, snapshot.HistoricalCostRevenue);
        Assert.Equal(0m, snapshot.EstimatedCostRevenue);
    }

    [Fact]
    public void MarginAccumulator_TracksHistoricalAndFallbackCoverageSeparately()
    {
        var accumulator = new MarginAccumulator();

        accumulator.Add(revenue: 500m, quantity: 2m, saleLineCost: 200m, productCostRsd: 250m, productCostLegacy: 260m);
        accumulator.Add(revenue: 300m, quantity: 1m, saleLineCost: null, productCostRsd: 100m, productCostLegacy: 110m);

        var snapshot = accumulator.Build(totalRevenue: 1_000m);

        Assert.Equal(800m, snapshot.RevenueWithCost);
        Assert.Equal(500m, snapshot.HistoricalCostRevenue);
        Assert.Equal(300m, snapshot.EstimatedCostRevenue);
        Assert.Equal(50d, snapshot.HistoricalMarginCoveragePct);
        Assert.Equal(30d, snapshot.FallbackCostCoveragePct);
    }
}
