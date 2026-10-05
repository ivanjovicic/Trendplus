using Domain.Model;

namespace Application.Analytics;

public static class InventoryValuationBases
{
    public const string InboundReceiptUnitCost = "inbound_receipt_unit_cost";
    public const string EstimatedFromSaleCost = "estimated_from_sale_cost";
    public const string Unknown = "unknown";
}

public static class InventoryAgeBases
{
    public const string LastInboundReceipt = "last_inbound_receipt";
    public const string Unknown = "unknown";
}

public sealed record InventoryArticleValuation(
    decimal? UnitCost,
    string ValuationBasis,
    bool IsEstimated);

public sealed record InventoryValuationAggregate(
    decimal? TotalValue,
    decimal ValueCoveragePct,
    int ValuedUnits,
    int UnknownValueUnits,
    string ValuationBasisSummary);

public static class InventoryValuationAndAgingPolicy
{
    public const int DefaultLowStockThreshold = 2;

    public static bool IsSyntheticImportReceipt(
        string? tipPromene,
        decimal iznos,
        int? kolicina,
        string? dataOrigin)
    {
        if (!string.Equals(tipPromene, TipPromeneConstants.UlazRobe, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(dataOrigin, "access", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return iznos == 0m && (kolicina is null or >= 0);
    }

    public static bool IsReliableInboundMovement(string? tipPromene, decimal iznos, int? kolicina)
        => !string.IsNullOrWhiteSpace(tipPromene)
           && TipPromeneConstants.UlazTypes.Contains(tipPromene, StringComparer.OrdinalIgnoreCase)
           && (kolicina ?? 0) > 0
           && iznos > 0m;

    public static decimal? ResolveInboundUnitCost(decimal iznos, int? kolicina)
    {
        if ((kolicina ?? 0) <= 0 || iznos <= 0m)
        {
            return null;
        }

        return Math.Round(iznos / Math.Abs(kolicina!.Value), 4, MidpointRounding.AwayFromZero);
    }

    public static InventoryArticleValuation ResolveArticleValuation(
        int? quantity,
        decimal? latestInboundUnitCost,
        decimal? latestSaleLineUnitCost)
    {
        if (latestInboundUnitCost is > 0m)
        {
            return new InventoryArticleValuation(
                latestInboundUnitCost,
                InventoryValuationBases.InboundReceiptUnitCost,
                IsEstimated: false);
        }

        if (latestSaleLineUnitCost is > 0m)
        {
            return new InventoryArticleValuation(
                latestSaleLineUnitCost,
                InventoryValuationBases.EstimatedFromSaleCost,
                IsEstimated: true);
        }

        return new InventoryArticleValuation(null, InventoryValuationBases.Unknown, IsEstimated: false);
    }

    public static InventoryValuationAggregate AggregateValuation(
        IEnumerable<(int? Quantity, InventoryArticleValuation Valuation)> rows)
    {
        var materialized = rows.ToList();
        var onHandUnits = materialized.Sum(row => InventoryStockEvidence.MeasuredOnHandUnits(row.Quantity));
        if (onHandUnits <= 0)
        {
            return new InventoryValuationAggregate(0m, 0m, 0, 0, InventoryValuationBases.Unknown);
        }

        decimal totalValue = 0m;
        var valuedUnits = 0;
        var unknownUnits = 0;
        var inboundUnits = 0;
        var estimatedUnits = 0;

        foreach (var row in materialized)
        {
            var quantity = row.Quantity ?? 0;
            if (quantity <= 0)
            {
                continue;
            }

            var lineValue = InventoryStockEvidence.ComputeEstimatedValue(quantity, row.Valuation.UnitCost);
            if (lineValue is null)
            {
                unknownUnits += quantity;
                continue;
            }

            totalValue += lineValue.Value;
            valuedUnits += quantity;
            if (row.Valuation.ValuationBasis == InventoryValuationBases.InboundReceiptUnitCost)
            {
                inboundUnits += quantity;
            }
            else if (row.Valuation.ValuationBasis == InventoryValuationBases.EstimatedFromSaleCost)
            {
                estimatedUnits += quantity;
            }
        }

        var coveragePct = Math.Round((decimal)valuedUnits / onHandUnits * 100m, 2);
        var summary = inboundUnits >= estimatedUnits && inboundUnits > 0
            ? InventoryValuationBases.InboundReceiptUnitCost
            : estimatedUnits > 0
                ? InventoryValuationBases.EstimatedFromSaleCost
                : valuedUnits > 0
                    ? InventoryValuationBases.EstimatedFromSaleCost
                    : InventoryValuationBases.Unknown;

        return new InventoryValuationAggregate(
            Math.Round(totalValue, 2),
            coveragePct,
            valuedUnits,
            unknownUnits,
            summary);
    }

    public static bool IsMasterCostScaleSuspicious(decimal? masterCost, decimal? saleLineUnitCost)
    {
        if (masterCost is null or <= 0m || saleLineUnitCost is null or <= 0m)
        {
            return false;
        }

        return masterCost.Value < saleLineUnitCost.Value * 0.25m;
    }

    public static (string Bucket, string Label, int? DaysSinceMovement, string AgeBasis) ResolveAgingFromReceipt(
        DateTime? lastRealReceiptAtUtc)
    {
        if (!lastRealReceiptAtUtc.HasValue)
        {
            return ("unknown", "Nepoznato", null, InventoryAgeBases.Unknown);
        }

        var days = Math.Max((DateTime.UtcNow.Date - lastRealReceiptAtUtc.Value.Date).Days, 0);
        var (bucket, label) = days switch
        {
            <= 30 => ("0-30", "0-30 dana"),
            <= 60 => ("31-60", "31-60 dana"),
            <= 90 => ("61-90", "61-90 dana"),
            _ => ("90+", "90+ dana")
        };

        return (bucket, label, days, InventoryAgeBases.LastInboundReceipt);
    }
}
