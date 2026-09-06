using Application.Analytics;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class InventoryStockEvidenceTests
{
    [Fact(DisplayName = "Null quantity is not measured OOS")]
    public void NullQuantity_IsNotOutOfStock()
    {
        Assert.False(InventoryStockEvidence.IsMeasuredOutOfStock(null));
        Assert.True(InventoryStockEvidence.IsMeasuredOutOfStock(0));
        Assert.False(InventoryStockEvidence.IsMeasuredOutOfStock(1));
    }

    [Fact(DisplayName = "Null quantity is not low stock against threshold")]
    public void NullQuantity_IsNotLowStock()
    {
        Assert.False(InventoryStockEvidence.IsMeasuredLowStock(null, lowStockThreshold: 2));
        Assert.False(InventoryStockEvidence.IsMeasuredLowStock(0, lowStockThreshold: 2));
        Assert.True(InventoryStockEvidence.IsMeasuredLowStock(1, lowStockThreshold: 2));
        Assert.False(InventoryStockEvidence.IsMeasuredLowStock(5, lowStockThreshold: 2));
    }

    [Fact(DisplayName = "Null minimum cannot classify low stock")]
    public void NullMinimum_IsNotLowStockAgainstMinimum()
    {
        Assert.False(InventoryStockEvidence.IsMeasuredLowStockAgainstMinimum(5, minimum: null));
        Assert.True(InventoryStockEvidence.IsMeasuredLowStockAgainstMinimum(2, minimum: 5));
        Assert.False(InventoryStockEvidence.IsMeasuredLowStockAgainstMinimum(10, minimum: 5));
        Assert.False(InventoryStockEvidence.IsMeasuredLowStockAgainstMinimum(0, minimum: 5));
    }

    [Fact(DisplayName = "Estimated value stays unavailable without quantity or cost")]
    public void EstimatedValue_PreservesUnknownEvidence()
    {
        Assert.Null(InventoryStockEvidence.ComputeEstimatedValue(null, 100m));
        Assert.Null(InventoryStockEvidence.ComputeEstimatedValue(5, null));
        Assert.Equal(0m, InventoryStockEvidence.ComputeEstimatedValue(0, null));
        Assert.Equal(500m, InventoryStockEvidence.ComputeEstimatedValue(5, 100m));
    }
}
