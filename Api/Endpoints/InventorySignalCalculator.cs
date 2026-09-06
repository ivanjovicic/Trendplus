using System.Globalization;

namespace Trendplus2.Endpoints;

public static class InventorySignalCalculator
{
    public const string StockCoverLow = "low_cover";
    public const string StockCoverHealthy = "healthy";
    public const string StockCoverOverstock = "overstock";
    public const string StockCoverSlowStock = "slow_stock";
    public const string StockCoverNoVelocity = "no_velocity";
    public const string StockCoverOutOfStockRisk = "out_of_stock_risk";
    public const string StockCoverInsufficientData = "insufficient_data";

    public const string SellThroughGood = "good";
    public const string SellThroughWarning = "warning";
    public const string SellThroughCritical = "critical";
    public const string SellThroughInsufficientData = "insufficient_data";

    public sealed record SignalResult(
        decimal? StockCoverDays,
        string StockCoverStatus,
        string StockCoverStatusLabel,
        decimal? SellThroughRatio,
        string SellThroughStatus,
        string SellThroughStatusLabel,
        decimal SignalConfidencePct,
        bool RecommendationAllowed,
        IReadOnlyList<string> ReasonCodes);

    public static SignalResult Calculate(
        int currentOnHandUnits,
        decimal avgDailySalesUnits,
        int soldUnits,
        int? openingStockUnits,
        int? inboundUnits,
        string dataQualityStatus,
        bool hasSufficientData)
    {
        var reasonCodes = new List<string>();

        var stockCover = CalculateStockCover(currentOnHandUnits, avgDailySalesUnits, hasSufficientData, reasonCodes);
        var sellThrough = CalculateSellThrough(soldUnits, openingStockUnits, inboundUnits, hasSufficientData, reasonCodes);
        var confidence = CalculateSignalConfidence(stockCover.Status, sellThrough.Status, dataQualityStatus, hasSufficientData);

        return new SignalResult(
            stockCover.Days,
            stockCover.Status,
            StockCoverStatusLabel(stockCover.Status),
            sellThrough.Ratio,
            sellThrough.Status,
            SellThroughStatusLabel(sellThrough.Status),
            confidence,
            ResolveRecommendationAllowed(stockCover.Status, sellThrough.Status, hasSufficientData),
            reasonCodes);
    }

    private static bool ResolveRecommendationAllowed(
        string stockCoverStatus,
        string sellThroughStatus,
        bool hasSufficientData)
    {
        if (!hasSufficientData)
        {
            return false;
        }

        if (stockCoverStatus == StockCoverInsufficientData)
        {
            return false;
        }

        if (sellThroughStatus == SellThroughInsufficientData)
        {
            return false;
        }

        return true;
    }

    private static (decimal? Days, string Status) CalculateStockCover(
        int currentOnHandUnits,
        decimal avgDailySalesUnits,
        bool hasSufficientData,
        List<string> reasonCodes)
    {
        if (!hasSufficientData)
        {
            reasonCodes.Add("stock_cover_insufficient_data");
            return (null, StockCoverInsufficientData);
        }

        if (currentOnHandUnits <= 0 && avgDailySalesUnits > 0)
        {
            reasonCodes.Add("stock_cover_out_of_stock_risk");
            return (null, StockCoverOutOfStockRisk);
        }

        if (avgDailySalesUnits <= 0 && currentOnHandUnits > 0)
        {
            reasonCodes.Add("stock_cover_no_velocity");
            return (null, StockCoverNoVelocity);
        }

        if (avgDailySalesUnits <= 0)
        {
            reasonCodes.Add("stock_cover_insufficient_data");
            return (null, StockCoverInsufficientData);
        }

        var days = Math.Round(currentOnHandUnits / avgDailySalesUnits, 2, MidpointRounding.AwayFromZero);
        var status = days switch
        {
            <= 7m => StockCoverLow,
            <= 30m => StockCoverHealthy,
            <= 60m => StockCoverOverstock,
            _ => StockCoverSlowStock,
        };

        reasonCodes.Add($"stock_cover_status:{status}");
        return (days, status);
    }

    private static (decimal? Ratio, string Status) CalculateSellThrough(
        int soldUnits,
        int? openingStockUnits,
        int? inboundUnits,
        bool hasSufficientData,
        List<string> reasonCodes)
    {
        if (!hasSufficientData)
        {
            reasonCodes.Add("sell_through_insufficient_data");
            return (null, SellThroughInsufficientData);
        }

        // Both opening and inbound are required evidence (see analyticsMetricDefinitions sellThrough).
        // A single missing component must not coalesce to zero and invent a ratio.
        if (!openingStockUnits.HasValue || !inboundUnits.HasValue)
        {
            reasonCodes.Add("sell_through_insufficient_denominator_data");
            return (null, SellThroughInsufficientData);
        }

        if (openingStockUnits.Value < 0 || inboundUnits.Value < 0 || soldUnits < 0)
        {
            reasonCodes.Add("sell_through_invalid_denominator_input");
            return (null, SellThroughInsufficientData);
        }

        decimal denominator = openingStockUnits.Value + inboundUnits.Value;

        if (denominator <= 0)
        {
            reasonCodes.Add("sell_through_denominator_zero");
            return (null, SellThroughInsufficientData);
        }

        var ratio = Math.Round((decimal)soldUnits / denominator, 4, MidpointRounding.AwayFromZero);
        var status = ratio switch
        {
            >= 0.60m => SellThroughGood,
            >= 0.30m => SellThroughWarning,
            _ => SellThroughCritical,
        };

        reasonCodes.Add($"sell_through_status:{status}");
        return (ratio, status);
    }

    private static decimal CalculateSignalConfidence(
        string stockCoverStatus,
        string sellThroughStatus,
        string dataQualityStatus,
        bool hasSufficientData)
    {
        if (!hasSufficientData)
        {
            return 35m;
        }

        var confidence = 80m;

        if (stockCoverStatus is StockCoverInsufficientData or StockCoverNoVelocity)
        {
            confidence -= 25m;
        }
        else if (stockCoverStatus is StockCoverOutOfStockRisk or StockCoverSlowStock)
        {
            confidence -= 10m;
        }

        if (sellThroughStatus == SellThroughInsufficientData)
        {
            confidence -= 25m;
        }
        else if (sellThroughStatus == SellThroughWarning)
        {
            confidence -= 8m;
        }

        var normalizedQuality = (dataQualityStatus ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedQuality == "critical")
        {
            confidence = Math.Min(confidence, 40m);
        }
        else if (normalizedQuality == "warning")
        {
            confidence = Math.Min(confidence, 65m);
        }
        else if (normalizedQuality == "insufficient_data")
        {
            confidence = Math.Min(confidence, 45m);
        }

        return Math.Clamp(Math.Round(confidence, 2, MidpointRounding.AwayFromZero), 5m, 99m);
    }

    public static string StockCoverStatusLabel(string status)
    {
        return status switch
        {
            StockCoverLow => "Niska pokrivenost",
            StockCoverHealthy => "Zdrava pokrivenost",
            StockCoverOverstock => "Prekomerna zaliha",
            StockCoverSlowStock => "Spor obrt zalihe",
            StockCoverNoVelocity => "Bez rotacije",
            StockCoverOutOfStockRisk => "Rizik rasprodaje",
            _ => "Nedovoljno podataka",
        };
    }

    public static string SellThroughStatusLabel(string status)
    {
        return status switch
        {
            SellThroughGood => "Dobar sell-through",
            SellThroughWarning => "Upozorenje sell-through",
            SellThroughCritical => "Kritičan sell-through",
            _ => "Nedovoljno podataka",
        };
    }
}
