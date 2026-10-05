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
    public void ComputeScoreBreakdown_ExposesCohortRelativeMaxSensitivity()
    {
        var service = new PreNivelacijaScoringService();

        var baselineReference = service.ComputeScoreBreakdown(
            stockUnits: 50,
            velocity180: 0.20m,
            daysSinceLastSale: 30,
            markdownEvents: 0,
            avgMarkdownPct: 0m,
            grossMarginPctEst: 30m,
            seasonRecencyBoost: 10m,
            maxStock: 100,
            maxVelocity: 0.40m);
        var extremeReference = service.ComputeScoreBreakdown(
            stockUnits: 50,
            velocity180: 0.20m,
            daysSinceLastSale: 30,
            markdownEvents: 0,
            avgMarkdownPct: 0m,
            grossMarginPctEst: 30m,
            seasonRecencyBoost: 10m,
            maxStock: 1000,
            maxVelocity: 4m);

        Assert.Equal(50m, baselineReference.StockPressure);
        Assert.Equal(5m, extremeReference.StockPressure);
        Assert.Equal(50m, baselineReference.VelocityRisk);
        Assert.Equal(95m, extremeReference.VelocityRisk);
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

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void SimulateScenarios_WithUnconstrainedStock_KeepsBothScenariosAtOrAboveSmoothedBaseline(int score)
    {
        var service = new PreNivelacijaScoringService();
        var baselineUnits = (int)Math.Round((double)(((180m + 0.05m) / 181m) * 30m), MidpointRounding.AwayFromZero);

        var (highlight, markdown, _) = service.SimulateScenarios(
            stockUnits: 1000,
            units180: 180,
            markdownEvents: 0,
            avgMarkdownPct: 0m,
            sellingPrice: 5200m,
            purchasePrice: 2600m,
            preNivelacijaScore: score);

        Assert.True(highlight.ExpectedUnits30d > baselineUnits);
        Assert.True(markdown.ExpectedUnits30d >= baselineUnits);
    }

    [Fact]
    public void SimulateScenarios_WhenStockCapsBothScenarios_RevenueDeltaIsPriceOnly()
    {
        var service = new PreNivelacijaScoringService();

        var (highlight, markdown, _) = service.SimulateScenarios(
            stockUnits: 10,
            units180: 180,
            markdownEvents: 0,
            avgMarkdownPct: 0m,
            sellingPrice: 5200m,
            purchasePrice: 2600m,
            preNivelacijaScore: 100m);

        Assert.Equal(10, highlight.ExpectedUnits30d);
        Assert.Equal(highlight.ExpectedUnits30d, markdown.ExpectedUnits30d);
        Assert.Equal(
            highlight.ExpectedUnits30d * (highlight.EffectivePrice - markdown.EffectivePrice),
            highlight.ExpectedRevenue30d - markdown.ExpectedRevenue30d);
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

    [Fact]
    public void EvaluateRecommendation_WithMissingEvidence_BlocksActionAndMarksInsufficientData()
    {
        var service = new PreNivelacijaScoringService();

        var result = service.EvaluateRecommendation(new IPreNivelacijaScoringService.RecommendationInput(
            PreNivelacijaScore: 82m,
            RevenueDelta: 120m,
            MinRevenueDelta: -20m,
            MaxRevenueDelta: 180m,
            DaysSinceLastSale: 70,
            PriorityBand: "high",
            Confidence: "High",
            Units180: 40,
            StockUnits: 10,
            HasCompleteEvidence: false));

        Assert.False(result.Recommendation.RecommendationAllowed);
        Assert.Equal("insufficient_data", result.Recommendation.Status);
        Assert.Equal("insufficient_data", result.Recommendation.DataQualityStatus);
        Assert.Equal("Nedovoljno podataka", result.Recommendation.Label);
        Assert.Contains("missing_evidence", result.Recommendation.ReasonCodes);
    }

    [Fact]
    public void EvaluateRecommendation_WithSignedSalesAdjustment_BlocksActionAndKeepsReason()
    {
        var service = new PreNivelacijaScoringService();

        var result = service.EvaluateRecommendation(new IPreNivelacijaScoringService.RecommendationInput(
            PreNivelacijaScore: 82m,
            RevenueDelta: 120m,
            MinRevenueDelta: -20m,
            MaxRevenueDelta: 180m,
            DaysSinceLastSale: 70,
            PriorityBand: "high",
            Confidence: "High",
            Units180: 40,
            StockUnits: 10,
            HasCompleteEvidence: false,
            SalesEvidenceStatus: "signed_adjustment"));

        Assert.False(result.Recommendation.RecommendationAllowed);
        Assert.Equal("insufficient_data", result.Recommendation.Status);
        Assert.Contains("signed_adjustment", result.Recommendation.ReasonCodes);
    }

    [Fact]
    public void SimulateScenarios_WithoutReliableCost_DoesNotInventFullMargin()
    {
        var service = new PreNivelacijaScoringService();

        var (highlight, markdown, confidence) = service.SimulateScenarios(
            stockUnits: 40,
            units180: 12,
            markdownEvents: 2,
            avgMarkdownPct: 15m,
            sellingPrice: 5200m,
            purchasePrice: 0m,
            preNivelacijaScore: 78m,
            hasReliableCost: false);

        Assert.Equal(0m, highlight.ExpectedMargin30d);
        Assert.Equal(0m, markdown.ExpectedMargin30d);
        Assert.Equal("Low", confidence);
        Assert.True(highlight.ExpectedRevenue30d > 0m);
    }

    [Fact]
    public void SimulateScenarios_WithReliableEqualCost_KeepsHighlightAtZeroAndMarkdownBelowCost()
    {
        var service = new PreNivelacijaScoringService();

        var (highlight, markdown, confidence) = service.SimulateScenarios(
            stockUnits: 40,
            units180: 12,
            markdownEvents: 0,
            avgMarkdownPct: 0m,
            sellingPrice: 5200m,
            purchasePrice: 5200m,
            preNivelacijaScore: 78m,
            hasReliableCost: true);

        Assert.Equal(0m, highlight.ExpectedMargin30d);
        Assert.Equal(-832m, markdown.ExpectedMargin30d);
        Assert.Contains(confidence, new[] { "Low", "Medium", "High" });
    }
}
