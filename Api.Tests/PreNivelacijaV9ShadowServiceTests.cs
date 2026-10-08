using Api.Models;
using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaV9ShadowServiceTests
{
    [Fact]
    public void BuildEvidence_UsesReceiptAndSeasonFacts_AndKeepsSellThroughUnavailable()
    {
        var anchor = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = PreNivelacijaV9ShadowService.BuildEvidence(
            stockUnits: 14,
            units180: 180,
            daysSinceReceipt: 90,
            receiptEvidenceStatus: "received",
            seasonEndUtc: anchor.AddDays(14),
            anchorDateUtc: anchor);

        Assert.Equal(2m, result.WeeksOfCover);
        Assert.Equal(2m, result.WeeksToSeasonEnd);
        Assert.Equal(0m, result.CoverGap);
        Assert.Equal(90, result.AgeDays);
        Assert.Equal("first_receipt", result.AgeBasis);
        Assert.Equal(15m, result.Score);
        Assert.Null(result.SellThroughSinceReceipt);
        Assert.Equal("opening_stock_and_inbound_history_unavailable", result.SellThroughUnavailableReason);
        Assert.Contains("sell_through_denominator_unavailable", result.ReasonCodes);
    }

    [Fact]
    public void BuildEvidence_DoesNotInventVelocitySeasonAgeOrScore_WhenEvidenceIsMissing()
    {
        var anchor = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = PreNivelacijaV9ShadowService.BuildEvidence(
            stockUnits: 5,
            units180: 0,
            daysSinceReceipt: null,
            receiptEvidenceStatus: "unknown",
            seasonEndUtc: null,
            anchorDateUtc: anchor);

        Assert.Null(result.WeeksOfCover);
        Assert.Null(result.WeeksToSeasonEnd);
        Assert.Null(result.CoverGap);
        Assert.Null(result.AgeDays);
        Assert.Equal("unavailable", result.AgeBasis);
        Assert.Null(result.Score);
        Assert.Contains("positive_net_velocity_unavailable", result.ReasonCodes);
        Assert.Contains("season_end_unavailable", result.ReasonCodes);
        Assert.Contains("shadow_score_unavailable", result.ReasonCodes);
    }

    [Fact]
    public void BuildEvidence_LabelsFirstPositiveSaleAsAgeFallback()
    {
        var result = PreNivelacijaV9ShadowService.BuildEvidence(
            stockUnits: 2,
            units180: 10,
            daysSinceReceipt: 45,
            receiptEvidenceStatus: "first_sale_fallback",
            seasonEndUtc: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            anchorDateUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("first_positive_sale_fallback", result.AgeBasis);
        Assert.Equal(45, result.AgeDays);
    }

    [Fact]
    public void AssignRanks_ProducesDeterministicShadowComparison_WithoutChangingCanonicalScore()
    {
        var candidates = new[]
        {
            Candidate(1, "A", canonicalScore: 90m, shadowScore: 10m),
            Candidate(2, "B", canonicalScore: 80m, shadowScore: 50m),
            Candidate(3, "C", canonicalScore: 70m, shadowScore: 90m)
        };

        PreNivelacijaV9ShadowService.AssignRanks(candidates);
        var comparison = PreNivelacijaV9ShadowService.BuildComparison(candidates);

        Assert.Equal(90m, candidates[0].PreNivelacijaScore);
        Assert.Equal(1, candidates[0].CanonicalRank);
        Assert.Equal(3, candidates[0].ShadowV9.Rank);
        Assert.Equal(-1m, comparison.SpearmanRankCorrelation);
        Assert.Equal(3, comparison.RowsCompared);
        Assert.Equal(3, comparison.TopNOverlapCount);
        Assert.Equal(2, comparison.RowsWithRankMovement);
        Assert.Equal("pre_nivelacija_v9_shadow_v1", comparison.Version);
        Assert.Equal(PreNivelacijaV9ShadowService.CoverGapWeight, comparison.CoverGapWeight);
    }

    private static PreNivelacijaSkuCandidateDto Candidate(int id, string sku, decimal canonicalScore, decimal shadowScore) => new()
    {
        ArtikalId = id,
        StoreId = 7,
        Sku = sku,
        PreNivelacijaScore = canonicalScore,
        ShadowV9 = new PreNivelacijaShadowV9EvidenceDto { Score = shadowScore }
    };
}
