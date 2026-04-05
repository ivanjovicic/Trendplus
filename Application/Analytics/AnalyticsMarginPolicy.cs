namespace Application.Analytics;

public readonly record struct MarginSnapshot(
    decimal RevenueWithCost,
    decimal TotalCost,
    decimal MarginContribution,
    double MarginPct,
    double? MarginDataCoveragePct);

public sealed class MarginAccumulator
{
    private decimal _revenueWithCost;
    private decimal _totalCost;

    public decimal RevenueWithCost => _revenueWithCost;

    public decimal TotalCost => _totalCost;

    public void Add(decimal revenue, decimal quantity, decimal? unitCost)
    {
        if (!AnalyticsMarginPolicy.IsReliableCost(unitCost))
        {
            return;
        }

        _revenueWithCost += revenue;
        _totalCost += quantity * unitCost!.Value;
    }

    public MarginSnapshot Build(decimal totalRevenue)
    {
        var marginContribution = _revenueWithCost - _totalCost;
        var marginPct = _revenueWithCost > 0m
            ? (double)(marginContribution / _revenueWithCost * 100m)
            : 0d;

        return new MarginSnapshot(
            RevenueWithCost: Math.Round(_revenueWithCost, 2),
            TotalCost: Math.Round(_totalCost, 2),
            MarginContribution: Math.Round(marginContribution, 2),
            MarginPct: Math.Round(marginPct, 2),
            MarginDataCoveragePct: totalRevenue != 0m
                ? Math.Round((double)(_revenueWithCost / totalRevenue * 100m), 2)
                : null);
    }
}

public static class AnalyticsMarginPolicy
{
    public static decimal? ResolveUnitCost(
        decimal? saleLineCost,
        decimal? productCostRsd,
        decimal? productCostLegacy)
    {
        if (IsReliableCost(saleLineCost))
        {
            return saleLineCost;
        }

        if (IsReliableCost(productCostRsd))
        {
            return productCostRsd;
        }

        if (IsReliableCost(productCostLegacy))
        {
            return productCostLegacy;
        }

        return null;
    }

    public static decimal? ResolveProductUnitCost(
        decimal? productCostRsd,
        decimal? productCostLegacy)
        => ResolveUnitCost(null, productCostRsd, productCostLegacy);

    public static bool IsReliableCost(decimal? unitCost)
        => unitCost.HasValue && unitCost.Value > 0m;

    public static string BuildPositiveCostSql(params string[] candidates)
    {
        if (candidates is null || candidates.Length == 0)
        {
            throw new ArgumentException("At least one SQL candidate is required.", nameof(candidates));
        }

        var clauses = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => $"WHEN {candidate} > 0 THEN {candidate}")
            .ToArray();

        if (clauses.Length == 0)
        {
            throw new ArgumentException("At least one non-empty SQL candidate is required.", nameof(candidates));
        }

        return $"CASE {string.Join(" ", clauses)} ELSE NULL END";
    }
}
