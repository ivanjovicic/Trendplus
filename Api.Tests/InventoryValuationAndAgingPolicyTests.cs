using Application.Analytics;
using Domain.Model;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class InventoryValuationAndAgingPolicyTests
{
    [Fact(DisplayName = "Synthetic import Ulaz robe is excluded from inbound evidence")]
    public void SyntheticImportReceipt_IsDetected()
    {
        Assert.True(InventoryValuationAndAgingPolicy.IsSyntheticImportReceipt(
            TipPromeneConstants.UlazRobe,
            iznos: 0m,
            kolicina: 10,
            dataOrigin: "access"));
        Assert.False(InventoryValuationAndAgingPolicy.IsSyntheticImportReceipt(
            TipPromeneConstants.UlazRobe,
            iznos: 1000m,
            kolicina: 10,
            dataOrigin: "access"));
    }

    [Fact(DisplayName = "Master 245 vs sale 3000 uses estimated sale-line fallback")]
    public void Valuation_PrefersInboundThenSaleFallback()
    {
        var valuation = InventoryValuationAndAgingPolicy.ResolveArticleValuation(
            quantity: 5,
            latestInboundUnitCost: null,
            latestSaleLineUnitCost: 3000m);

        Assert.Equal(InventoryValuationBases.EstimatedFromSaleCost, valuation.ValuationBasis);
        Assert.True(valuation.IsEstimated);
        Assert.Equal(3000m, valuation.UnitCost);
    }

    [Fact(DisplayName = "Aggregate valuation reports coverage and excludes unknown units")]
    public void AggregateValuation_ReportsCoverage()
    {
        var aggregate = InventoryValuationAndAgingPolicy.AggregateValuation([
            (5, new InventoryArticleValuation(3000m, InventoryValuationBases.EstimatedFromSaleCost, true)),
            (2, new InventoryArticleValuation(null, InventoryValuationBases.Unknown, false)),
        ]);

        Assert.Equal(15000m, aggregate.TotalValue);
        Assert.Equal(71.43m, aggregate.ValueCoveragePct);
        Assert.Equal(2, aggregate.UnknownValueUnits);
    }

    [Fact(DisplayName = "Aging without reliable receipt is unknown")]
    public void Aging_WithoutReceipt_IsUnknown()
    {
        var aging = InventoryValuationAndAgingPolicy.ResolveAgingFromReceipt(null);
        Assert.Equal("unknown", aging.Bucket);
        Assert.Equal(InventoryAgeBases.Unknown, aging.AgeBasis);
        Assert.Null(aging.DaysSinceMovement);
    }

    [Fact(DisplayName = "Master cost scale suspicion flags low master vs sale line")]
    public void MasterCostScale_IsSuspicious()
    {
        Assert.True(InventoryValuationAndAgingPolicy.IsMasterCostScaleSuspicious(245m, 3038m));
        Assert.False(InventoryValuationAndAgingPolicy.IsMasterCostScaleSuspicious(2500m, 3038m));
    }
}
