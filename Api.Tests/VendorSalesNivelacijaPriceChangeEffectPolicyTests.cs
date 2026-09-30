using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class VendorSalesNivelacijaPriceChangeEffectPolicyTests
{
    [Theory]
    [InlineData(100, 120, 20)]
    [InlineData(100, 80, -20)]
    [InlineData(100, 100, 0)]
    public void ComputeSemanticChangePercent_UsesValidBaseline(decimal pre, decimal post, decimal expected)
    {
        Assert.Equal(expected, VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeSemanticChangePercent(pre, post));
    }

    [Fact]
    public void ComputeSemanticChangePercent_DoesNotFabricatePositiveBaselineUplift()
    {
        Assert.Null(VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeSemanticChangePercent(0m, 120m));
    }

    [Fact]
    public void IsPostWindowMature_RequiresFullThirtyDayWindow()
    {
        var eventDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(VendorSalesNivelacijaPriceChangeEffectPolicy.IsPostWindowMature(eventDate, new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc)));
        Assert.True(VendorSalesNivelacijaPriceChangeEffectPolicy.IsPostWindowMature(eventDate, new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Evaluate_MarksImmatureVendorRowsAsInProgress()
    {
        var result = VendorSalesNivelacijaPriceChangeEffectPolicy.Evaluate(
            new VendorSalesNivelacijaPriceChangeEffectPolicy.VendorAggregateInput(
                IsUnknownVendor: false,
                PreRevenue: 100m,
                PostRevenue: 80m,
                PreQty: 5,
                PostQty: 4,
                ComparableArticleCount: 2,
                MatureComparableArticleCount: 0,
                ImmatureComparableArticleCount: 2,
                SemanticChangePercentRevenue: -20d,
                MarginPct: 35d,
                MarginCoveragePct: 80d,
                SplitCoveragePct: 70d));

        Assert.Equal("immature", result.Status);
        Assert.Equal("Prozor u toku", result.Label);
        Assert.False(result.RecommendationAllowed);
    }

    [Fact]
    public void Evaluate_ClassifiesMatureNegativeEffectAsIneffective()
    {
        var result = VendorSalesNivelacijaPriceChangeEffectPolicy.Evaluate(
            new VendorSalesNivelacijaPriceChangeEffectPolicy.VendorAggregateInput(
                IsUnknownVendor: false,
                PreRevenue: 100m,
                PostRevenue: 80m,
                PreQty: 5,
                PostQty: 4,
                ComparableArticleCount: 2,
                MatureComparableArticleCount: 2,
                ImmatureComparableArticleCount: 0,
                SemanticChangePercentRevenue: -20d,
                MarginPct: 35d,
                MarginCoveragePct: 80d,
                SplitCoveragePct: 70d));

        Assert.Equal("ineffective", result.Status);
        Assert.False(result.RecommendationAllowed);
    }

    [Fact]
    public void ComputeWeightedMarginBenchmarkPct_WeightsByPostRevenue()
    {
        var benchmark = VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeWeightedMarginBenchmarkPct([
            (MarginPct: 10d, MarginCoveragePct: 80d, PostRevenue: 100m),
            (MarginPct: 30d, MarginCoveragePct: 80d, PostRevenue: 300m)
        ]);

        Assert.Equal(25d, benchmark);
    }

    [Fact]
    public void VendorAggregateChangeRevenueMatchesPostMinusPrePopulation()
    {
        var pre = 100m + 50m;
        var post = 80m + 60m;
        var change = post - pre;
        Assert.Equal(-10m, change);
        Assert.Equal(-6.67m, VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeSemanticChangePercent(pre, post));
    }
}
