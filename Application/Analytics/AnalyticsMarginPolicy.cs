namespace Application.Analytics;

public enum MarginCostSource
{
    None = 0,
    Historical = 1,
    ProductFallbackRsd = 2,
    ProductFallbackLegacy = 3
}

public readonly record struct ResolvedUnitCost(
    decimal? UnitCost,
    MarginCostSource Source);

public readonly record struct MarginSnapshot(
    decimal RevenueWithCost,
    decimal TotalCost,
    decimal MarginContribution,
    double MarginPct,
    double? MarginDataCoveragePct,
    decimal HistoricalCostRevenue,
    decimal EstimatedCostRevenue,
    double? HistoricalMarginCoveragePct,
    double? FallbackCostCoveragePct);

public sealed class MarginAccumulator
{
    private decimal _revenueWithCost;
    private decimal _totalCost;
    private decimal _historicalCostRevenue;
    private decimal _estimatedCostRevenue;

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
        _historicalCostRevenue += revenue;
    }

    public void Add(
        decimal revenue,
        decimal quantity,
        decimal? saleLineCost,
        decimal? productCostRsd,
        decimal? productCostLegacy)
    {
        Add(revenue, quantity, AnalyticsMarginPolicy.ResolveUnitCostWithSource(saleLineCost, productCostRsd, productCostLegacy));
    }

    public void Add(decimal revenue, decimal quantity, ResolvedUnitCost resolvedCost)
    {
        if (!AnalyticsMarginPolicy.IsReliableCost(resolvedCost.UnitCost))
        {
            return;
        }

        _revenueWithCost += revenue;
        _totalCost += quantity * resolvedCost.UnitCost!.Value;

        if (resolvedCost.Source == MarginCostSource.Historical)
        {
            _historicalCostRevenue += revenue;
            return;
        }

        if (resolvedCost.Source is MarginCostSource.ProductFallbackRsd or MarginCostSource.ProductFallbackLegacy)
        {
            _estimatedCostRevenue += revenue;
        }
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
                : null,
            HistoricalCostRevenue: Math.Round(_historicalCostRevenue, 2),
            EstimatedCostRevenue: Math.Round(_estimatedCostRevenue, 2),
            HistoricalMarginCoveragePct: totalRevenue != 0m
                ? Math.Round((double)(_historicalCostRevenue / totalRevenue * 100m), 2)
                : null,
            FallbackCostCoveragePct: totalRevenue != 0m
                ? Math.Round((double)(_estimatedCostRevenue / totalRevenue * 100m), 2)
                : null);
    }
}

public static class AnalyticsMarginPolicy
{
    public static ResolvedUnitCost ResolveUnitCostWithSource(
        decimal? saleLineCost,
        decimal? productCostRsd,
        decimal? productCostLegacy)
    {
        if (IsReliableCost(saleLineCost))
        {
            return new ResolvedUnitCost(saleLineCost, MarginCostSource.Historical);
        }

        if (IsReliableCost(productCostRsd))
        {
            return new ResolvedUnitCost(productCostRsd, MarginCostSource.ProductFallbackRsd);
        }

        if (IsReliableCost(productCostLegacy))
        {
            return new ResolvedUnitCost(productCostLegacy, MarginCostSource.ProductFallbackLegacy);
        }

        return new ResolvedUnitCost(null, MarginCostSource.None);
    }

    public static decimal? ResolveUnitCost(
        decimal? saleLineCost,
        decimal? productCostRsd,
        decimal? productCostLegacy)
    {
        return ResolveUnitCostWithSource(saleLineCost, productCostRsd, productCostLegacy).UnitCost;
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
