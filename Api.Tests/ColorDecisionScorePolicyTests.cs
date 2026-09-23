using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class ColorDecisionScorePolicyTests
{
    [Fact]
    public void ResolveUsesEvidenceWeightedScoreInsteadOfCopyingConfidence()
    {
        var score = ColorDecisionScorePolicy.Resolve(
            confidencePct: 88d,
            reliabilityPct: 82d,
            evidenceCoveragePct: 75d,
            recommendationAllowed: true);

        Assert.Equal(84.25d, score);
        Assert.NotEqual(88d, score);
    }

    [Fact]
    public void ResolveBlocksWhenRecommendationIsNotActionable()
    {
        Assert.Null(ColorDecisionScorePolicy.Resolve(88d, 82d, 75d, recommendationAllowed: false));
        Assert.Null(ColorDecisionScorePolicy.Resolve(0d, 0d, 0d, recommendationAllowed: false));
    }

    [Theory]
    [InlineData(null, 82d, 75d)]
    [InlineData(88d, null, 75d)]
    [InlineData(88d, 82d, null)]
    [InlineData(-1d, 82d, 75d)]
    [InlineData(88d, 101d, 75d)]
    [InlineData(double.NaN, 82d, 75d)]
    [InlineData(88d, double.PositiveInfinity, 75d)]
    public void ResolveDoesNotReturnAValueForMissingOrInvalidEvidence(
        double? confidencePct,
        double? reliabilityPct,
        double? evidenceCoveragePct)
    {
        Assert.Null(ColorDecisionScorePolicy.Resolve(
            confidencePct,
            reliabilityPct,
            evidenceCoveragePct,
            recommendationAllowed: true));
    }
}
