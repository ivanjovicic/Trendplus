using Application.Analytics;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class SupplierSharePolicyTests
{
    [Fact]
    public void MixedPositiveAndNegativeRowsUsePositiveNetRevenueDenominator()
    {
        var denominator = SupplierSharePolicy.ResolveDenominator([125m, -25m, 0m]);

        var positive = SupplierSharePolicy.Resolve(125m, denominator);
        var negative = SupplierSharePolicy.Resolve(-25m, denominator);

        Assert.Equal(125m, denominator);
        Assert.Equal(100d, positive.SharePct);
        Assert.True(positive.IsAvailable);
        Assert.Null(negative.SharePct);
        Assert.Equal(SupplierSharePolicy.NonPositiveSupplierState, negative.State);
        Assert.False(negative.IsAvailable);
    }

    [Fact]
    public void ReturnsOnlyPopulationHasExplicitUnavailableShareState()
    {
        var denominator = SupplierSharePolicy.ResolveDenominator([-40m, 0m]);
        var evidence = SupplierSharePolicy.Resolve(-40m, denominator);

        Assert.Equal(0m, denominator);
        Assert.Null(evidence.SharePct);
        Assert.Equal(SupplierSharePolicy.NonPositiveDenominatorState, evidence.State);
        Assert.False(evidence.IsAvailable);
    }
}
