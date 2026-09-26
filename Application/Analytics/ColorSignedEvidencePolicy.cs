namespace Application.Analytics;

/// <summary>
/// Contract helpers for Color analytics, where sales lines are net signed evidence.
/// Amounts remain signed; ratio metrics are only emitted when their denominator and
/// numerator produce a valid non-negative percentage.
/// </summary>
public static class ColorSignedEvidencePolicy
{
    public const string SignedRevenuePolicy = "signed_net_revenue_preserved";
    public const string SignedQuantityPolicy = "signed_net_quantity_preserved";
    public const string PositiveNetRevenueDenominator = "measured_positive_net_revenue";
    public const string NonPositiveNetRevenueDenominator = "unavailable_non_positive_net_revenue";

    public static double? ResolveWeightedMarginPct(
        IEnumerable<(decimal RevenueWithCost, decimal MarginContribution)> evidence)
    {
        var rows = evidence.ToList();
        var coveredRevenue = rows.Sum(row => row.RevenueWithCost);
        if (coveredRevenue <= 0m)
        {
            return null;
        }

        var marginPct = (double)(rows.Sum(row => row.MarginContribution) / coveredRevenue * 100m);
        return double.IsFinite(marginPct) ? Math.Round(marginPct, 2) : null;
    }

    public static double? ResolveNonNegativePercentage(decimal numerator, decimal denominator)
    {
        if (denominator <= 0m || numerator < 0m)
        {
            return null;
        }

        var percentage = (double)(numerator / denominator * 100m);
        return double.IsFinite(percentage) && percentage is >= 0d and <= 100d
            ? Math.Round(percentage, 2)
            : null;
    }

    public static MarginQualityClassifier.MarginQualityResult ClassifyCostQuality(
        MarginSnapshot snapshot,
        decimal totalRevenue)
    {
        if (totalRevenue <= 0m)
        {
            return UnavailableCostQuality("Neto promet nije pozitivan, pa pokriće troška nije merljivo.");
        }

        var historical = ResolveNonNegativePercentage(snapshot.HistoricalCostRevenue, totalRevenue);
        var estimated = ResolveNonNegativePercentage(snapshot.EstimatedCostRevenue, totalRevenue);
        var noCost = ResolveNonNegativePercentage(totalRevenue - snapshot.RevenueWithCost, totalRevenue);
        var totalCoverage = ResolveNonNegativePercentage(snapshot.RevenueWithCost, totalRevenue);

        if (!historical.HasValue || !estimated.HasValue || !noCost.HasValue || !totalCoverage.HasValue)
        {
            return UnavailableCostQuality("Signed trošak ili promet ne daju validan nenegativan imenilac za coverage.");
        }

        return MarginQualityClassifier.Classify(
            historical.Value,
            estimated.Value,
            noCost.Value,
            totalCoverage.Value);
    }

    public static bool HasMeasurableRecommendationEvidence(
        decimal totalRevenue,
        double? marginPct,
        double? marginCoveragePct,
        double? unknownColorSharePct)
        => totalRevenue > 0m
            && marginPct.HasValue
            && marginCoveragePct.HasValue
            && unknownColorSharePct.HasValue;

    public static MarginQualityClassifier.MarginQualityResult UnavailableCostQuality(string reason)
        => new(
            Tier: "unavailable",
            Label: "Nije merljivo",
            ShortLabel: "Nije merljivo",
            Tooltip: reason);
}
