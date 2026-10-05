using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class CategoricalDimensionCoveragePolicyTests
{
    [Fact]
    public void ResolveFromRevenueBuckets_returns_not_populated_when_all_unknown()
    {
        var coverage = CategoricalDimensionCoveragePolicy.ResolveFromRevenueBuckets([
            (1000m, true),
            (500m, true),
        ]);

        Assert.Equal(0m, coverage.KnownCoveragePct);
        Assert.Equal(CategoricalDimensionCoverageStates.NotPopulatedInSource, coverage.DimensionCoverageState);
    }

    [Fact]
    public void ResolveFromRevenueBuckets_returns_populated_when_known_share_exists()
    {
        var coverage = CategoricalDimensionCoveragePolicy.ResolveFromRevenueBuckets([
            (750m, false),
            (250m, true),
        ]);

        Assert.Equal(75m, coverage.KnownCoveragePct);
        Assert.Equal(CategoricalDimensionCoverageStates.PopulatedInSource, coverage.DimensionCoverageState);
    }

    [Fact]
    public void ResolveHourDistribution_returns_not_populated_when_hour_zero_dominates()
    {
        var coverage = CategoricalDimensionCoveragePolicy.ResolveHourDistribution([
            (0, 1_000_000m, 5530),
            (12, 10_000m, 20),
        ]);

        Assert.True(coverage.KnownCoveragePct is < 1m);
        Assert.Equal(CategoricalDimensionCoverageStates.NotPopulatedInSource, coverage.DimensionCoverageState);
    }

    [Fact]
    public void ResolveHourDistribution_returns_populated_when_non_zero_hours_exist()
    {
        var coverage = CategoricalDimensionCoveragePolicy.ResolveHourDistribution([
            (0, 100m, 10),
            (14, 900m, 90),
        ]);

        Assert.Equal(90m, coverage.KnownCoveragePct);
        Assert.Equal(CategoricalDimensionCoverageStates.PopulatedInSource, coverage.DimensionCoverageState);
    }
}
