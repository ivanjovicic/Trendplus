using Application.Analytics;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class ColorIdentityPolicyTests
{
    [Fact]
    public void KeyMergesCasingWhitespaceAndEquivalentUnicodeForms()
    {
        Assert.Equal(ColorIdentityPolicy.Key(" Crna "), ColorIdentityPolicy.Key("crna"));
        Assert.Equal(ColorIdentityPolicy.Key("Bež"), ColorIdentityPolicy.Key("Bež"));
        Assert.Equal("Bež", ColorIdentityPolicy.DisplayName(" Bež "));
    }

    [Fact]
    public void UnknownValuesUseOneDisplayAndComparisonBucket()
    {
        Assert.Equal(ColorIdentityPolicy.UnknownDisplayName, ColorIdentityPolicy.DisplayName(null));
        Assert.Equal(ColorIdentityPolicy.UnknownDisplayName, ColorIdentityPolicy.DisplayName("  "));
        Assert.Equal(ColorIdentityPolicy.UnknownDisplayName, ColorIdentityPolicy.DisplayName("nepoznato"));
        Assert.True(ColorIdentityPolicy.IsUnknown("NEPOZNATO"));
    }

    [Fact]
    public void InternalSpacingRemainsARealIdentityDifference()
    {
        Assert.NotEqual(ColorIdentityPolicy.Key("Svetlo plava"), ColorIdentityPolicy.Key("Svetlo  plava"));
    }
}
