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
            openingStockUnits: 130,
            inboundUnits: 20,
            dataQualityStatus: "good",
            hasSufficientData: true);

        Assert.Equal(10m, result.StockCoverDays);
        Assert.Equal(InventorySignalCalculator.StockCoverHealthy, result.StockCoverStatus);
        Assert.Equal("Zdrava pokrivenost", result.StockCoverStatusLabel);
        Assert.Equal(0.2667m, result.SellThroughRatio);
        Assert.Equal("Kritičan sell-through", result.SellThroughStatusLabel);
        Assert.True(result.RecommendationAllowed);
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
        Assert.Equal("Bez rotacije", result.StockCoverStatusLabel);
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

        Assert.Null(result.StockCoverDays);
        Assert.Equal(InventorySignalCalculator.StockCoverOutOfStockRisk, result.StockCoverStatus);
        Assert.Equal("Rizik rasprodaje", result.StockCoverStatusLabel);
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
        Assert.False(result.RecommendationAllowed);
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
        Assert.Equal("Nedovoljno podataka", result.SellThroughStatusLabel);
        Assert.False(result.RecommendationAllowed);
        Assert.Contains("sell_through_denominator_zero", result.ReasonCodes);
    }

    [Fact(DisplayName = "Missing sell-through denominator maps to insufficient_data without fallback")]
    public void Calculate_SellThroughInsufficient_WhenOpeningAndInboundMissing()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 12,
            avgDailySalesUnits: 2m,
            soldUnits: 30,
            openingStockUnits: null,
            inboundUnits: null,
            dataQualityStatus: "warning",
            hasSufficientData: true);

        Assert.Null(result.SellThroughRatio);
        Assert.Equal(InventorySignalCalculator.SellThroughInsufficientData, result.SellThroughStatus);
        Assert.False(result.RecommendationAllowed);
        Assert.Contains("sell_through_insufficient_denominator_data", result.ReasonCodes);
    }

    [Fact(DisplayName = "Opening-only missing sell-through denominator stays unavailable")]
    public void Calculate_SellThroughInsufficient_WhenOnlyOpeningMissing()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 12,
            avgDailySalesUnits: 2m,
            soldUnits: 30,
            openingStockUnits: null,
            inboundUnits: 20,
            dataQualityStatus: "good",
            hasSufficientData: true);

        Assert.Null(result.SellThroughRatio);
        Assert.Equal(InventorySignalCalculator.SellThroughInsufficientData, result.SellThroughStatus);
        Assert.False(result.RecommendationAllowed);
        Assert.Contains("sell_through_insufficient_denominator_data", result.ReasonCodes);
    }

    [Fact(DisplayName = "Inbound-only missing sell-through denominator stays unavailable")]
    public void Calculate_SellThroughInsufficient_WhenOnlyInboundMissing()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 12,
            avgDailySalesUnits: 2m,
            soldUnits: 30,
            openingStockUnits: 100,
            inboundUnits: null,
            dataQualityStatus: "good",
            hasSufficientData: true);

        Assert.Null(result.SellThroughRatio);
        Assert.Equal(InventorySignalCalculator.SellThroughInsufficientData, result.SellThroughStatus);
        Assert.False(result.RecommendationAllowed);
        Assert.Contains("sell_through_insufficient_denominator_data", result.ReasonCodes);
    }

    [Fact(DisplayName = "Genuine zero sold units with valid denominator remains measured zero")]
    public void Calculate_SellThroughZeroSold_WithValidDenominator()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 50,
            avgDailySalesUnits: 0m,
            soldUnits: 0,
            openingStockUnits: 40,
            inboundUnits: 10,
            dataQualityStatus: "good",
            hasSufficientData: true);

        Assert.Equal(0m, result.SellThroughRatio);
        Assert.Equal(InventorySignalCalculator.SellThroughCritical, result.SellThroughStatus);
        Assert.Contains("sell_through_status:critical", result.ReasonCodes);
    }

    [Fact(DisplayName = "Negative denominator inputs fail closed")]
    public void Calculate_SellThroughInsufficient_WhenDenominatorInputsNegative()
    {
        var result = InventorySignalCalculator.Calculate(
            currentOnHandUnits: 12,
            avgDailySalesUnits: 2m,
            soldUnits: 5,
            openingStockUnits: -1,
            inboundUnits: 10,
            dataQualityStatus: "good",
            hasSufficientData: true);

        Assert.Null(result.SellThroughRatio);
        Assert.Equal(InventorySignalCalculator.SellThroughInsufficientData, result.SellThroughStatus);
        Assert.False(result.RecommendationAllowed);
        Assert.Contains("sell_through_invalid_denominator_input", result.ReasonCodes);
    }

    [Fact(DisplayName = "Signal labels are Serbian and UTF-8 safe")]
    public void StatusLabels_AreSerbianAndUtf8Safe()
    {
        Assert.Equal("Rizik rasprodaje", InventorySignalCalculator.StockCoverStatusLabel(InventorySignalCalculator.StockCoverOutOfStockRisk));
        Assert.Equal("Kritičan sell-through", InventorySignalCalculator.SellThroughStatusLabel(InventorySignalCalculator.SellThroughCritical));
        Assert.DoesNotContain("Ä", InventorySignalCalculator.StockCoverStatusLabel(InventorySignalCalculator.StockCoverOutOfStockRisk));
        Assert.DoesNotContain("�", InventorySignalCalculator.SellThroughStatusLabel(InventorySignalCalculator.SellThroughCritical));
    }
}
