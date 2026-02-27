using Api.Models;

namespace Api.Services;

public interface IPreNivelacijaScoringService
{
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
            decimal preNivelacijaScore);
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
            decimal preNivelacijaScore)
    {
        // Bayesian smoothing for baseline daily units
        var smoothedUnits = BayesianSmoothing(units180, 180m, 0.05m);

        // Highlight scenario
        var highlightBoost = 0.15m + (preNivelacijaScore / 100m) * 0.30m;
        var highlightUnits = CalculateScenarioUnits(smoothedUnits, stockUnits, highlightBoost);
        var highlightRevenue = highlightUnits * sellingPrice;
        var highlightMargin = highlightUnits * Math.Max(0m, sellingPrice - purchasePrice);

        // Markdown scenario
        var markdownDiscountPct = Clamp(0.08m + markdownEvents * 0.02m + avgMarkdownPct / 200m, 0.08m, 0.35m);
        var markdownPrice = sellingPrice * (1m - markdownDiscountPct);
        var markdownDemandBoost = 1m + markdownDiscountPct * 1.8m;
        var markdownUnits = CalculateScenarioUnits(smoothedUnits, stockUnits, markdownDemandBoost);
        var markdownRevenue = markdownUnits * markdownPrice;
        var markdownMargin = markdownUnits * Math.Max(0m, markdownPrice - purchasePrice);

        // Confidence level
        var confidence = CalculateConfidence(units180, stockUnits);

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
        return (int)Math.Clamp(
            Math.Round((double)(baselineUnits * 30m * boost), MidpointRounding.AwayFromZero),
            1,
            Math.Max(1, stockUnits));
    }

    private static string CalculateConfidence(int units180, int stockUnits)
    {
        if (units180 >= 18 && stockUnits >= 6) return "High";
        if (units180 >= 6 || stockUnits >= 4) return "Medium";
        return "Low";
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
