namespace Application.Analytics;

public static class AnalyticsDecisionRecommendationEngine
{
    public sealed record RecommendationInput(
        bool IsUnknownEntity,
        decimal TotalRevenue,
        int TotalUnits,
        int ItemCount,
        double SharePct,
        double MarginPct,
        double? MarginCoveragePct,
        double? SplitCoveragePct,
        double? PopRevenueChangePct,
        double? PopUnitsChangePct,
        decimal? PreviousPeriodRevenue,
        int? PreviousPeriodUnits,
        bool HasPreviousPeriodWindow,
        bool IsNewEntity,
        double UnknownBucketSharePct);

    public sealed record RecommendationResult(
        string Status,
        string Label,
        string Summary,
        double ConfidencePct,
        double ReliabilityPct,
        string DataQualityStatus,
        IReadOnlyList<string> ReasonCodes);

    public static RecommendationResult Evaluate(RecommendationInput input, double? averageMarginPct)
    {
        var reasons = new List<string>();

        var marginCoverage = Clamp(input.MarginCoveragePct ?? 0d, 0d, 100d);
        var splitCoverage = Clamp(input.SplitCoveragePct ?? 0d, 0d, 100d);
        var unknownShare = Clamp(input.UnknownBucketSharePct, 0d, 100d);

        if (input.IsUnknownEntity) reasons.Add("unknown_entity");
        if (input.IsNewEntity) reasons.Add("new_entity");
        if (!input.HasPreviousPeriodWindow) reasons.Add("previous_period_missing");
        if (input.PreviousPeriodRevenue.HasValue && input.PreviousPeriodRevenue.Value <= 0m && input.TotalRevenue > 0m) reasons.Add("no_previous_baseline");
        if (!averageMarginPct.HasValue) reasons.Add("missing_known_margin_baseline");
        if (marginCoverage < 70d) reasons.Add("missing_cost_coverage");
        if (splitCoverage > 0d && splitCoverage < 60d) reasons.Add("limited_nivelacija_coverage");
        if (unknownShare >= 15d) reasons.Add("unknown_heavy_dataset");
        if (IsTinySample(input)) reasons.Add("tiny_sample");
        if (IsUnstableMargin(input.MarginPct)) reasons.Add("unstable_margin");
        if (input.PopRevenueChangePct is null && input.HasPreviousPeriodWindow) reasons.Add("pop_unavailable");

        var reliability = ComputeReliabilityPct(input, marginCoverage, splitCoverage);
        var dataQualityStatus = ComputeDataQualityStatus(input, marginCoverage, splitCoverage, unknownShare, reliability);
        var status = DecideStatus(input, averageMarginPct, reliability, dataQualityStatus, reasons);
        var confidence = ComputeConfidence(status, reliability, reasons);
        var summary = BuildSummary(status, reasons, input, reliability);

        return new RecommendationResult(
            Status: status,
            Label: ToLabel(status),
            Summary: summary,
            ConfidencePct: confidence,
            ReliabilityPct: reliability,
            DataQualityStatus: dataQualityStatus,
            ReasonCodes: reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool IsTinySample(RecommendationInput input)
        => input.ItemCount < 3 || input.TotalUnits < 8 || input.TotalRevenue < 15000m;

    private static bool IsUnstableMargin(double marginPct)
        => marginPct < -15d || Math.Abs(marginPct) > 80d;

    private static double ComputeReliabilityPct(
        RecommendationInput input,
        double marginCoverage,
        double splitCoverage)
    {
        var reliability =
            marginCoverage * 0.50 +
            splitCoverage * 0.15 +
            (input.HasPreviousPeriodWindow ? 20d : 0d) +
            (input.IsUnknownEntity ? 0d : 10d) +
            (input.ItemCount >= 6 ? 5d : input.ItemCount >= 3 ? 2d : 0d);

        return Math.Round(Clamp(reliability, 0d, 100d), 2);
    }

    private static string ComputeDataQualityStatus(
        RecommendationInput input,
        double marginCoverage,
        double splitCoverage,
        double unknownShare,
        double reliabilityPct)
    {
        if (input.IsUnknownEntity || marginCoverage < 40d || unknownShare >= 25d || reliabilityPct < 35d)
        {
            return "critical";
        }

        if (marginCoverage < 70d || (splitCoverage > 0d && splitCoverage < 60d) || unknownShare >= 10d || reliabilityPct < 55d)
        {
            return "warning";
        }

        return "good";
    }

    private static string DecideStatus(
        RecommendationInput input,
        double? averageMarginPct,
        double reliabilityPct,
        string dataQualityStatus,
        IReadOnlyCollection<string> reasons)
    {
        if (input.IsUnknownEntity)
        {
            return "do_not_trust";
        }

        if (reasons.Contains("missing_known_margin_baseline"))
        {
            return "insufficient_data";
        }

        if (reasons.Contains("tiny_sample") || (!input.HasPreviousPeriodWindow && input.TotalRevenue < 60000m))
        {
            return "insufficient_data";
        }

        if (dataQualityStatus == "critical" || reliabilityPct < 35d || reasons.Contains("unstable_margin"))
        {
            return "do_not_trust";
        }

        if (input.IsNewEntity || !input.HasPreviousPeriodWindow || input.PopRevenueChangePct is null)
        {
            return "review";
        }

        var pop = input.PopRevenueChangePct.Value;
        var absoluteMarginFloor = 8d;
        var dynamicMarginFloor = Math.Max(absoluteMarginFloor, (averageMarginPct ?? absoluteMarginFloor) - 2d);

        if (pop >= 12d && input.MarginPct >= dynamicMarginFloor && input.SharePct >= 2.5d && reliabilityPct >= 60d)
        {
            return "increase_focus";
        }

        if (pop <= -10d || input.MarginPct < 4d)
        {
            return "review";
        }

        return "maintain";
    }

    private static double ComputeConfidence(string status, double reliabilityPct, IReadOnlyCollection<string> reasons)
    {
        var confidence = reliabilityPct;

        if (reasons.Contains("new_entity")) confidence -= 20d;
        if (reasons.Contains("tiny_sample")) confidence -= 25d;
        if (reasons.Contains("unknown_heavy_dataset")) confidence -= 15d;
        if (reasons.Contains("unstable_margin")) confidence -= 20d;

        if (status == "insufficient_data") confidence = Math.Min(confidence, 35d);
        if (status == "do_not_trust") confidence = Math.Min(confidence, 45d);

        return Math.Round(Clamp(confidence, 0d, 100d), 2);
    }

    private static string BuildSummary(
        string status,
        IReadOnlyCollection<string> reasons,
        RecommendationInput input,
        double reliabilityPct)
    {
        var costCaveat = reasons.Contains("missing_cost_coverage")
            ? $" Margin signal relies on estimated cost ({input.MarginCoveragePct ?? 0:0.#}% coverage)."
            : "";

        var baselineCaveat = reasons.Contains("missing_known_margin_baseline")
            ? " Comparable known-margin baseline is unavailable."
            : "";

        return status switch
        {
            "increase_focus" => $"Strong PoP trend and healthy margin with acceptable reliability ({reliabilityPct:0.#}%).{costCaveat}{baselineCaveat}",
            "maintain" => $"Stable supplier profile without strong upside/downside signal. Reliability {reliabilityPct:0.#}%.{costCaveat}{baselineCaveat}",
            "review" when reasons.Contains("new_entity") =>
                "Supplier is new versus previous comparable period; review manually before increasing focus.",
            "review" => "Performance or quality signals are mixed; review before changing procurement focus.",
            "do_not_trust" when reasons.Contains("unknown_entity") =>
                "Supplier identity is unknown, so recommendation is not trustworthy for business decisions.",
            "do_not_trust" =>
                "Data reliability is too low or margin signal is unstable; do not trust automated recommendation.",
            "insufficient_data" when reasons.Contains("previous_period_missing") =>
                "Comparable previous period is missing; insufficient evidence for a reliable recommendation.",
            "insufficient_data" when reasons.Contains("missing_known_margin_baseline") =>
                "Comparable known-margin baseline is missing; insufficient evidence for a reliable recommendation.",
            "insufficient_data" when IsTinySample(input) =>
                "Sample is too small (revenue/units/articles) to produce a trustworthy recommendation.",
            _ => "Insufficient evidence for automated decision support."
        };
    }

    private static string ToLabel(string status)
    {
        return status switch
        {
            "increase_focus" => "Increase focus",
            "maintain" => "Maintain",
            "review" => "Review",
            "do_not_trust" => "Do not trust",
            _ => "Insufficient data"
        };
    }

    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));
}
