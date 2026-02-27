using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class ShoeScoringTests
{
    [Fact]
    public void Compute_HigherRatingAndReviews_IncreasesScore()
    {
        var low = ShoeScoring.Compute(rating: 4.0f, reviewCount: 50, price: 80m);
        var high = ShoeScoring.Compute(rating: 4.6f, reviewCount: 500, price: 80m);

        Assert.True(high > low);
    }

    [Fact]
    public void Compute_TrainingSignals_BoostWithinExpectedRange()
    {
        var baseScore = ShoeScoring.Compute(rating: 4.5f, reviewCount: 500, price: 80m, popularityPriorScore: 0m, dealScore: 0m);
        var boosted = ShoeScoring.Compute(rating: 4.5f, reviewCount: 500, price: 80m, popularityPriorScore: 100m, dealScore: 100m);

        Assert.True(boosted > baseScore);
        Assert.InRange((double)boosted / baseScore, 1.0, 1.37);
    }

    [Fact]
    public void Compute_NegativeReviewCount_DoesNotThrow()
    {
        var score = ShoeScoring.Compute(rating: 4.2f, reviewCount: -10, price: 80m);
        Assert.True(score >= 0);
    }
}

