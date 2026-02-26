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
        var stockPressure = maxStock <= 0 ? 0m : Clamp((stockUnits * 100m) / maxStock);
        var velocityNorm = maxVelocity <= 0m ? 0m : Clamp((velocity180 * 100m) / maxVelocity);
        var velocityRisk = 100m - velocityNorm;
        var recencyRisk = Clamp((daysSinceLastSale * 100m) / 180m);
        var markdownOpportunity = Clamp(100m - (markdownEvents * 20m + avgMarkdownPct * 0.5m));
        var marginPotential = Clamp((grossMarginPctEst * 100m) / 60m);

        return new PreNivelacijaScoreBreakdownDto
        {
            StockPressure = decimal.Round(stockPressure, 2),
            VelocityRisk = decimal.Round(velocityRisk, 2),
            RecencyRisk = decimal.Round(recencyRisk, 2),
            MarkdownOpportunity = decimal.Round(markdownOpportunity, 2),
            MarginPotential = decimal.Round(marginPotential, 2),
            SeasonRecencyBoost = decimal.Round(Clamp(seasonRecencyBoost), 2)
        };
    }

    public decimal ComputePreNivelacijaScore(PreNivelacijaScoreBreakdownDto breakdown)
    {
        // Formula (0-100):
        // 0.30*stockPressure + 0.25*velocityRisk + 0.20*recencyRisk
        // + 0.10*markdownOpportunity + 0.10*marginPotential + 0.05*seasonRecencyBoost
        var score =
            0.30m * breakdown.StockPressure +
            0.25m * breakdown.VelocityRisk +
            0.20m * breakdown.RecencyRisk +
            0.10m * breakdown.MarkdownOpportunity +
            0.10m * breakdown.MarginPotential +
            0.05m * breakdown.SeasonRecencyBoost;

        return decimal.Round(Clamp(score), 2);
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
        var baselineDailyUnits = units180 <= 0 ? 0.05m : units180 / 180m;
        var scoreFactor = preNivelacijaScore / 100m;

        var highlightBoost = 0.15m + scoreFactor * 0.30m;
        var highlightUnits = (int)Math.Clamp(
            Math.Round((double)(baselineDailyUnits * 30m * (1m + highlightBoost)), MidpointRounding.AwayFromZero),
            1,
            Math.Max(1, stockUnits));

        var markdownDiscountPct = Clamp(0.08m + markdownEvents * 0.02m + avgMarkdownPct / 200m, 0.08m, 0.35m);
        var markdownPrice = decimal.Round(sellingPrice * (1m - markdownDiscountPct), 2);
        var markdownDemandBoost = 1m + markdownDiscountPct * 1.8m;
        var markdownUnits = (int)Math.Clamp(
            Math.Round((double)(baselineDailyUnits * 30m * markdownDemandBoost), MidpointRounding.AwayFromZero),
            1,
            Math.Max(1, stockUnits));

        var highlightRevenue = decimal.Round(highlightUnits * sellingPrice, 2);
        var highlightMargin = decimal.Round(highlightUnits * Math.Max(0m, sellingPrice - purchasePrice), 2);
        var markdownRevenue = decimal.Round(markdownUnits * markdownPrice, 2);
        var markdownMargin = decimal.Round(markdownUnits * Math.Max(0m, markdownPrice - purchasePrice), 2);

        var confidence = "Low";
        if (units180 >= 18 && stockUnits >= 6) confidence = "High";
        else if (units180 >= 6 || stockUnits >= 4) confidence = "Medium";

        return (
            new PreNivelacijaScenarioDto
            {
                ExpectedUnits30d = highlightUnits,
                ExpectedRevenue30d = highlightRevenue,
                ExpectedMargin30d = highlightMargin,
                EffectivePrice = decimal.Round(sellingPrice, 2)
            },
            new PreNivelacijaScenarioDto
            {
                ExpectedUnits30d = markdownUnits,
                ExpectedRevenue30d = markdownRevenue,
                ExpectedMargin30d = markdownMargin,
                EffectivePrice = markdownPrice
            },
            confidence
        );
    }

    private static decimal Clamp(decimal value, decimal min = 0m, decimal max = 100m)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
