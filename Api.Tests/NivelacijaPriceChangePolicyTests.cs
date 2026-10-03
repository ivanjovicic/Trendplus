using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class NivelacijaPriceChangePolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveNewPrices(decimal newPrice)
    {
        Assert.Equal(
            "nivelacija_new_price_must_be_positive",
            NivelacijaPriceChangePolicy.Validate(100m, newPrice, 35m, overrideMaximumMarkdown: false));
    }

    [Fact]
    public void RejectsUnchangedPrice()
    {
        Assert.Equal(
            "nivelacija_price_unchanged",
            NivelacijaPriceChangePolicy.Validate(100m, 100m, 35m, overrideMaximumMarkdown: false));
    }

    [Fact]
    public void EnforcesMaximumMarkdownUnlessExplicitlyOverridden()
    {
        Assert.Equal(40m, NivelacijaPriceChangePolicy.CalculateMarkdownPercent(100m, 60m));
        Assert.Equal(
            "nivelacija_markdown_limit_exceeded",
            NivelacijaPriceChangePolicy.Validate(100m, 60m, 35m, overrideMaximumMarkdown: false));
        Assert.Null(NivelacijaPriceChangePolicy.Validate(100m, 60m, 35m, overrideMaximumMarkdown: true));
    }

    [Fact]
    public void PriceIncreaseDoesNotCountAsMarkdown()
    {
        Assert.Null(NivelacijaPriceChangePolicy.CalculateMarkdownPercent(100m, 120m));
        Assert.Null(NivelacijaPriceChangePolicy.Validate(100m, 120m, 35m, overrideMaximumMarkdown: false));
    }
}
