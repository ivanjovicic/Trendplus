namespace Application.Analytics;

public enum MarginCostSource
{
    None = 0,
    Historical = 1,
    ProductFallbackRsd = 2,
    ProductFallbackLegacy = 3,
    SnapshotFallback = 4
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
    double? FallbackCostCoveragePct,
    decimal SnapshotCostRevenue,
    double? SnapshotCostCoveragePct);

public sealed class MarginAccumulator
{
    private decimal _revenueWithCost;
    private decimal _totalCost;
    private decimal _historicalCostRevenue;
    private decimal _estimatedCostRevenue;
    private decimal _snapshotCostRevenue;

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

    public void Add(
        decimal revenue,
        decimal quantity,
        decimal? saleLineCost,
        decimal? snapshotCost,
        decimal? productCostRsd,
        decimal? productCostLegacy)
    {
        Add(revenue, quantity, AnalyticsMarginPolicy.ResolveUnitCostWithSnapshot(saleLineCost, snapshotCost, productCostRsd, productCostLegacy));
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

        if (resolvedCost.Source == MarginCostSource.SnapshotFallback)
        {
            _snapshotCostRevenue += revenue;
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
                : null,
            SnapshotCostRevenue: Math.Round(_snapshotCostRevenue, 2),
            SnapshotCostCoveragePct: totalRevenue != 0m
                ? Math.Round((double)(_snapshotCostRevenue / totalRevenue * 100m), 2)
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

    public static ResolvedUnitCost ResolveUnitCostWithSnapshot(
        decimal? saleLineCost,
        decimal? snapshotCost,
        decimal? productCostRsd,
        decimal? productCostLegacy)
    {
        if (IsReliableCost(saleLineCost))
        {
            return new ResolvedUnitCost(saleLineCost, MarginCostSource.Historical);
        }

        if (IsReliableCost(snapshotCost))
        {
            return new ResolvedUnitCost(snapshotCost, MarginCostSource.SnapshotFallback);
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

public static class MarginQualityClassifier
{
    public sealed record MarginQualityResult(
        string Tier,
        string Label,
        string ShortLabel,
        string Tooltip);

    /// <summary>
    /// Classifies margin quality into one of four tiers based on cost coverage breakdown.
    /// </summary>
    /// <param name="historicalCoveragePct">% of revenue with historical (sale-line) cost</param>
    /// <param name="estimatedCoveragePct">% of revenue with fallback (product-level) cost</param>
    /// <param name="noCostCoveragePct">% of revenue with no cost at all</param>
    /// <param name="totalCoveragePct">% of revenue with any cost (historical + estimated)</param>
    public static MarginQualityResult Classify(
        double historicalCoveragePct,
        double estimatedCoveragePct,
        double noCostCoveragePct,
        double totalCoveragePct)
    {
        // Tier 1: Historically confirmed — ≥80% historical cost
        if (historicalCoveragePct >= 80d)
        {
            return new MarginQualityResult(
                Tier: "confirmed",
                Label: "Istorijski potvrđena",
                ShortLabel: "Potvrđena",
                Tooltip: $"Nabavna cena je preuzeta sa prodajne stavke za {historicalCoveragePct:0.#}% prometa. Signal marže je pouzdan.");
        }

        // Tier 2: Partially estimated — ≥50% historical, rest is fallback
        if (historicalCoveragePct >= 50d)
        {
            return new MarginQualityResult(
                Tier: "partial",
                Label: "Delimično procenjena",
                ShortLabel: "Delimično",
                Tooltip: $"Istorijski trošak pokriva {historicalCoveragePct:0.#}% prometa, a za {estimatedCoveragePct:0.#}% se koristi procena iz troška artikla. Signal marže je umereno pouzdan.");
        }

        // Tier 3: Dominantly estimated — <50% historical but >0% total coverage
        if (totalCoveragePct > 0d)
        {
            return new MarginQualityResult(
                Tier: "estimated",
                Label: "Dominantno procenjena",
                ShortLabel: "Procenjena",
                Tooltip: $"Istorijski trošak pokriva samo {historicalCoveragePct:0.#}% prometa. Za {estimatedCoveragePct:0.#}% prometa trošak je procenjen iz artikla. Marže nisu istorijski stabilne i mogu se promeniti pri novom unosu nabavne cene.");
        }

        // Tier 4: Low confidence — no cost data at all
        return new MarginQualityResult(
            Tier: "no_data",
            Label: "Nedovoljno pokriveno",
            ShortLabel: "Bez troška",
            Tooltip: "Nabavna cena nije dostupna ni na prodajnoj stavci ni na artiklu. Marža se ne može obračunati.");
    }

    public static MarginQualityResult ClassifyFromSnapshot(MarginSnapshot snapshot, decimal totalRevenue)
    {
        var historical = snapshot.HistoricalMarginCoveragePct ?? 0d;
        var estimated = (snapshot.FallbackCostCoveragePct ?? 0d) + (snapshot.SnapshotCostCoveragePct ?? 0d);
        var total = snapshot.MarginDataCoveragePct ?? 0d;
        var noCost = totalRevenue > 0m
            ? Math.Round((double)((totalRevenue - snapshot.RevenueWithCost) / totalRevenue * 100m), 2)
            : 0d;

        return Classify(historical, estimated, noCost, total);
    }
}
