using Api.Models;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaKpiDefinitionTests
{
    [Fact]
    public void BuildSummary_ExcludesBlockedIncreaseFocusFromRevenueUplift()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(
                artikalId: 1,
                priorityBand: "high",
                status: "increase_focus",
                recommendationAllowed: true,
                stockUnits: 10,
                revenueDelta: 500m,
                marginDelta: 100m,
                hasCompleteEvidence: true),
            CreateCandidate(
                artikalId: 2,
                priorityBand: "high",
                status: "increase_focus",
                recommendationAllowed: false,
                stockUnits: 8,
                revenueDelta: 900m,
                marginDelta: 200m,
                hasCompleteEvidence: true),
            CreateCandidate(
                artikalId: 3,
                priorityBand: "medium",
                status: "review",
                recommendationAllowed: true,
                stockUnits: 4,
                revenueDelta: 700m,
                marginDelta: 150m,
                hasCompleteEvidence: true),
        };

        var summary = PreNivelacijaPriorityEndpoints.BuildSummary(candidates, []);

        Assert.Equal(500m, summary.ExpectedHighlightRevenueUplift);
        Assert.Equal(1, summary.ExpectedHighlightRevenueUpliftCoverageEligible);
        Assert.Equal(3, summary.ExpectedHighlightRevenueUpliftCoverageTotal);
    }

    [Fact]
    public void BuildSummary_ExcludesMissingCostRowsFromAvoidableMarginLoss()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(
                artikalId: 1,
                priorityBand: "high",
                status: "increase_focus",
                recommendationAllowed: true,
                stockUnits: 10,
                revenueDelta: 500m,
                marginDelta: 120m,
                hasCompleteEvidence: true),
            CreateCandidate(
                artikalId: 2,
                priorityBand: "medium",
                status: "review",
                recommendationAllowed: true,
                stockUnits: 5,
                revenueDelta: 400m,
                marginDelta: 999m,
                hasCompleteEvidence: false),
        };

        var summary = PreNivelacijaPriorityEndpoints.BuildSummary(candidates, []);

        Assert.Equal(120m, summary.EstimatedAvoidableMarkdownLoss);
        Assert.Equal(1, summary.EstimatedAvoidableMarkdownLossCoverageEligible);
        Assert.Equal(2, summary.EstimatedAvoidableMarkdownLossCoverageTotal);
    }

    [Fact]
    public void BuildSummary_StockAtRisk_MatchesHighPriorityPopulation()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(1, "high", "increase_focus", true, 12, 100m, 50m, true),
            CreateCandidate(2, "high", "insufficient_data", false, 8, 0m, 0m, false),
            CreateCandidate(3, "medium", "review", true, 20, 100m, 50m, true),
        };

        var summary = PreNivelacijaPriorityEndpoints.BuildSummary(candidates, []);

        Assert.Equal(20, summary.TotalStockAtRisk);
        Assert.Equal(2, summary.TotalStockAtRiskCoverageEligible);
        Assert.Equal(3, summary.TotalStockAtRiskCoverageTotal);
    }

    [Fact]
    public void BuildSummary_ReturnsNullWhenNoEligibleRowsExist()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(1, "medium", "review", true, 5, 200m, 80m, false),
            CreateCandidate(2, "low", "maintain", true, 3, -10m, 0m, true),
        };

        var summary = PreNivelacijaPriorityEndpoints.BuildSummary(candidates, []);

        Assert.Null(summary.TotalStockAtRisk);
        Assert.Equal(0, summary.TotalStockAtRiskCoverageEligible);
        Assert.Null(summary.ExpectedHighlightRevenueUplift);
        Assert.Equal(0, summary.ExpectedHighlightRevenueUpliftCoverageEligible);
        Assert.Null(summary.EstimatedAvoidableMarkdownLoss);
        Assert.Equal(0, summary.EstimatedAvoidableMarkdownLossCoverageEligible);
        Assert.Equal(2, summary.TotalStockAtRiskCoverageTotal);
    }

    private static PreNivelacijaSkuCandidateDto CreateCandidate(
        int artikalId,
        string priorityBand,
        string status,
        bool recommendationAllowed,
        int stockUnits,
        decimal revenueDelta,
        decimal marginDelta,
        bool hasCompleteEvidence)
    {
        return new PreNivelacijaSkuCandidateDto
        {
            ArtikalId = artikalId,
            Sku = $"SKU-{artikalId}",
            PriorityBand = priorityBand,
            StockUnits = stockUnits,
            PreNivelacijaScore = 80,
            RevenueDeltaHighlightVsMarkdown = revenueDelta,
            MarginDeltaHighlightVsMarkdown = marginDelta,
            HasCompleteEvidence = hasCompleteEvidence,
            Recommendation = new PreNivelacijaRecommendationDto
            {
                Status = status,
                RecommendationAllowed = recommendationAllowed,
            },
        };
    }
}
