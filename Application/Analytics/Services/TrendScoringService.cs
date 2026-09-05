using System;
using System.Collections.Generic;
using System.Linq;

namespace Application.Analytics.Services;

/// <summary>
/// Čiste analitičke formule Trend Engine-a — bez DB, bez HTTP.
///
/// Sve metode su statičke. Odgovaraju 1:1 Python implementaciji u
/// trend_engine/core.py. Komentari u kodu pokazuju Python ekvivalente.
/// </summary>
public static class TrendScoringService
{
    // ═══════════════════════════════════════════════════════════════════════
    //  KONFIGURACIJA TEŽINA
    //  Svaka konstanta preslikava Python konstantu u core.py
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, double> SourceWeight = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zalando"]   = 1.00,
        ["aboutyou"]  = 0.90,
        ["deichmann"] = 0.75,
        ["humanic"]   = 0.70,
    };

    private static readonly Dictionary<string, double> MarketWeight = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DE"] = 1.00,
        ["AT"] = 0.85,
        ["CH"] = 0.80,
        ["HU"] = 0.60,
        ["RO"] = 0.55,
    };

    private const double NoImagePenalty   = 0.50;
    private const double NewArrivalBonus  = 0.20;  // ×1.20
    private const double SaleBonus        = 0.10;  // ×1.10
    private const double CrossSourceBonus = 0.40;
    private const double CrossMarketBonus = 0.15;
    private const double EntropyBonusMax  = 0.25;
    private const double MaxSocialWeight  = 0.30;

    // ═══════════════════════════════════════════════════════════════════════
    //  RANK SCORE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Logaritamski pad po rangu.
    ///
    /// rankScore = 1 / log2(rank + 1)
    ///   rank=1  → 1.000
    ///   rank=2  → 0.631
    ///   rank=10 → 0.289
    ///
    /// Python: _rank_score(rank)
    /// </summary>
    public static double RankScore(int rank)
    {
        int r = Math.Max(rank, 1);
        return 1.0 / Math.Log2(r + 1);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ITEM SCORE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Score za jedan scraped item.
    ///
    /// itemScore = rankScore × sourceWeight × marketWeight
    ///           × imageFactor × newFactor × saleFactor
    ///
    /// Python: g["base_score"] += item_score  (unutar compute_trend_groups)
    /// </summary>
    public static double ItemScore(
        int rank,
        string source,
        string market,
        bool hasImage = true,
        bool isNew    = false,
        bool isOnSale = false)
    {
        double score =
            RankScore(rank)
            * SourceWeight.GetValueOrDefault(source.ToLowerInvariant(), 0.5)
            * MarketWeight.GetValueOrDefault(market.ToUpperInvariant(), 0.5);

        if (!hasImage) score *= NoImagePenalty;
        if (isNew)     score *= (1.0 + NewArrivalBonus);
        if (isOnSale)  score *= (1.0 + SaleBonus);

        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  GROUP FINAL SCORE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Grupni multiplikatori na base_score.
    ///
    /// Formule:
    ///   crossSrcMult  = 1 + 0.40 × (uniqueSources - 1)
    ///   crossMktMult  = 1 + 0.15 × (uniqueMarkets - 1)
    ///   entropyMult   = 1 + (H / Hmax) × 0.25
    ///   finalScore    = baseScore × crossSrcMult × crossMktMult × entropyMult
    ///
    /// Python: compute_trend_groups – korak 3
    /// </summary>
    public static double? GroupFinalScore(
        double baseScore,
        int uniqueSources,
        int uniqueMarkets,
        Dictionary<string, int> sourceCounts)
    {
        if (!double.IsFinite(baseScore)) return null;
        if (baseScore <= 0) return 0.0;

        // Cross-source: svaki dodatni izvor dodaje 40%
        double crossSrcMult = 1.0 + Math.Max(0, uniqueSources - 1) * CrossSourceBonus;

        // Cross-market: svako dodatno tržište dodaje 15%
        double crossMktMult = 1.0 + Math.Max(0, uniqueMarkets - 1) * CrossMarketBonus;

        // Entropijski bonus — nagrađijemo ravnomjernu distribuciju po izvorima
        double H    = ShannonEntropy(sourceCounts);
        double Hmax = uniqueSources > 1 ? Math.Log2(uniqueSources) : 0.0;
        double entropyRatio = Hmax > 0 ? H / Hmax : 0.0;
        double entropyMult  = 1.0 + entropyRatio * EntropyBonusMax;

        return baseScore * crossSrcMult * crossMktMult * entropyMult;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SHANNON ENTROPIJA
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// H = -Σ p_i × log2(p_i)
    ///
    /// Maksimum kada su svi izvori jednako zastupljeni.
    /// Python: _shannon_entropy(counts)
    /// </summary>
    public static double ShannonEntropy(Dictionary<string, int> counts)
    {
        if (counts is null || counts.Count == 0) return 0.0;

        double total = counts.Values.Sum();
        if (total <= 0) return 0.0;

        return -counts.Values
            .Where(c => c > 0)
            .Sum(c =>
            {
                double p = c / total;
                return p * Math.Log2(p);
            });
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SOCIAL BOOST
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Primijeni social signal kao multiplikator.
    ///
    /// Formula:
    ///   socialNorm   = clamp(socialScore / 100, 0, 1)
    ///   boostFactor  = 1 + socialWeight × socialNorm
    ///   finalScore'  = finalScore × boostFactor
    ///
    /// Primjeri (socialWeight=0.30):
    ///   social=100 → ×1.30  social=50 → ×1.15  social=0 → ×1.00
    ///
    /// Python: apply_social_boost(groups, social_scores_by_brand)
    /// </summary>
    public static double? ApplySocialBoost(
        double finalScore,
        double socialScore,
        double socialWeight = MaxSocialWeight)
    {
        if (!double.IsFinite(finalScore) || !double.IsFinite(socialScore) || !double.IsFinite(socialWeight))
            return null;

        double norm   = Math.Max(0.0, Math.Min(1.0, socialScore / 100.0));
        double factor = 1.0 + socialWeight * norm;
        return finalScore * factor;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  MOMENTUM
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Izračunaj momentum za jedan proizvod između dva dana.
    ///
    /// Formula:
    ///   scoreDelta     = todayScore - yesterdayScore
    ///   relScoreDelta  = scoreDelta / max(|yesterdayScore|, ε)
    ///   rankComponent  = (yesterdayRank - todayRank) / 1000
    ///   raw            = 0.70 × relScoreDelta + 0.30 × rankComponent
    ///   momentum       = clamp(raw, -1, 1)
    ///
    /// Tumačenje:
    ///   > 0  → raste (postaje trendovskiji)
    ///   = 0  → stagnira
    ///   < 0  → opada
    ///
    /// Python: analytics/trend_momentum_engine.py::compute_momentum()
    /// </summary>
    public static double? ComputeMomentum(
        double? todayScore,
        double? yesterdayScore,
        int?    todayRank       = null,
        int?    yesterdayRank   = null)
    {
        // Novi ulaz u sistem
        if (yesterdayScore is null || todayScore is null
            || !double.IsFinite(yesterdayScore.Value)
            || !double.IsFinite(todayScore.Value))
            return null;

        const double epsilon = 1e-6;

        double scoreDelta    = todayScore.Value - yesterdayScore.Value;
        double relScoreDelta = scoreDelta / Math.Max(Math.Abs(yesterdayScore.Value), epsilon);

        double rankComponent = 0.0;
        if (todayRank.HasValue && yesterdayRank.HasValue)
        {
            // Poboljšan rank (manji broj) → pozitivna delta
            int rankDelta = yesterdayRank.Value - todayRank.Value;
            // Normalizujemo na max ~1000 pozicija
            rankComponent = rankDelta / 1000.0;
        }

        // 70% score component, 30% rank component
        double raw = 0.70 * relScoreDelta + 0.30 * rankComponent;

        // Clamp [-1, 1]
        return Math.Max(-1.0, Math.Min(1.0, raw));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TRENDPLUS INDEX
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Izračunaj Trendplus Index [0–100] za dati scope (market/brand/category).
    ///
    /// Algoritam:
    ///   1. Uzmi top K score-ova (gdje K = topK, default 50)
    ///   2. Normalizuj na [0,1] dijeleći s max
    ///   3. baseComponent = prosjek normalizovanih
    ///   4. index = 100 × baseComponent
    ///
    /// Python: analytics/trendplus_index.py::compute_trendplus_index()
    /// </summary>
    public static double? ComputeTrendIndex(
        IEnumerable<double> scores,
        int topK = 50)
    {
        var finiteScores = scores
            .Where(double.IsFinite)
            .ToList();

        if (finiteScores.Count == 0)
            return null;

        var list = finiteScores
            .Where(s => s >= 0)
            .OrderByDescending(s => s)
            .Take(topK)
            .ToList();

        if (list.Count == 0) return null;

        double maxScore = list.Max();
        if (maxScore <= 0) return 0.0;

        double baseComponent = list.Average(s => s / maxScore);
        return Math.Round(100.0 * baseComponent, 4);
    }

    /// <summary>
    /// Index s momentum i social komponentama.
    ///
    /// Formula:
    ///   baseComponent     = ComputeTrendIndex(scores, topK)
    ///   momentumComponent = clamp(avg(momentums), 0, 1) × 100
    ///   socialComponent   = socialScore (0–100, optional)
    ///
    ///   index = 0.65 × base + 0.25 × momentum + 0.10 × social
    ///
    /// Python: analytics/trendplus_index.py::compute_trendplus_index(products)
    /// </summary>
    public static (double? Index, double? Base, double? Momentum, double? Social)
        ComputeExtendedTrendIndex(
            IEnumerable<double> scores,
            IEnumerable<double>? momentums = null,
            double? avgSocialScore = null,
            int topK = 50)
    {
        double? baseComponent = ComputeTrendIndex(scores, topK);

        double? momentumComponent = null;
        if (momentums is not null)
        {
            var mList = momentums.Where(double.IsFinite).ToList();
            if (mList.Count > 0)
            {
                double avgMom = mList.Average();
                // clamp [-1,1] → normalizuj na [0,100]
                momentumComponent = Math.Max(0.0, Math.Min(1.0, (avgMom + 1.0) / 2.0)) * 100.0;
            }
        }

        double? socialComponent = avgSocialScore is { } social && double.IsFinite(social)
            ? Math.Max(0.0, Math.Min(100.0, social))
            : null;

        if (baseComponent is null)
            return (null, null, momentumComponent, socialComponent);

        // Missing components are excluded and the remaining weights are normalized.
        // This prevents an unavailable signal from becoming a trusted zero.
        double weightedTotal = 0.65 * baseComponent.Value;
        double weightTotal = 0.65;
        if (momentumComponent is { } momentum)
        {
            weightedTotal += 0.25 * momentum;
            weightTotal += 0.25;
        }
        if (socialComponent is { } socialValue)
        {
            weightedTotal += 0.10 * socialValue;
            weightTotal += 0.10;
        }

        double index = weightedTotal / weightTotal;

        return (Math.Round(index, 4), Math.Round(baseComponent.Value, 4),
                momentumComponent is { } momentumValue ? Math.Round(momentumValue, 4) : null,
                socialComponent is { } socialValue2 ? Math.Round(socialValue2, 4) : null);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  INVENTORY PREPORUKE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Izračunaj preporučenu količinu narudžbe na osnovu trend i momentum signala.
    ///
    /// Formula:
    ///   horizon       = leadTimeDays + targetCoverageDays
    ///   baselineDemand = salesVelocityPerDay × horizon
    ///   trendMult     = 1 + 0.50 × clamp(trendScore, 0, 1)
    ///   momentumMult  = 1 + 0.30 × clamp(momentumScore, -1, 1)
    ///   targetDemand  = baselineDemand × trendMult × momentumMult
    ///   recommended   = max(0, round(targetDemand - stockOnHand))
    ///
    /// Napomene:
    ///   trendScore treba biti normalizovan na [0,1] prije poziva
    ///   momentumScore je u [-1,1] (direktno iz ComputeMomentum)
    ///
    /// Python: analytics/inventory_intelligence_model.py::compute_inventory_recommendations
    /// </summary>
    public static int? ComputeRecommendedOrderQty(
        double trendScore,
        double momentumScore,
        double salesVelocityPerDay,
        int    stockOnHand,
        int    leadTimeDays       = 14,
        int    targetCoverageDays = 30)
    {
        if (!double.IsFinite(trendScore)
            || !double.IsFinite(momentumScore)
            || !double.IsFinite(salesVelocityPerDay))
            return null;

        if (salesVelocityPerDay <= 0)
            return 0;

        int    horizon   = leadTimeDays + targetCoverageDays;
        double baseline  = salesVelocityPerDay * horizon;

        double t = Math.Max(0.0, Math.Min(1.0, trendScore));
        double m = Math.Max(-1.0, Math.Min(1.0, momentumScore));

        double trendMult    = 1.0 + 0.50 * t;
        double momentumMult = 1.0 + 0.30 * m;  // negativan momentum = naruči manje

        double target = baseline * trendMult * momentumMult;
        int recommended = (int)Math.Round(target - stockOnHand);

        return Math.Max(0, recommended);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CANONICAL KEY (za C# import Python rezultata)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stabilan ključ proizvoda — isti algoritam kao Python build_canonical_key().
    ///
    /// Koristi se kad C# worker deserijalizuje Python JSON rezultate
    /// i upisuje ih u TrendProductSnapshots tabelu.
    ///
    /// Prioritet:
    ///   1. brand + productId
    ///   2. brand + sku
    ///   3. brand + ID iz URL-a (regex: 6+ cifara)
    ///   4. brand + tokenizovano ime (fallback)
    ///
    /// Python: build_canonical_key(item) u core.py
    /// </summary>
    public static string BuildCanonicalKey(
        string brand,
        string? productId = null,
        string? sku       = null,
        string? url       = null,
        string? name      = null)
    {
        string brandNorm = NormText(brand);

        if (!string.IsNullOrWhiteSpace(productId))
            return $"{brandNorm}|id:{NormText(productId)}";

        if (!string.IsNullOrWhiteSpace(sku))
            return $"{brandNorm}|sku:{NormText(sku)}";

        if (!string.IsNullOrWhiteSpace(url))
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                url, @"/[a-z0-9-]*?(\d{6,})(?:\.html?)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
                return $"{brandNorm}|id:{m.Groups[1].Value}";
        }

        string tokens = TokenizeName(name ?? "");
        return $"{brandNorm}|{tokens}";
    }

    // ─── Private helpers ──────────────────────────────────────────────────

    private static readonly System.Text.RegularExpressions.Regex _StripRe =
        new(@"[^a-z0-9 ]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly HashSet<string> _StopTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "damen","herren","women","men","woman","man","ladies",
        "girls","boys","femme","homme",
        "noi","barbati","damske","panske",
        "new","sale","original","official",
    };

    private static string NormText(string s) =>
        _StripRe.Replace(s.ToLowerInvariant(), "").Trim();

    private static string TokenizeName(string name)
    {
        var tokens = NormText(name)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !_StopTokens.Contains(t))
            .OrderBy(t => t);
        return string.Join(" ", tokens);
    }
}
