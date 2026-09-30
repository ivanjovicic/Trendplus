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
        double? UnknownBucketSharePct,
        bool SharePctAvailable = true);

    public sealed record RecommendationResult(
        string Status,
        string Label,
        string Summary,
        double ConfidencePct,
        double ReliabilityPct,
        string DataQualityStatus,
        bool RecommendationAllowed,
        IReadOnlyList<string> ReasonCodes);

    public static RecommendationResult ApplyComparableSignalGate(
        RecommendationResult recommendation,
        bool hasComparableSignal)
    {
        if (hasComparableSignal)
        {
            return recommendation;
        }

        var reasonCodes = recommendation.ReasonCodes
            .Append("missing_comparable_signal")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return recommendation with
        {
            Status = "insufficient_data",
            Label = "Nedovoljno podataka",
            Summary = "Nedostaje uporediv signal pre i posle nivelacije; nema dovoljno dokaza za pouzdanu preporuku.",
            DataQualityStatus = "insufficient_data",
            RecommendationAllowed = false,
            ReasonCodes = reasonCodes
        };
    }

    public static RecommendationResult Evaluate(
        RecommendationInput input,
        double? averageMarginPct,
        bool requireComparableSignal = true,
        bool applyUnknownShareCriticalGate = true)
    {
        var reasons = new List<string>();

        var hasMarginCoverage = input.MarginCoveragePct.HasValue;
        var hasSplitCoverage = input.SplitCoveragePct.HasValue;
        var marginCoverage = hasMarginCoverage ? Clamp(input.MarginCoveragePct!.Value, 0d, 100d) : 0d;
        var splitCoverage = hasSplitCoverage ? Clamp(input.SplitCoveragePct!.Value, 0d, 100d) : 0d;
        var hasUnknownShare = input.UnknownBucketSharePct.HasValue;
        var unknownShare = hasUnknownShare
            ? Clamp(input.UnknownBucketSharePct!.Value, 0d, 100d)
            : 100d;

        if (input.IsUnknownEntity) reasons.Add("unknown_entity");
        if (!input.SharePctAvailable) reasons.Add("share_denominator_unavailable");
        if (input.IsNewEntity) reasons.Add("new_entity");
        if (!input.HasPreviousPeriodWindow) reasons.Add("previous_period_missing");
        if (input.PreviousPeriodRevenue.HasValue && input.PreviousPeriodRevenue.Value <= 0m && input.TotalRevenue > 0m) reasons.Add("no_previous_baseline");
        if (!averageMarginPct.HasValue) reasons.Add("missing_known_margin_baseline");
        if (!hasUnknownShare) reasons.Add("unknown_bucket_share_unavailable");
        if (!hasMarginCoverage || marginCoverage < 70d) reasons.Add("missing_cost_coverage");
        if (!hasSplitCoverage) reasons.Add("missing_split_coverage");
        else if (splitCoverage > 0d && splitCoverage < 60d) reasons.Add("limited_nivelacija_coverage");
        if (unknownShare >= 15d) reasons.Add("unknown_heavy_dataset");
        if (IsTinySample(input)) reasons.Add("tiny_sample");
        if (IsUnstableMargin(input.MarginPct)) reasons.Add("unstable_margin");
        if (input.PopRevenueChangePct is null && input.HasPreviousPeriodWindow) reasons.Add("pop_unavailable");

        var reliability = ComputeReliabilityPct(input, hasMarginCoverage, marginCoverage, hasSplitCoverage, splitCoverage);
        var dataQualityStatus = ComputeDataQualityStatus(
            input,
            hasMarginCoverage,
            marginCoverage,
            hasSplitCoverage,
            splitCoverage,
            hasUnknownShare,
            unknownShare,
            reliability,
            applyUnknownShareCriticalGate);
        var status = DecideStatus(
            input,
            averageMarginPct,
            reliability,
            dataQualityStatus,
            reasons,
            requireComparableSignal);
        var confidence = ComputeConfidence(status, reliability, reasons);
        var summary = BuildSummary(status, reasons, input, reliability);

        return new RecommendationResult(
            Status: status,
            Label: ToLabel(status),
            Summary: summary,
            ConfidencePct: confidence,
            ReliabilityPct: reliability,
            DataQualityStatus: dataQualityStatus,
            RecommendationAllowed: status is not ("insufficient_data" or "do_not_trust")
                && dataQualityStatus is not "critical",
            ReasonCodes: reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool IsTinySample(RecommendationInput input)
        => input.ItemCount < 3 || input.TotalUnits < 8 || input.TotalRevenue < 15000m;

    private static bool IsUnstableMargin(double marginPct)
        => marginPct < -15d || Math.Abs(marginPct) > 80d;

    private static double ComputeReliabilityPct(
        RecommendationInput input,
        bool hasMarginCoverage,
        double marginCoverage,
        bool hasSplitCoverage,
        double splitCoverage)
    {
        var reliability =
            (hasMarginCoverage ? marginCoverage * 0.50 : 0d) +
            (hasSplitCoverage ? splitCoverage * 0.15 : 0d) +
            (input.HasPreviousPeriodWindow ? 20d : 0d) +
            (input.IsUnknownEntity ? 0d : 10d) +
            (input.ItemCount >= 6 ? 5d : input.ItemCount >= 3 ? 2d : 0d);

        return Math.Round(Clamp(reliability, 0d, 100d), 2);
    }

    private static string ComputeDataQualityStatus(
        RecommendationInput input,
        bool hasMarginCoverage,
        double marginCoverage,
        bool hasSplitCoverage,
        double splitCoverage,
        bool hasUnknownShare,
        double unknownShare,
        double reliabilityPct,
        bool applyUnknownShareCriticalGate)
    {
        if (input.IsUnknownEntity
            || !input.SharePctAvailable
            || !hasUnknownShare
            || !hasMarginCoverage
            || marginCoverage < 40d
            || (applyUnknownShareCriticalGate && unknownShare >= 25d)
            || reliabilityPct < 35d)
        {
            return "critical";
        }

        if (marginCoverage < 70d
            || !hasSplitCoverage
            || (splitCoverage > 0d && splitCoverage < 60d)
            || unknownShare >= 10d
            || reliabilityPct < 55d)
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
        IReadOnlyCollection<string> reasons,
        bool requireComparableSignal)
    {
        if (input.IsUnknownEntity)
        {
            return "do_not_trust";
        }

        if (reasons.Contains("missing_known_margin_baseline")
            || (requireComparableSignal && reasons.Contains("missing_split_coverage"))
            || reasons.Contains("unknown_bucket_share_unavailable")
            || reasons.Contains("share_denominator_unavailable"))
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
            "increase_focus" => $"Snažan trend u odnosu na prethodni period i zdrava marža uz prihvatljivu pouzdanost ({reliabilityPct:0.#}%).{costCaveat}{baselineCaveat}",
            "maintain" => $"Stabilan profil dobavljača bez jakog pozitivnog ili negativnog signala. Pouzdanost {reliabilityPct:0.#}%.{costCaveat}{baselineCaveat}",
            "review" when reasons.Contains("new_entity") =>
                "Dobavljač je nov u odnosu na prethodni uporediv period; pre promene fokusa potrebna je ručna provera.",
            "review" => "Signal učinka ili kvaliteta je mešovit; proverite podatke pre promene fokusa nabavke.",
            "do_not_trust" when reasons.Contains("unknown_entity") =>
                "Identitet dobavljača nije poznat, pa preporuka nije pouzdana za poslovne odluke.",
            "do_not_trust" =>
                "Pouzdanost podataka je preniska ili je signal marže nestabilan; ne oslanjajte se na automatsku preporuku.",
            "insufficient_data" when reasons.Contains("previous_period_missing") =>
                "Nedostaje uporediv prethodni period; nema dovoljno dokaza za pouzdanu preporuku.",
            "insufficient_data" when reasons.Contains("missing_known_margin_baseline") =>
                "Nedostaje uporediva osnova poznate marže; nema dovoljno dokaza za pouzdanu preporuku.",
            "insufficient_data" when reasons.Contains("missing_split_coverage") =>
                "Nedostaje pokrivenost podele nivelacije; nema dovoljno dokaza za pouzdanu preporuku.",
            "insufficient_data" when reasons.Contains("unknown_bucket_share_unavailable") =>
                "Delilac udela nepoznatih dobavljača nije dostupan; nema dovoljno dokaza za pouzdanu preporuku.",
            "insufficient_data" when reasons.Contains("share_denominator_unavailable") =>
                "Delilac neto prodajnog udela nije dostupan; nema dovoljno dokaza za pouzdanu preporuku.",
            "insufficient_data" when IsTinySample(input) =>
                "Uzorak je premali (promet/komadi/artikli) za pouzdanu preporuku.",
            _ => "Nema dovoljno dokaza za automatsku podršku odlučivanju."
        };
    }

    private static string ToLabel(string status)
    {
        return status switch
        {
            "increase_focus" => "Pojačati fokus",
            "maintain" => "Zadržati",
            "review" => "Proveriti",
            "do_not_trust" => "Ne verovati podacima",
            _ => "Nedovoljno podataka"
        };
    }

    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));
}
