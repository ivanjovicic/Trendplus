using Application.Analytics;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public sealed class InventoryActionDecisionPolicyTests
{
    [Fact]
    public void StaleSourceHorizon_ProducesSameSignalWindowIndependentOfWallClock()
    {
        var horizon = new DateTime(2026, 8, 8, 13, 30, 0, DateTimeKind.Utc);
        var oldClock = InventoryActionDecisionPolicy.ResolveSignalWindow(horizon.Date.AddDays(1), horizon);
        var currentClock = InventoryActionDecisionPolicy.ResolveSignalWindow(
            new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc), horizon);

        Assert.Equal(oldClock.FromUtc, currentClock.FromUtc);
        Assert.Equal(oldClock.ToExclusiveUtc, currentClock.ToExclusiveUtc);
        Assert.Equal(oldClock.AsOfUtc, currentClock.AsOfUtc);
        Assert.Equal("source_horizon", currentClock.Basis);
    }

    [Fact]
    public void RecentReceiptAndNoSales_DoesNotQualifyForClearanceByAge()
    {
        var age = InventoryActionDecisionPolicy.ResolveReceiptAgeDays(
            new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 8, 23, 59, 0, DateTimeKind.Utc));

        Assert.Equal(7, age);
        Assert.False(InventoryActionDecisionPolicy.IsClearanceEligible(20, 5, age, soldUnits: 0));
    }

    [Fact]
    public void OldStockAndNoSales_QualifiesWithKnownReceiptAge()
    {
        Assert.True(InventoryActionDecisionPolicy.IsSlowStockActionEligible(20, 5, 75, soldUnits: 0));
        Assert.True(InventoryActionDecisionPolicy.IsClearanceEligible(20, 5, 105, soldUnits: 0));
        Assert.True(InventoryActionDecisionPolicy.IsClearanceEligible(20, 5, 105, soldUnits: 1));
        Assert.False(InventoryActionDecisionPolicy.IsSlowStockActionEligible(100, 5, 75, soldUnits: 60));
        Assert.False(InventoryActionDecisionPolicy.IsSlowStockActionEligible(20, 5, 95, soldUnits: 0));
        Assert.False(InventoryActionDecisionPolicy.IsClearanceEligible(20, 5, null, soldUnits: 0));
    }

    [Fact]
    public void TransferRequiresMaterialDemandAdvantageAndPreservesMinimumOrWeeklyCover()
    {
        Assert.True(InventoryActionDecisionPolicy.HasMateriallyStrongerDemand(4, 5));
        Assert.False(InventoryActionDecisionPolicy.HasMateriallyStrongerDemand(4, 4));
        Assert.False(InventoryActionDecisionPolicy.HasMateriallyStrongerDemand(4, 3));
        Assert.Equal(3, InventoryActionDecisionPolicy.SafeSourceMinimum(sourceMinimum: 2, sourceDemandUnits: 9));
        Assert.Equal(7, InventoryActionDecisionPolicy.SafeSourceMinimum(sourceMinimum: 2, sourceDemandUnits: 30));
        Assert.True(InventoryActionDecisionPolicy.CanTransfer(1, "A", 2, "B", 4, 5, 15, 8, 7));
        Assert.False(InventoryActionDecisionPolicy.CanTransfer(1, "A", 2, "B", 4, 4, 15, 8, 7));
        Assert.False(InventoryActionDecisionPolicy.CanTransfer(1, "A", 2, "B", 4, 5, 15, 9, 7));
        Assert.False(InventoryActionDecisionPolicy.CanTransfer(1, null, 2, "B", 4, 5, 15, 8, 7));
    }

    [Fact]
    public void Replenishment_RequiresConfiguredMinimumOrObservedSales()
    {
        Assert.True(InventoryActionDecisionPolicy.IsReplenishmentEligible(0, 0, soldUnits: 3));
        Assert.True(InventoryActionDecisionPolicy.IsReplenishmentEligible(0, 5, soldUnits: 0));
        Assert.True(InventoryActionDecisionPolicy.IsReplenishmentEligible(2, 5, soldUnits: 0));
        Assert.False(InventoryActionDecisionPolicy.IsReplenishmentEligible(0, 0, soldUnits: 0));
        Assert.False(InventoryActionDecisionPolicy.IsReplenishmentEligible(-1, 0, soldUnits: 0));
        Assert.False(InventoryActionDecisionPolicy.IsReplenishmentEligible(6, 5, soldUnits: 10));
    }
}
