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
    public void SummarizeBaselineEvidence_PreservesTrueZeroAndDistinctUnknownReasons()
    {
        var valid = VendorSalesNivelacijaPriceChangeEffectPolicy.SummarizeBaselineEvidence([
            (true, (string?)null),
            (true, (string?)null)
        ]);
        var noBaseline = VendorSalesNivelacijaPriceChangeEffectPolicy.SummarizeBaselineEvidence([
            (false, (string?)"no_pre_revenue_baseline_uplift"),
            (false, (string?)"no_pre_revenue_baseline_uplift")
        ]);
        var mixed = VendorSalesNivelacijaPriceChangeEffectPolicy.SummarizeBaselineEvidence([
            (true, (string?)null),
            (false, (string?)"no_pre_revenue_baseline_uplift")
        ]);

        Assert.Equal(new(false, "missing_comparable_rows"), VendorSalesNivelacijaPriceChangeEffectPolicy.SummarizeBaselineEvidence([]));
        Assert.Equal(new(true, null), valid);
        Assert.Equal(new(false, "no_pre_revenue_baseline_uplift"), noBaseline);
        Assert.Equal(new(false, "mixed_baseline_evidence"), mixed);
    }

    [Fact]
    public void ComputeCohortChangePercent_IsUnknownWithoutMatureComparableRows()
    {
        Assert.Null(VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeCohortChangePercent(0, 0m, 0m));
        Assert.Null(VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeCohortChangePercent(0, 100m, 120m));
    }

    [Fact]
    public void ComputeCohortChangePercent_KeepsSemanticRulesForMatureCohorts()
    {
        Assert.Equal(20m, VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeCohortChangePercent(3, 100m, 120m));
        Assert.Null(VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeCohortChangePercent(2, 0m, 120m));
        Assert.Equal(0m, VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeCohortChangePercent(1, 0m, 0m));
    }

    [Fact]
    public void ComputeAveragePriceChangePercent_KeepsUnknownDistinctFromMeasuredZero()
    {
        Assert.Null(VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeAveragePriceChangePercent([]));
        Assert.Null(VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeAveragePriceChangePercent([null, null]));
        Assert.Equal(0m, VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeAveragePriceChangePercent([0m, null]));
        Assert.Equal(10m, VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeAveragePriceChangePercent([20m, null, 0m]));
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
