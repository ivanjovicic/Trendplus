using Api.Models;

namespace Api.Services;

/// <summary>
/// Versioned, non-authoritative comparison model. It must never feed the canonical
/// Pre-Nivelacija score, recommendation, queue eligibility or action ordering.
/// </summary>
public static class PreNivelacijaV9ShadowService
{
    public const string Version = "pre_nivelacija_v9_shadow_v1";
    public const decimal CoverGapWeight = 0.70m;
    public const decimal AgeWeight = 0.30m;
    public const decimal CoverGapSaturationWeeks = 12m;
    public const int AgeSaturationDays = 180;
    public const int ComparisonTopN = 10;
    public const string Methodology = "Eksperimentalni skor v9 = normalizovan višak pokrića zalihe u odnosu na kraj sezone (70%) + rizik starosti od prijema (30%); težine se preraspodeljuju na signale koji postoje. Skor je eksperimentalan i ne menja aktivne v10 preporuke ni akcije.";

    public static PreNivelacijaShadowV9EvidenceDto BuildEvidence(
        int stockUnits,
        int units180,
        int? daysSinceReceipt,
        string receiptEvidenceStatus,
        DateTime? seasonEndUtc,
        DateTime anchorDateUtc)
    {
        var reasons = new List<string>();
        decimal? weeksOfCover = null;
        decimal? weeksToSeasonEnd = null;
        int? age = daysSinceReceipt is >= 0 ? daysSinceReceipt : null;
        var ageBasis = age is null
            ? "unavailable"
            : receiptEvidenceStatus switch
            {
                "received" => "first_receipt",
                "first_sale_fallback" => "first_positive_sale_fallback",
                _ => "unavailable"
            };

        if (ageBasis == "unavailable")
        {
            age = null;
            reasons.Add("receipt_age_unavailable");
        }

        if (units180 > 0 && stockUnits >= 0)
        {
            weeksOfCover = Round2(stockUnits * 180m / units180 / 7m);
        }
        else
        {
            reasons.Add(units180 <= 0 ? "positive_net_velocity_unavailable" : "stock_quantity_unavailable");
        }

        if (seasonEndUtc.HasValue && seasonEndUtc.Value > DateTime.MinValue)
        {
            weeksToSeasonEnd = Round2((decimal)(seasonEndUtc.Value.Date - anchorDateUtc.Date).TotalDays / 7m);
        }
        else
        {
            reasons.Add("season_end_unavailable");
        }

        decimal? coverGap = weeksOfCover.HasValue && weeksToSeasonEnd.HasValue
            ? Round2(weeksOfCover.Value - weeksToSeasonEnd.Value)
            : null;
        if (!coverGap.HasValue) reasons.Add("cover_gap_unavailable");

        // Current Pre-Nivelacija history has no authoritative opening-stock and inbound denominator.
        reasons.Add("sell_through_denominator_unavailable");

        var score = ComputeScore(coverGap, age);
        if (!score.HasValue) reasons.Add("shadow_score_unavailable");

        return new PreNivelacijaShadowV9EvidenceDto
        {
            Version = Version,
            WeeksOfCover = weeksOfCover,
            WeeksToSeasonEnd = weeksToSeasonEnd,
            AgeDays = age,
            AgeBasis = ageBasis,
            SellThroughSinceReceipt = null,
            SellThroughUnavailableReason = "opening_stock_and_inbound_history_unavailable",
            CoverGap = coverGap,
            Score = score,
            ReasonCodes = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    public static PreNivelacijaShadowComparisonDto BuildComparison(IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates)
    {
        var eligible = candidates
            .Where(candidate => candidate.ShadowV9.Score.HasValue)
            .ToArray();
        if (eligible.Length == 0)
        {
            return new PreNivelacijaShadowComparisonDto
            {
                Version = Version,
                Methodology = Methodology,
                CoverGapWeight = CoverGapWeight,
                AgeWeight = AgeWeight,
                CoverGapSaturationWeeks = CoverGapSaturationWeeks,
                AgeSaturationDays = AgeSaturationDays,
                Label = "Eksperimentalno poređenje nije dostupno: nema redova sa dovoljnim dokazima.",
                TopN = 0
            };
        }

        var canonicalOrder = eligible
            .OrderBy(candidate => candidate.CanonicalRank)
            .ThenBy(candidate => candidate.ArtikalId)
            .ThenBy(candidate => candidate.StoreId)
            .ToArray();
        var shadowOrder = eligible
            .OrderBy(candidate => candidate.ShadowV9.Rank ?? int.MaxValue)
            .ThenBy(candidate => candidate.ArtikalId)
            .ThenBy(candidate => candidate.StoreId)
            .ToArray();
        var canonicalRanks = canonicalOrder.Select((candidate, index) => (Key: Key(candidate), Rank: index + 1))
            .ToDictionary(item => item.Key, item => item.Rank, StringComparer.Ordinal);
        var shadowRanks = shadowOrder.Select((candidate, index) => (Key: Key(candidate), Rank: index + 1))
            .ToDictionary(item => item.Key, item => item.Rank, StringComparer.Ordinal);

        var topN = Math.Min(ComparisonTopN, eligible.Length);
        var canonicalTop = canonicalOrder.Take(topN).Select(Key).ToHashSet(StringComparer.Ordinal);
        var shadowTop = shadowOrder.Take(topN).Select(Key).ToHashSet(StringComparer.Ordinal);
        var overlap = canonicalTop.Intersect(shadowTop, StringComparer.Ordinal).Count();
        var movementRows = eligible
            .Select(candidate =>
            {
                var key = Key(candidate);
                return (Candidate: candidate, CanonicalRank: canonicalRanks[key], ShadowRank: shadowRanks[key]);
            })
            .Select(item => new PreNivelacijaShadowRankMovementDto
            {
                ArtikalId = item.Candidate.ArtikalId,
                StoreId = item.Candidate.StoreId,
                Sku = item.Candidate.Sku,
                CanonicalRank = item.CanonicalRank,
                ShadowRank = item.ShadowRank,
                RankMovement = item.CanonicalRank - item.ShadowRank,
                ShadowScore = item.Candidate.ShadowV9.Score!.Value,
                WeeksOfCover = item.Candidate.ShadowV9.WeeksOfCover,
                WeeksToSeasonEnd = item.Candidate.ShadowV9.WeeksToSeasonEnd,
                AgeDays = item.Candidate.ShadowV9.AgeDays,
                AgeBasis = item.Candidate.ShadowV9.AgeBasis
            })
            .ToArray();

        return new PreNivelacijaShadowComparisonDto
        {
            Version = Version,
            Methodology = Methodology,
            CoverGapWeight = CoverGapWeight,
            AgeWeight = AgeWeight,
            CoverGapSaturationWeeks = CoverGapSaturationWeeks,
            AgeSaturationDays = AgeSaturationDays,
            RowsCompared = eligible.Length,
            SpearmanRankCorrelation = Spearman(canonicalOrder, canonicalRanks, shadowRanks),
            TopN = topN,
            TopNOverlapCount = overlap,
            RowsWithRankMovement = movementRows.Count(item => item.RankMovement != 0),
            LargestMovements = movementRows
                .OrderByDescending(item => Math.Abs(item.RankMovement))
                .ThenBy(item => item.CanonicalRank)
                .ThenBy(item => item.ArtikalId)
                .Take(10)
                .ToArray()
        };
    }

    public static void AssignRanks(IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates)
    {
        // The caller passes the exact list after the existing canonical v10 sort.
        for (var index = 0; index < candidates.Count; index++)
            candidates[index].CanonicalRank = index + 1;

        var shadowOrder = candidates
            .Where(candidate => candidate.ShadowV9.Score.HasValue)
            .OrderByDescending(candidate => candidate.ShadowV9.Score)
            .ThenBy(candidate => candidate.Sku, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ArtikalId)
            .ThenBy(candidate => candidate.StoreId)
            .ToArray();
        for (var index = 0; index < shadowOrder.Length; index++)
            shadowOrder[index].ShadowV9.Rank = index + 1;
    }

    private static decimal? ComputeScore(decimal? coverGapWeeks, int? ageDays)
    {
        var weighted = 0m;
        var availableWeight = 0m;
        if (coverGapWeeks.HasValue)
        {
            var normalizedCoverGap = Math.Clamp(Math.Max(coverGapWeeks.Value, 0m) / CoverGapSaturationWeeks * 100m, 0m, 100m);
            weighted += normalizedCoverGap * CoverGapWeight;
            availableWeight += CoverGapWeight;
        }
        if (ageDays.HasValue)
        {
            var normalizedAge = Math.Clamp(ageDays.Value / (decimal)AgeSaturationDays * 100m, 0m, 100m);
            weighted += normalizedAge * AgeWeight;
            availableWeight += AgeWeight;
        }

        return availableWeight <= 0m ? null : Round2(weighted / availableWeight);
    }

    private static decimal? Spearman(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> canonicalOrder,
        IReadOnlyDictionary<string, int> canonicalRanks,
        IReadOnlyDictionary<string, int> shadowRanks)
    {
        var count = canonicalOrder.Count;
        if (count < 2) return null;

        var squaredDifferences = canonicalOrder.Sum(candidate =>
        {
            var key = Key(candidate);
            var difference = canonicalRanks[key] - shadowRanks[key];
            return (decimal)difference * difference;
        });
        var denominator = (decimal)count * count * count - count;
        return denominator == 0m ? null : Round2(1m - (6m * squaredDifferences / denominator));
    }

    private static string Key(PreNivelacijaSkuCandidateDto candidate) =>
        $"{candidate.ArtikalId}:{candidate.StoreId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "all"}";

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
