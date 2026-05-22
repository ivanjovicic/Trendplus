namespace Application.Analytics;

public static class ProductDecisionReasoningHelper
{
    public static class ReasonCodes
    {
        public const string HighVelocity = "high_velocity";
        public const string LowStock = "low_stock";
        public const string PoorMargin = "poor_margin";
        public const string StaleStock = "stale_stock";
        public const string MissingCost = "missing_cost";
        public const string MissingSupplier = "missing_supplier";
        public const string InsufficientHistory = "insufficient_history";
        public const string ReplenishNeeded = "replenish_needed";
        public const string HighStockRisk = "high_stock_risk";
        public const string DataQualityBlocker = "data_quality_blocker";
    }

    public sealed record Input(
        bool MissingSupplier,
        bool MissingCost,
        bool MissingCategory,
        bool MissingVariantData,
        decimal Revenue,
        int UnitsSold,
        decimal VelocityUnitsPerDay,
        decimal? MarginPct,
        decimal MarginCoveragePct,
        decimal? TrendPct,
        int StockGap,
        int CurrentStock,
        int MinStock,
        int? DaysSinceLastSale);

    public sealed record Result(
        string RecommendationStatus,
        IReadOnlyList<string> ReasonCodes);

    public static Result Evaluate(Input input)
    {
        var recommendationStatus = ResolveRecommendationStatus(input);
        var codes = BuildReasonCodes(input, recommendationStatus);
        return new Result(recommendationStatus, codes);
    }

    public static string ResolveRecommendationStatus(Input input)
    {
        if (input.MissingSupplier || input.MissingCost || input.MissingCategory)
            return "FIX_DATA";

        if (input.UnitsSold < 3 || input.Revenue <= 0m || !input.DaysSinceLastSale.HasValue)
            return "INSUFFICIENT_DATA";

        var goodTrend = (input.TrendPct ?? 0m) >= 10m;
        var badTrend = (input.TrendPct ?? 0m) <= -10m;
        var goodMargin = (input.MarginPct ?? 0m) >= 22m;
        var lowMargin = (input.MarginPct ?? 0m) < 10m;
        var highVelocity = input.VelocityUnitsPerDay >= 0.8m;
        var lowVelocity = input.VelocityUnitsPerDay < 0.15m;
        var staleStock = input.DaysSinceLastSale.Value >= 45;
        var highStock = input.CurrentStock > Math.Max(input.MinStock * 3, input.MinStock + 10);

        if (goodTrend && goodMargin && highVelocity && input.StockGap > 0)
            return "BOOST";

        if (highVelocity && input.StockGap > 0)
            return "REPLENISH";

        if ((staleStock && lowVelocity && (badTrend || lowMargin)) && input.CurrentStock > input.MinStock)
            return "MARKDOWN";

        if ((badTrend && lowMargin && highStock) || (staleStock && highStock && lowVelocity))
            return "DO_NOT_ORDER";

        return "WATCH";
    }

    public static IReadOnlyList<string> BuildReasonCodes(Input input, string recommendationStatus)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);

        if (input.MissingSupplier) codes.Add(ReasonCodes.MissingSupplier);
        if (input.MissingCost) codes.Add(ReasonCodes.MissingCost);

        if (input.MissingCategory || input.MissingVariantData)
            codes.Add(ReasonCodes.DataQualityBlocker);

        if (input.StockGap > 0 || input.CurrentStock < input.MinStock)
            codes.Add(ReasonCodes.LowStock);

        if (input.VelocityUnitsPerDay >= 0.8m)
            codes.Add(ReasonCodes.HighVelocity);

        if ((input.MarginPct ?? 0m) < 10m || input.MarginCoveragePct < 60m)
            codes.Add(ReasonCodes.PoorMargin);

        if (input.DaysSinceLastSale.HasValue && input.DaysSinceLastSale.Value >= 45)
            codes.Add(ReasonCodes.StaleStock);

        if (input.UnitsSold < 3 || input.Revenue <= 0m || !input.DaysSinceLastSale.HasValue)
            codes.Add(ReasonCodes.InsufficientHistory);

        if (recommendationStatus == "REPLENISH")
            codes.Add(ReasonCodes.ReplenishNeeded);

        if (recommendationStatus == "DO_NOT_ORDER")
            codes.Add(ReasonCodes.HighStockRisk);

        if (recommendationStatus == "FIX_DATA")
            codes.Add(ReasonCodes.DataQualityBlocker);

        if (recommendationStatus == "INSUFFICIENT_DATA" && codes.Count == 0)
            codes.Add(ReasonCodes.InsufficientHistory);

        return codes.ToList();
    }
}
