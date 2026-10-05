using Application.Analytics;

namespace Api.Services;

public sealed record VendorSalesNivelacijaOutcomeSaleLineFact(
    int SaleLineId,
    int Quantity,
    decimal Price,
    decimal? SaleLineCost,
    decimal? ProductCostRsd,
    decimal? ProductCostLegacy);

public sealed record VendorSalesNivelacijaOutcomePeriodEvidence(
    decimal? Units,
    decimal? Revenue,
    decimal? CostCoveragePct,
    decimal? MarginContribution);

public static class VendorSalesNivelacijaOutcomeEvidencePolicy
{
    public static VendorSalesNivelacijaOutcomePeriodEvidence CalculatePeriod(
        IReadOnlyCollection<VendorSalesNivelacijaOutcomeSaleLineFact> rows,
        IReadOnlyDictionary<int, decimal?> snapshotCosts,
        bool emptyWindowIsZero)
    {
        if (rows.Count == 0)
        {
            return emptyWindowIsZero
                ? new VendorSalesNivelacijaOutcomePeriodEvidence(0m, 0m, null, null)
                : new VendorSalesNivelacijaOutcomePeriodEvidence(null, null, null, null);
        }

        var revenue = rows.Sum(row => row.Price * row.Quantity);
        decimal coveredRevenue = 0m;
        decimal margin = 0m;
        var allCovered = true;
        foreach (var row in rows)
        {
            snapshotCosts.TryGetValue(row.SaleLineId, out var snapshot);
            var cost = AnalyticsMarginPolicy.ResolveUnitCostWithSnapshot(
                row.SaleLineCost,
                snapshot,
                row.ProductCostRsd,
                row.ProductCostLegacy).UnitCost;
            if (cost is null)
            {
                allCovered = false;
                continue;
            }

            coveredRevenue += row.Price * row.Quantity;
            margin += (row.Price - cost.Value) * row.Quantity;
        }

        var coverage = revenue > 0m ? decimal.Round(coveredRevenue / revenue * 100m, 2) : (decimal?)null;
        return new VendorSalesNivelacijaOutcomePeriodEvidence(
            rows.Sum(row => (decimal)row.Quantity),
            revenue,
            coverage,
            allCovered ? margin : null);
    }
}
