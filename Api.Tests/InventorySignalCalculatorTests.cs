using Trendplus2.Endpoints;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class InventorySignalCalculatorTests
{
    [Fact(DisplayName = "Stock cover uses on-hand / avg daily sales")]
    public void Calculate_StockCoverDays_FromOnHandAndVelocity()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 100,
            avgDailySalesUnits: 10m,
            soldUnits: 40,
            openingStockUnits: 120,
            inboundUnits: 20,
            dataQualityStatus: "good",
            hasSufficientData: true);

        Assert.Equal(10m, result.StockCoverDays);
        Assert.Equal(InventorySignalCalculator.StockCoverHealthy, result.StockCoverStatus);
    }

    [Fact(DisplayName = "Positive stock with zero velocity maps to no_velocity")]
    public void Calculate_NoVelocity_WhenStockPositiveAndVelocityZero()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 25,
            avgDailySalesUnits: 0m,
            soldUnits: 0,
            openingStockUnits: 20,
            inboundUnits: 0,
            dataQualityStatus: "good",
            hasSufficientData: true);

        Assert.Null(result.StockCoverDays);
        Assert.Equal(InventorySignalCalculator.StockCoverNoVelocity, result.StockCoverStatus);
    }

    [Fact(DisplayName = "Zero stock with positive velocity maps to out_of_stock_risk")]
    public void Calculate_OosRisk_WhenStockZeroAndVelocityPositive()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 0,
            avgDailySalesUnits: 2m,
            soldUnits: 8,
            openingStockUnits: 10,
            inboundUnits: 2,
            dataQualityStatus: "warning",
            hasSufficientData: true);

        Assert.Equal(0m, result.StockCoverDays);
        Assert.Equal(InventorySignalCalculator.StockCoverOutOfStockRisk, result.StockCoverStatus);
    }

    [Fact(DisplayName = "Insufficient data maps both signals to insufficient_data")]
    public void Calculate_InsufficientData_WhenSignalIsNotReliable()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 15,
            avgDailySalesUnits: 1.5m,
            soldUnits: 3,
            openingStockUnits: 20,
            inboundUnits: 4,
            dataQualityStatus: "insufficient_data",
            hasSufficientData: false);

        Assert.Equal(InventorySignalCalculator.StockCoverInsufficientData, result.StockCoverStatus);
        Assert.Equal(InventorySignalCalculator.SellThroughInsufficientData, result.SellThroughStatus);
    }

    [Fact(DisplayName = "Sell-through denominator zero maps to insufficient_data")]
    public void Calculate_SellThroughInsufficient_WhenDenominatorIsZero()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 0,
            avgDailySalesUnits: 1m,
            soldUnits: 0,
            openingStockUnits: 0,
            inboundUnits: 0,
            dataQualityStatus: "good",
            hasSufficientData: true);

        Assert.Null(result.SellThroughRatio);
        Assert.Equal(InventorySignalCalculator.SellThroughInsufficientData, result.SellThroughStatus);
        Assert.Contains("sell_through_denominator_zero", result.ReasonCodes);
    }
}
