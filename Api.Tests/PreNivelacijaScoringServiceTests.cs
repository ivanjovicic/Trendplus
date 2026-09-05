using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaScoringServiceTests
{
    [Fact]
    public void ComputePreNivelacijaScore_IsDeterministic_AndInRange()
    {
        var service = new PreNivelacijaScoringService();

        var breakdown = service.ComputeScoreBreakdown(
            stockUnits: 120,
            velocity180: 0.05m,
            daysSinceLastSale: 90,
            markdownEvents: 1,
            avgMarkdownPct: 12m,
            grossMarginPctEst: 38m,
            seasonRecencyBoost: 60m,
            maxStock: 240,
            maxVelocity: 0.40m);

        var score1 = service.ComputePreNivelacijaScore(breakdown);
        var score2 = service.ComputePreNivelacijaScore(breakdown);

        Assert.Equal(score1, score2);
        Assert.InRange(score1, 0m, 100m);
    }

    [Fact]
    public void SimulateScenarios_ProducesPositiveEffectivePrices_AndConfidence()
    {
        var service = new PreNivelacijaScoringService();

        var (highlight, markdown, confidence) = service.SimulateScenarios(
            stockUnits: 40,
            units180: 12,
            markdownEvents: 2,
            avgMarkdownPct: 15m,
            sellingPrice: 5200m,
            purchasePrice: 2600m,
            preNivelacijaScore: 78m);

        Assert.True(highlight.ExpectedUnits30d >= 1);
        Assert.True(markdown.ExpectedUnits30d >= 1);
        Assert.True(highlight.EffectivePrice > 0m);
        Assert.True(markdown.EffectivePrice > 0m);
        Assert.Contains(confidence, new[] { "Low", "Medium", "High" });
    }

    [Fact]
    public void SimulateScenarios_WithNoStock_DoesNotInventOneExpectedUnit()
    {
        var service = new PreNivelacijaScoringService();

        var (highlight, markdown, _) = service.SimulateScenarios(
            stockUnits: 0,
            units180: 180,
            markdownEvents: 0,
            avgMarkdownPct: 0m,
            sellingPrice: 5200m,
            purchasePrice: 2600m,
            preNivelacijaScore: 78m);

        Assert.Equal(0, highlight.ExpectedUnits30d);
        Assert.Equal(0, markdown.ExpectedUnits30d);
        Assert.Equal(0m, highlight.ExpectedRevenue30d);
        Assert.Equal(0m, markdown.ExpectedRevenue30d);
    }
}
