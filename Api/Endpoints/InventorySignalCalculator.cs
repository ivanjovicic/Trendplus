using System.Globalization;

namespace Trendplus2.Endpoints;

public static class InventorySignalCalculator
{
    public const string StockCoverLow = "low";
    public const string StockCoverHealthy = "healthy";
    public const string StockCoverHigh = "high";
    public const string StockCoverSlow = "slow";
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
        decimal? SellThroughRatio,
        string SellThroughStatus,
        decimal SignalConfidencePct,
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
        var sellThrough = CalculateSellThrough(soldUnits, openingStockUnits, inboundUnits, currentOnHandUnits, hasSufficientData, reasonCodes, out var usedFallback);
        var confidence = CalculateSignalConfidence(stockCover.Status, sellThrough.Status, dataQualityStatus, usedFallback, hasSufficientData);

        return new SignalResult(
            stockCover.Days,
            stockCover.Status,
            sellThrough.Ratio,
            sellThrough.Status,
            confidence,
            reasonCodes);
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
            return (0m, StockCoverOutOfStockRisk);
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
            <= 60m => StockCoverHigh,
            _ => StockCoverSlow,
        };

        reasonCodes.Add($"stock_cover_status:{status}");
        return (days, status);
    }

    private static (decimal? Ratio, string Status) CalculateSellThrough(
        int soldUnits,
        int? openingStockUnits,
        int? inboundUnits,
        int currentOnHandUnits,
        bool hasSufficientData,
        List<string> reasonCodes,
        out bool usedFallback)
    {
        usedFallback = false;

        if (!hasSufficientData)
        {
            reasonCodes.Add("sell_through_insufficient_data");
            return (null, SellThroughInsufficientData);
        }

        var hasOpeningInbound = openingStockUnits.HasValue || inboundUnits.HasValue;
        decimal denominator;

        if (hasOpeningInbound)
        {
            denominator = Math.Max(openingStockUnits ?? 0, 0) + Math.Max(inboundUnits ?? 0, 0);
        }
        else
        {
            denominator = Math.Max(soldUnits, 0) + Math.Max(currentOnHandUnits, 0);
            usedFallback = true;
            reasonCodes.Add("sell_through_fallback_current_plus_sold");
        }

        if (denominator <= 0)
        {
            reasonCodes.Add("sell_through_denominator_zero");
            return (null, SellThroughInsufficientData);
        }

        var ratio = Math.Round(Math.Max(soldUnits, 0) / denominator, 4, MidpointRounding.AwayFromZero);
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
        bool usedSellThroughFallback,
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
        else if (stockCoverStatus is StockCoverOutOfStockRisk or StockCoverSlow)
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

        if (usedSellThroughFallback)
        {
            confidence -= 12m;
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
            StockCoverHigh => "Visoka pokrivenost",
            StockCoverSlow => "Spor obrt zalihe",
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
