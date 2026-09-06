using Api.Models;

namespace Api.Services;

public interface IPreNivelacijaScoringService
{
    public sealed record RecommendationInput(
        decimal PreNivelacijaScore,
        decimal RevenueDelta,
        decimal MinRevenueDelta,
        decimal MaxRevenueDelta,
        int DaysSinceLastSale,
        string PriorityBand,
        string Confidence,
        int Units180,
        int StockUnits,
        bool HasCompleteEvidence = true);

    public sealed record RecommendationResult(
        int DecisionScore,
        double ReliabilityPct,
        PreNivelacijaRecommendationDto Recommendation);

    PreNivelacijaScoreBreakdownDto ComputeScoreBreakdown(
        int stockUnits,
        decimal velocity180,
        int daysSinceLastSale,
        int markdownEvents,
        decimal avgMarkdownPct,
        decimal grossMarginPctEst,
        decimal seasonRecencyBoost,
        int maxStock,
        decimal maxVelocity);

    decimal ComputePreNivelacijaScore(PreNivelacijaScoreBreakdownDto breakdown);

    (PreNivelacijaScenarioDto HighlightNow, PreNivelacijaScenarioDto MarkdownNow, string Confidence)
        SimulateScenarios(
            int stockUnits,
            int units180,
            int markdownEvents,
            decimal avgMarkdownPct,
            decimal sellingPrice,
            decimal purchasePrice,
            decimal preNivelacijaScore,
            bool hasReliableCost = true);

    RecommendationResult EvaluateRecommendation(RecommendationInput input);
}

public sealed class PreNivelacijaScoringService : IPreNivelacijaScoringService
{
    public PreNivelacijaScoreBreakdownDto ComputeScoreBreakdown(
        int stockUnits,
        decimal velocity180,
        int daysSinceLastSale,
        int markdownEvents,
        decimal avgMarkdownPct,
        decimal grossMarginPctEst,
        decimal seasonRecencyBoost,
        int maxStock,
        decimal maxVelocity)
    {
        // Stock pressure: normalized stock level
        var stockPressure = maxStock <= 0 ? 0m : PercentileNormalize(stockUnits, maxStock);

        // Velocity risk: inverse of normalized velocity
        var velocityNorm = maxVelocity <= 0m ? 0m : PercentileNormalize(velocity180, maxVelocity);
        var velocityRisk = 100m - velocityNorm;

        // Recency risk: days since last sale normalized to 180 days
        var recencyRisk = PercentileNormalize(daysSinceLastSale, 180m);

        // Markdown opportunity: potential for markdown based on events and average markdown percentage
        var markdownOpportunity = Clamp(100m - (markdownEvents * 20m + avgMarkdownPct * 0.5m));

        // Margin potential: normalized gross margin percentage
        var marginPotential = PercentileNormalize(grossMarginPctEst, 60m);

        // Season recency boost: directly clamped
        var seasonBoost = Clamp(seasonRecencyBoost);

        return new PreNivelacijaScoreBreakdownDto
        {
            StockPressure = Round2(stockPressure),
            VelocityRisk = Round2(velocityRisk),
            RecencyRisk = Round2(recencyRisk),
            MarkdownOpportunity = Round2(markdownOpportunity),
            MarginPotential = Round2(marginPotential),
            SeasonRecencyBoost = Round2(seasonBoost)
        };
    }

    public decimal ComputePreNivelacijaScore(PreNivelacijaScoreBreakdownDto breakdown)
    {
        // Weighted scoring formula
        var score =
            0.30m * breakdown.StockPressure +
            0.25m * breakdown.VelocityRisk +
            0.20m * breakdown.RecencyRisk +
            0.10m * breakdown.MarkdownOpportunity +
            0.10m * breakdown.MarginPotential +
            0.05m * breakdown.SeasonRecencyBoost;

        return Round2(Clamp(score));
    }

    public (PreNivelacijaScenarioDto HighlightNow, PreNivelacijaScenarioDto MarkdownNow, string Confidence)
        SimulateScenarios(
            int stockUnits,
            int units180,
            int markdownEvents,
            decimal avgMarkdownPct,
            decimal sellingPrice,
            decimal purchasePrice,
            decimal preNivelacijaScore,
            bool hasReliableCost = true)
    {
        // Bayesian smoothing for baseline daily units
        var smoothedUnits = BayesianSmoothing(units180, 180m, 0.05m);

        // Highlight scenario
        var highlightBoost = 0.15m + (preNivelacijaScore / 100m) * 0.30m;
        var highlightUnits = CalculateScenarioUnits(smoothedUnits, stockUnits, highlightBoost);
        var highlightRevenue = highlightUnits * sellingPrice;
        var highlightMargin = hasReliableCost
            ? highlightUnits * Math.Max(0m, sellingPrice - purchasePrice)
            : 0m;

        // Markdown scenario
        var markdownDiscountPct = Clamp(0.08m + markdownEvents * 0.02m + avgMarkdownPct / 200m, 0.08m, 0.35m);
        var markdownPrice = sellingPrice * (1m - markdownDiscountPct);
        var markdownDemandBoost = 1m + markdownDiscountPct * 1.8m;
        var markdownUnits = CalculateScenarioUnits(smoothedUnits, stockUnits, markdownDemandBoost);
        var markdownRevenue = markdownUnits * markdownPrice;
        var markdownMargin = hasReliableCost
            ? markdownUnits * Math.Max(0m, markdownPrice - purchasePrice)
            : 0m;

        // Confidence level
        var confidence = hasReliableCost
            ? CalculateConfidence(units180, stockUnits)
            : "Low";

        return (
            new PreNivelacijaScenarioDto
            {
                ExpectedUnits30d = highlightUnits,
                ExpectedRevenue30d = Round2(highlightRevenue),
                ExpectedMargin30d = Round2(highlightMargin),
                EffectivePrice = Round2(sellingPrice)
            },
            new PreNivelacijaScenarioDto
            {
                ExpectedUnits30d = markdownUnits,
                ExpectedRevenue30d = Round2(markdownRevenue),
                ExpectedMargin30d = Round2(markdownMargin),
                EffectivePrice = Round2(markdownPrice)
            },
            confidence
        );
    }

    public IPreNivelacijaScoringService.RecommendationResult EvaluateRecommendation(
        IPreNivelacijaScoringService.RecommendationInput input)
    {
        var reliabilityPct = ResolveReliabilityPct(input.Confidence);
        var scoreBase = Clamp(input.PreNivelacijaScore);
        var hasRevenueDeltaEvidence = input.MaxRevenueDelta > input.MinRevenueDelta;
        var deltaNorm = hasRevenueDeltaEvidence
            ? ResolveRevenueDeltaNorm(input.RevenueDelta, input.MinRevenueDelta, input.MaxRevenueDelta)
            : 0m;
        var staleRiskNorm = Clamp((input.DaysSinceLastSale / 90m) * 100m);
        var decisionScore = (int)Math.Round(
            (double)Clamp(scoreBase * 0.50m + deltaNorm * 0.20m + staleRiskNorm * 0.15m + (decimal)reliabilityPct * 0.15m),
            MidpointRounding.AwayFromZero);

        var lowReliability = reliabilityPct < 40d;
        var lowPriorityBand = string.Equals(input.PriorityBand, "low", StringComparison.OrdinalIgnoreCase);
        var negativeDelta = input.RevenueDelta < 0m;
        var thinSample = input.Units180 < 6 && input.StockUnits < 4;

        var reasons = new List<string>();
        if (!input.HasCompleteEvidence) reasons.Add("missing_evidence");
        if (!hasRevenueDeltaEvidence) reasons.Add("missing_revenue_delta_baseline");
        if (thinSample) reasons.Add("thin_sample");
        if (lowReliability) reasons.Add("low_confidence_signal");
        if (lowPriorityBand) reasons.Add("low_priority_band");
        if (negativeDelta) reasons.Add("highlight_underperforms_markdown");
        if (input.DaysSinceLastSale >= 60) reasons.Add("stale_inventory_pressure");
        if (input.PreNivelacijaScore >= 75m) reasons.Add("high_pre_nivelacija_score");

        var status = !input.HasCompleteEvidence || !hasRevenueDeltaEvidence
            ? "insufficient_data"
            : ResolveStatus(decisionScore, lowReliability, lowPriorityBand, negativeDelta, thinSample);
        var confidencePct = ResolveConfidencePct(status, decisionScore, reliabilityPct, thinSample);

        var recommendation = new PreNivelacijaRecommendationDto
        {
            Status = status,
            Label = ResolveLabel(status),
            Summary = BuildSummary(status, lowReliability, lowPriorityBand, negativeDelta, reliabilityPct),
            ConfidencePct = confidencePct,
            ReliabilityPct = reliabilityPct,
            DataQualityStatus = !input.HasCompleteEvidence || !hasRevenueDeltaEvidence
                ? "insufficient_data"
                : ResolveDataQualityStatus(reliabilityPct, thinSample),
            RecommendationAllowed = input.HasCompleteEvidence && hasRevenueDeltaEvidence && status is not ("insufficient_data" or "do_not_trust"),
            ReasonCodes = reasons
        };

        return new IPreNivelacijaScoringService.RecommendationResult(decisionScore, reliabilityPct, recommendation);
    }

    // Helper methods

    private static decimal PercentileNormalize(decimal value, decimal max)
    {
        return Clamp((value * 100m) / max);
    }

    private static decimal BayesianSmoothing(int observed, decimal period, decimal prior)
    {
        return (observed + prior) / (period + 1);
    }

    private static int CalculateScenarioUnits(decimal baselineUnits, int stockUnits, decimal boost)
    {
        if (stockUnits <= 0 || baselineUnits <= 0m || boost <= 0m)
        {
            return 0;
        }

        return (int)Math.Clamp(
            Math.Round((double)(baselineUnits * 30m * boost), MidpointRounding.AwayFromZero),
            0,
            stockUnits);
    }

    private static string CalculateConfidence(int units180, int stockUnits)
    {
        if (units180 >= 18 && stockUnits >= 6) return "High";
        if (units180 >= 6 || stockUnits >= 4) return "Medium";
        return "Low";
    }

    private static decimal ResolveRevenueDeltaNorm(decimal revenueDelta, decimal minRevenueDelta, decimal maxRevenueDelta)
    {
        var span = maxRevenueDelta - minRevenueDelta;
        if (span <= 0m) return 0m;

        return Clamp(((revenueDelta - minRevenueDelta) / span) * 100m);
    }

    private static double ResolveReliabilityPct(string confidence)
    {
        var normalized = (confidence ?? string.Empty).Trim();
        if (string.Equals(normalized, "High", StringComparison.OrdinalIgnoreCase)) return 90d;
        if (string.Equals(normalized, "Medium", StringComparison.OrdinalIgnoreCase)) return 65d;
        if (string.Equals(normalized, "Low", StringComparison.OrdinalIgnoreCase)) return 35d;
        return 0d;
    }

    private static string ResolveStatus(
        int decisionScore,
        bool lowReliability,
        bool lowPriorityBand,
        bool negativeDelta,
        bool thinSample)
    {
        if (thinSample)
        {
            return "insufficient_data";
        }

        if (decisionScore >= 68 && !lowReliability && !lowPriorityBand && !negativeDelta)
        {
            return "increase_focus";
        }

        if (decisionScore >= 43 && !negativeDelta)
        {
            return "maintain";
        }

        return "review";
    }

    private static double ResolveConfidencePct(string status, int decisionScore, double reliabilityPct, bool thinSample)
    {
        var confidencePct = Clamp((decisionScore * 0.60) + (reliabilityPct * 0.40), 0d, 100d);

        if (thinSample || status == "insufficient_data")
        {
            return Math.Round(Math.Min(confidencePct, 35d), 2);
        }

        if (status == "review")
        {
            confidencePct = Math.Min(confidencePct, 65d);
        }

        return Math.Round(confidencePct, 2);
    }

    private static string ResolveLabel(string status)
    {
        return status switch
        {
            "increase_focus" => "Increase focus",
            "maintain" => "Maintain",
            "review" => "Review",
            "do_not_trust" => "Do not trust",
            _ => "Insufficient data"
        };
    }

    private static string BuildSummary(
        string status,
        bool lowReliability,
        bool lowPriorityBand,
        bool negativeDelta,
        double reliabilityPct)
    {
        return status switch
        {
            "increase_focus" when !negativeDelta =>
                "Visok prioritet i bolji scenario prihoda uz isticanje pre nivelacije.",
            "increase_focus" =>
                "Signal je dovoljno jak za pojačan fokus pre nivelacije.",
            "maintain" when negativeDelta =>
                "Scenario prihoda je slabiji od markdown alternative; zadržati bez eskalacije.",
            "maintain" =>
                $"Stabilan signal bez potrebe za većom eskalacijom. Pouzdanost {reliabilityPct:0.#}%.",
            "review" when lowReliability =>
                "Signal postoji, ali je pouzdanost niska; proveriti ručno pre odluke.",
            "review" when lowPriorityBand =>
                "SKU je u nižoj prioritetnoj bandi; pregledati pre ulaganja u dodatnu vidljivost.",
            "review" when negativeDelta =>
                "Scenario isticanja je slabiji od markdown alternative; pregledati pre intervencije.",
            _ => "Premalo prodajnog ili stok signala za pouzdanu preporuku."
        };
    }

    private static string ResolveDataQualityStatus(double reliabilityPct, bool thinSample)
    {
        if (thinSample || reliabilityPct < 40d)
        {
            return "critical";
        }

        if (reliabilityPct < 70d)
        {
            return "warning";
        }

        return "good";
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static decimal Clamp(decimal value, decimal min = 0m, decimal max = 100m)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static decimal Round2(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
