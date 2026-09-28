namespace Application.Analytics;

public static class ProductDecisionReasoningHelper
{
    public sealed record ThresholdPolicy
    {
        public string PolicyName { get; init; } = "product-decision-initial-pilot-v1";
        public string Source { get; init; } = "initial pilot threshold, owner 2026-09-28";
        public string CalibrationStatus { get; init; } = "initial_pilot_not_calibrated";
        public int MinimumUnitsForPositiveSignal { get; init; } = 3;
        public int StaleReviewDays { get; init; } = 45;
        public int StrongNoSaleDays { get; init; } = 90;
        public decimal LowVelocityUnitsPerDay { get; init; } = 0.15m;
        public decimal HighVelocityUnitsPerDay { get; init; } = 0.8m;
        public decimal LowStockCoverDays { get; init; } = 14m;
        public decimal GoodTrendPct { get; init; } = 10m;
        public decimal BadTrendPct { get; init; } = -10m;
        public decimal GoodMarginPct { get; init; } = 22m;
        public decimal LowMarginPct { get; init; } = 10m;
        public decimal HighStockMultiple { get; init; } = 3m;
        public int HighStockAdditionalUnits { get; init; } = 10;
        public int LostSalesImpactWindowDays { get; init; } = 14;
        public int NewProductConfidencePenaltyPct { get; init; } = 15;
        public int UnconfiguredMinStockConfidencePenaltyPct { get; init; } = 15;

        public static ThresholdPolicy Default { get; } = new();
    }

    // Compatibility alias for existing explainability code. The policy is the
    // single owner of the value; callers should prefer the policy property.
    public static int MinimumUnitsForRecommendation => ThresholdPolicy.Default.MinimumUnitsForPositiveSignal;

    public static class ReasonCodes
    {
        public const string HighVelocity = "high_velocity";
        public const string LowStock = "low_stock";
        public const string PoorMargin = "poor_margin";
        public const string StaleStock = "stale_stock";
        public const string MissingCost = "missing_cost";
        public const string MissingSupplier = "missing_supplier";
        public const string InsufficientHistory = "insufficient_history";
        public const string LowSampleSize = "low_sample_size";
        public const string NoSalesInPeriod = "no_sales_in_period";
        public const string MissingLastSale = "missing_last_sale";
        public const string MarginCoverageUnavailable = "margin_coverage_unavailable";
        public const string StockEvidenceUnavailable = "stock_evidence_unavailable";
        public const string ReplenishNeeded = "replenish_needed";
        public const string HighStockRisk = "high_stock_risk";
        public const string DataQualityBlocker = "data_quality_blocker";
        public const string StaleNoSaleReview = "stale_no_sale_review";
        public const string LowVelocityReview = "low_velocity_review";
        public const string NoBaseline = "no_baseline";
        public const string MinimumStockNotConfigured = "minimum_stock_not_configured";
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
        decimal? MarginCoveragePct,
        decimal? TrendPct,
        int? StockGap,
        int? CurrentStock,
        int? MinStock,
        int? DaysSinceLastSale,
        bool IsNewProduct = false,
        decimal? StockCoverDays = null);

    public sealed record Result(
        string RecommendationStatus,
        IReadOnlyList<string> ReasonCodes);

    public static Result Evaluate(Input input, ThresholdPolicy? policy = null)
    {
        var effectivePolicy = policy ?? ThresholdPolicy.Default;
        var recommendationStatus = ResolveRecommendationStatus(input, effectivePolicy);
        var codes = BuildReasonCodes(input, recommendationStatus, effectivePolicy);
        return new Result(recommendationStatus, codes);
    }

    /// <summary>
    /// Previous baseline must be present and positive to emit a finite trend.
    /// Missing or zero previous revenue stays unavailable (never synthesizes +100%).
    /// </summary>
    public static decimal? ComputeTrendPct(decimal currentRevenue, decimal? previousRevenue)
    {
        if (previousRevenue is null || previousRevenue.Value <= 0m)
            return null;

        return ((currentRevenue - previousRevenue.Value) / previousRevenue.Value) * 100m;
    }

    public static string ResolveRecommendationStatus(Input input, ThresholdPolicy? policy = null)
    {
        var effectivePolicy = policy ?? ThresholdPolicy.Default;

        if (input.MissingSupplier || input.MissingCost || input.MissingCategory)
            return "FIX_DATA";

        if (!input.CurrentStock.HasValue)
            return "INSUFFICIENT_DATA";

        var staleStock = input.DaysSinceLastSale >= effectivePolicy.StaleReviewDays;
        var hasStockTarget = input.MinStock is > 0;
        var lowCover = input.StockCoverDays.HasValue
            && input.StockCoverDays.Value <= effectivePolicy.LowStockCoverDays;
        var replenishmentNeeded = input.StockGap is > 0 || (!hasStockTarget && lowCover);
        var highStock = hasStockTarget
            && input.CurrentStock.Value > Math.Max(
                input.MinStock!.Value * effectivePolicy.HighStockMultiple,
                input.MinStock.Value + (decimal)effectivePolicy.HighStockAdditionalUnits);

        // A stocked article with no sale for the initial review window is a
        // review candidate, even when current-period sales are zero. It is not
        // silently upgraded to an executable markdown/order decision.
        if (staleStock && input.CurrentStock.Value > 0 && input.UnitsSold <= 0)
            return "WATCH";

        if (!input.MarginCoveragePct.HasValue)
            return "INSUFFICIENT_DATA";

        if (!hasStockTarget && !lowCover)
            return "INSUFFICIENT_DATA";

        var positiveSampleComplete = input.UnitsSold >= effectivePolicy.MinimumUnitsForPositiveSignal
            && input.Revenue > 0m
            && input.MarginPct.HasValue
            && input.DaysSinceLastSale.HasValue;

        if (!positiveSampleComplete)
            return "INSUFFICIENT_DATA";

        // A missing previous-period baseline is valid for a new product, but
        // never becomes a fabricated percentage trend.
        if (input.TrendPct is null && !input.IsNewProduct)
            return "INSUFFICIENT_DATA";

        if (input.MarginPct == null)
            return "INSUFFICIENT_DATA";

        var goodTrend = input.IsNewProduct || input.TrendPct.GetValueOrDefault() >= effectivePolicy.GoodTrendPct;
        var badTrend = input.TrendPct.HasValue && input.TrendPct.Value <= effectivePolicy.BadTrendPct;
        var goodMargin = input.MarginPct.Value >= effectivePolicy.GoodMarginPct;
        var lowMargin = input.MarginPct.Value < effectivePolicy.LowMarginPct;
        var highVelocity = input.VelocityUnitsPerDay >= effectivePolicy.HighVelocityUnitsPerDay;
        var lowVelocity = input.VelocityUnitsPerDay < effectivePolicy.LowVelocityUnitsPerDay;

        if (goodTrend && goodMargin && highVelocity && replenishmentNeeded)
            return "BOOST";

        if (highVelocity && replenishmentNeeded)
            return "REPLENISH";

        if (staleStock && lowVelocity && (badTrend || lowMargin) && input.CurrentStock.Value > input.MinStock.GetValueOrDefault())
            return "MARKDOWN";

        if (input.DaysSinceLastSale >= effectivePolicy.StrongNoSaleDays
            && highStock
            && (lowVelocity || (badTrend && lowMargin)))
            return "DO_NOT_ORDER";

        return "WATCH";
    }

    public static IReadOnlyList<string> BuildReasonCodes(
        Input input,
        string recommendationStatus,
        ThresholdPolicy? policy = null)
    {
        var effectivePolicy = policy ?? ThresholdPolicy.Default;
        var codes = new HashSet<string>(StringComparer.Ordinal);

        if (input.MissingSupplier) codes.Add(ReasonCodes.MissingSupplier);
        if (input.MissingCost) codes.Add(ReasonCodes.MissingCost);

        if (input.MissingCategory || input.MissingVariantData)
            codes.Add(ReasonCodes.DataQualityBlocker);

        if (!input.StockGap.HasValue || !input.CurrentStock.HasValue || !input.MinStock.HasValue)
            codes.Add(ReasonCodes.StockEvidenceUnavailable);

        if (!input.MarginCoveragePct.HasValue)
            codes.Add(ReasonCodes.MarginCoverageUnavailable);

        if (input.StockGap is > 0 || (input.CurrentStock.HasValue && input.MinStock.HasValue && input.CurrentStock.Value < input.MinStock.Value))
            codes.Add(ReasonCodes.LowStock);

        if (input.VelocityUnitsPerDay >= effectivePolicy.HighVelocityUnitsPerDay)
            codes.Add(ReasonCodes.HighVelocity);

        if (input.VelocityUnitsPerDay < effectivePolicy.LowVelocityUnitsPerDay)
            codes.Add(ReasonCodes.LowVelocityReview);

        if (input.MarginPct.HasValue
            && (input.MarginPct.Value < effectivePolicy.LowMarginPct || input.MarginCoveragePct is < 60m))
            codes.Add(ReasonCodes.PoorMargin);

        if (input.DaysSinceLastSale.HasValue && input.DaysSinceLastSale.Value >= effectivePolicy.StaleReviewDays)
        {
            codes.Add(ReasonCodes.StaleStock);
            if (input.UnitsSold <= 0 && input.CurrentStock.GetValueOrDefault() > 0)
                codes.Add(ReasonCodes.StaleNoSaleReview);
        }

        if (input.IsNewProduct)
            codes.Add(ReasonCodes.NoBaseline);

        if (input.MinStock is null or <= 0)
            codes.Add(ReasonCodes.MinimumStockNotConfigured);

        var missingDecisionEvidence =
            (!input.IsNewProduct && input.TrendPct is null) ||
            input.MarginPct is null ||
            input.MarginCoveragePct is null ||
            input.UnitsSold < effectivePolicy.MinimumUnitsForPositiveSignal ||
            input.Revenue <= 0m ||
            !input.DaysSinceLastSale.HasValue;

        if (missingDecisionEvidence)
        {
            codes.Add(ReasonCodes.InsufficientHistory);

            if (input.UnitsSold < effectivePolicy.MinimumUnitsForPositiveSignal)
                codes.Add(ReasonCodes.LowSampleSize);

            if (input.Revenue <= 0m)
                codes.Add(ReasonCodes.NoSalesInPeriod);

            if (!input.DaysSinceLastSale.HasValue)
                codes.Add(ReasonCodes.MissingLastSale);
        }

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

    public static string RecommendationLabel(string? status)
    {
        return (status ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "BOOST" => "Pojačaj",
            "REPLENISH" => "Dopuni",
            "WATCH" => "Prati",
            "MARKDOWN" => "Snizi cenu",
            "DO_NOT_ORDER" => "Ne naručuj",
            "FIX_DATA" => "Proveri podatke",
            "INSUFFICIENT_DATA" => "Nedovoljno podataka",
            "" => "sve porodice",
            _ => status!.Trim()
        };
    }
}
