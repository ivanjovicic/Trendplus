using Application.Analytics.Services;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class TrendScoringServiceTests
{
    [Fact]
    public void ComputeMomentum_MissingOrNonFiniteEvidence_IsUnknown()
    {
        Assert.Null(TrendScoringService.ComputeMomentum(null, 10d));
        Assert.Null(TrendScoringService.ComputeMomentum(10d, double.NaN));
        Assert.Null(TrendScoringService.ComputeMomentum(double.PositiveInfinity, 10d));
    }

    [Fact]
    public void ComputeMomentum_TwoValidZeroScores_RemainsAValidZero()
    {
        var result = TrendScoringService.ComputeMomentum(0d, 0d);

        Assert.NotNull(result);
        Assert.Equal(0d, result!.Value);
    }

    [Fact]
    public void ComputeTrendIndex_DistinguishesEmptyFromValidZero()
    {
        Assert.Null(TrendScoringService.ComputeTrendIndex([]));

        var validZero = TrendScoringService.ComputeTrendIndex([0d]);
        Assert.NotNull(validZero);
        Assert.Equal(0d, validZero!.Value);
    }

    [Fact]
    public void ComputeTrendIndex_RejectsNonFiniteOnlyInput()
    {
        Assert.Null(TrendScoringService.ComputeTrendIndex([double.NaN, double.PositiveInfinity]));
    }

    [Fact]
    public void RecommendationQuantity_RejectsNonFiniteSignals_ButKeepsValidZeroDemand()
    {
        Assert.Null(TrendScoringService.ComputeRecommendedOrderQty(double.NaN, 0d, 1d, 0));
        Assert.Null(TrendScoringService.ComputeRecommendedOrderQty(0.5d, double.PositiveInfinity, 1d, 0));
        Assert.Equal(0, TrendScoringService.ComputeRecommendedOrderQty(0.5d, 0d, 0d, 10));
    }

    [Fact]
    public void ComputeExtendedTrendIndex_DoesNotTreatMissingComponentsAsZero()
    {
        var result = TrendScoringService.ComputeExtendedTrendIndex([10d], []);

        Assert.NotNull(result.Index);
        Assert.Null(result.Momentum);
        Assert.Null(result.Social);
    }
}
