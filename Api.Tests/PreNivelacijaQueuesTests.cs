using Api.Models;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaQueuesTests
{
    [Fact]
    public void BuildQueues_ExposesTotalsBeforeTakeCap()
    {
        var candidates = Enumerable.Range(1, 35)
            .Select(index => CreateCandidate(
                artikalId: index,
                priorityBand: "high",
                recommendationAllowed: true,
                daysSinceLastSale: 10))
            .Concat(Enumerable.Range(100, 32)
                .Select(index => CreateCandidate(
                    artikalId: index,
                    priorityBand: "medium",
                    recommendationAllowed: true,
                    daysSinceLastSale: 10)))
            .Concat(Enumerable.Range(200, 31)
                .Select(index => CreateCandidate(
                    artikalId: index,
                    priorityBand: "low",
                    recommendationAllowed: true,
                    daysSinceLastSale: 90)))
            .ToList();

        var queues = PreNivelacijaPriorityEndpoints.BuildQueues(candidates, new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(35, queues.HighlightNowTotal);
        Assert.Equal(30, queues.HighlightNow.Count);
        Assert.Equal(32, queues.MonitorTotal);
        Assert.Equal(30, queues.Monitor.Count);
        Assert.Equal(31, queues.LikelyMarkdownSoonTotal);
        Assert.Equal(30, queues.LikelyMarkdownSoon.Count);
    }

    private static PreNivelacijaSkuCandidateDto CreateCandidate(
        int artikalId,
        string priorityBand,
        bool recommendationAllowed,
        int daysSinceLastSale)
    {
        return new PreNivelacijaSkuCandidateDto
        {
            ArtikalId = artikalId,
            Sku = $"SKU-{artikalId}",
            SupplierName = "Dobavljac",
            PriorityBand = priorityBand,
            DaysSinceLastSale = daysSinceLastSale,
            StockUnits = 5,
            Recommendation = new PreNivelacijaRecommendationDto
            {
                Status = "increase_focus",
                RecommendationAllowed = recommendationAllowed,
            },
        };
    }
}
