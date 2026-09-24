using Api.Models;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaPopulationTests
{
    [Fact]
    public void BuildSummary_UsesFullFilteredUniverseAndKeepsInsufficientHighBandRows()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(1, "high", "insufficient_data", 8),
            CreateCandidate(2, "high", "increase_focus", 12),
            CreateCandidate(3, "medium", "review", 4),
        };

        var summary = PreNivelacijaPriorityEndpoints.BuildSummary(
            candidates,
            [new PreNivelacijaSupplierActionDto { SupplierName = "Dobavljač A" }]);

        Assert.Equal(3, summary.CandidatesCount);
        Assert.Equal(2, summary.HighPriorityCount);
        Assert.Equal(20, summary.TotalStockAtRisk);
        Assert.Equal(1, summary.IncreaseFocusCount);
        Assert.Equal(1, summary.ReviewCount);
        Assert.Equal(1, summary.InsufficientDataCount);
    }

    [Fact]
    public void IsHighPriorityCandidate_IsCaseInsensitiveAndIndependentFromActionability()
    {
        Assert.True(PreNivelacijaPriorityEndpoints.IsHighPriorityCandidate(CreateCandidate(1, "HIGH", "insufficient_data", 1)));
        Assert.False(PreNivelacijaPriorityEndpoints.IsHighPriorityCandidate(CreateCandidate(2, "medium", "increase_focus", 1)));
    }

    [Fact]
    public void FilterCandidatesByFocus_AppliesPopulationFilterBeforePaginationSemantics()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(1, "high", "insufficient_data", 8),
            CreateCandidate(2, "medium", "review", 12),
            CreateCandidate(3, "high", "increase_focus", 4),
        };

        var reviewOnly = PreNivelacijaPriorityEndpoints.FilterCandidatesByFocus(candidates, "review");
        var highPriorityOnly = PreNivelacijaPriorityEndpoints.FilterCandidatesByFocus(candidates, "highPriority");

        Assert.Single(reviewOnly);
        Assert.Equal(2, reviewOnly[0].ArtikalId);
        Assert.Equal(2, highPriorityOnly.Count);
        Assert.Contains(highPriorityOnly, candidate => candidate.ArtikalId == 1);
        Assert.Contains(highPriorityOnly, candidate => candidate.ArtikalId == 3);
    }

    private static PreNivelacijaSkuCandidateDto CreateCandidate(
        int artikalId,
        string priorityBand,
        string status,
        int stockUnits)
    {
        return new PreNivelacijaSkuCandidateDto
        {
            ArtikalId = artikalId,
            Sku = $"SKU-{artikalId}",
            PriorityBand = priorityBand,
            StockUnits = stockUnits,
            PreNivelacijaScore = 80,
            MarginDeltaHighlightVsMarkdown = 100,
            RevenueDeltaHighlightVsMarkdown = 200,
            Recommendation = new PreNivelacijaRecommendationDto { Status = status }
        };
    }
}
