namespace Application.Analytics;

/// <summary>
/// Descriptive pre/post markdown effect for Supplier Footwear (Asortiman).
/// Not a Supplier PoP buying recommendation; results stay non-actionable by default.
/// </summary>
public static class VendorSalesNivelacijaPriceChangeEffectPolicy
{
    public const double EffectiveThresholdPct = 5d;
    public const double IneffectiveThresholdPct = -5d;
    public const double MarginCoverageThresholdPct = 70d;
    public const int PostWindowDays = 30;

    public sealed record VendorAggregateInput(
        bool IsUnknownVendor,
        decimal PreRevenue,
        decimal PostRevenue,
        int PreQty,
        int PostQty,
        int ComparableArticleCount,
        int MatureComparableArticleCount,
        int ImmatureComparableArticleCount,
        double? SemanticChangePercentRevenue,
        double MarginPct,
        double? MarginCoveragePct,
        double? SplitCoveragePct);

    public sealed record PriceChangeEffectResult(
        string Status,
        string Label,
        string Summary,
        double? ConfidencePct,
        double? ReliabilityPct,
        string DataQualityStatus,
        bool RecommendationAllowed,
        IReadOnlyList<string> ReasonCodes);

    public static decimal? ComputeSemanticChangePercent(decimal preRevenue, decimal postRevenue)
    {
        if (preRevenue <= 0m)
        {
            return postRevenue == 0m ? 0m : null;
        }

        return Math.Round(((postRevenue - preRevenue) / preRevenue) * 100m, 2);
    }

    /// <summary>
    /// Change percent for an aggregated cohort (vendor or totals). Without mature comparable
    /// rows the pre/post sums are not evidence, so the result is unknown rather than 0%.
    /// </summary>
    public static decimal? ComputeCohortChangePercent(int matureComparableRowCount, decimal preRevenue, decimal postRevenue)
        => matureComparableRowCount > 0
            ? ComputeSemanticChangePercent(preRevenue, postRevenue)
            : null;

    public static bool IsPostWindowMature(DateTime eventDateUtc, DateTime asOfDateUtc)
        => eventDateUtc.Date.AddDays(PostWindowDays) <= asOfDateUtc.Date;

    public static int ComputePostWindowDaysElapsed(DateTime eventDateUtc, DateTime asOfDateUtc)
    {
        var elapsed = (asOfDateUtc.Date - eventDateUtc.Date).Days;
        return Math.Clamp(elapsed, 0, PostWindowDays);
    }

    public static double? ComputeWeightedMarginBenchmarkPct(
        IEnumerable<(double MarginPct, double MarginCoveragePct, decimal PostRevenue)> vendors)
    {
        decimal weightedSum = 0m;
        decimal weight = 0m;

        foreach (var vendor in vendors)
        {
            if (vendor.PostRevenue <= 0m
                || vendor.MarginCoveragePct < MarginCoverageThresholdPct
                || double.IsNaN(vendor.MarginPct)
                || double.IsInfinity(vendor.MarginPct))
            {
                continue;
            }

            weightedSum += (decimal)vendor.MarginPct * vendor.PostRevenue;
            weight += vendor.PostRevenue;
        }

        return weight <= 0m
            ? null
            : Math.Round((double)(weightedSum / weight), 2);
    }

    public static PriceChangeEffectResult Evaluate(VendorAggregateInput input)
    {
        var reasons = new List<string>();

        if (input.IsUnknownVendor)
        {
            reasons.Add("unknown_entity");
        }

        if (input.ComparableArticleCount == 0)
        {
            reasons.Add("missing_comparable_rows");
        }

        if (input.MatureComparableArticleCount == 0 && input.ImmatureComparableArticleCount > 0)
        {
            reasons.Add("post_window_immature");
        }

        if (input.SemanticChangePercentRevenue is null && input.MatureComparableArticleCount > 0)
        {
            reasons.Add("no_revenue_baseline");
        }

        if (input.MarginCoveragePct is null or < MarginCoverageThresholdPct)
        {
            reasons.Add("missing_cost_coverage");
        }

        if (input.SplitCoveragePct is null)
        {
            reasons.Add("missing_split_coverage");
        }

        string status;
        if (input.IsUnknownVendor || input.ComparableArticleCount == 0)
        {
            status = "insufficient_data";
        }
        else if (input.MatureComparableArticleCount == 0)
        {
            status = "immature";
        }
        else if (input.SemanticChangePercentRevenue is null)
        {
            status = "insufficient_data";
        }
        else if (input.SemanticChangePercentRevenue.Value >= EffectiveThresholdPct)
        {
            status = "effective";
        }
        else if (input.SemanticChangePercentRevenue.Value <= IneffectiveThresholdPct)
        {
            status = "ineffective";
        }
        else
        {
            status = "neutral";
        }

        var reliability = ComputeReliability(input, reasons);
        var dataQuality = ComputeDataQuality(input, reasons, reliability);
        var label = ToLabel(status);
        var summary = BuildSummary(status, input, reasons);

        return new PriceChangeEffectResult(
            Status: status,
            Label: label,
            Summary: summary,
            ConfidencePct: status is "effective" or "neutral" or "ineffective"
                ? Math.Round(Math.Min(95d, Math.Max(25d, reliability)), 2)
                : null,
            ReliabilityPct: status is "effective" or "neutral" or "ineffective" or "immature"
                ? reliability
                : null,
            DataQualityStatus: dataQuality,
            RecommendationAllowed: false,
            ReasonCodes: reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static double ComputeReliability(VendorAggregateInput input, IReadOnlyCollection<string> reasons)
    {
        var score = 55d;
        if (input.ComparableArticleCount >= 5) score += 10d;
        if (input.MatureComparableArticleCount > 0) score += 10d;
        if (input.MarginCoveragePct is >= MarginCoverageThresholdPct) score += 10d;
        if (input.SplitCoveragePct is >= 60d) score += 10d;
        if (reasons.Contains("missing_split_coverage")) score -= 15d;
        if (reasons.Contains("missing_cost_coverage")) score -= 10d;
        if (reasons.Contains("post_window_immature")) score -= 20d;
        return Math.Round(Math.Clamp(score, 0d, 100d), 2);
    }

    private static string ComputeDataQuality(
        VendorAggregateInput input,
        IReadOnlyCollection<string> reasons,
        double reliability)
    {
        if (reasons.Contains("unknown_entity") || input.ComparableArticleCount == 0)
        {
            return "insufficient_data";
        }

        if (reasons.Contains("post_window_immature"))
        {
            return "warning";
        }

        if (reliability < 45d || reasons.Contains("missing_cost_coverage"))
        {
            return "critical";
        }

        if (reasons.Contains("missing_split_coverage") || reliability < 65d)
        {
            return "warning";
        }

        return "good";
    }

    private static string BuildSummary(string status, VendorAggregateInput input, IReadOnlyCollection<string> reasons)
    {
        if (status == "immature")
        {
            return "Post prozor nije zreo; efekat nivelacije se ne tretira kao završen.";
        }

        if (status == "insufficient_data")
        {
            if (reasons.Contains("no_revenue_baseline"))
            {
                return "Nema validne prethodne baze prometa za procenu efekta posle promene cene.";
            }

            return "Nedovoljno uporedivih podataka za opis efekta promene cene.";
        }

        var pct = input.SemanticChangePercentRevenue!.Value;
        return status switch
        {
            "effective" => $"Promet posle promene cene raste {pct:0.##}% u odnosu na uporediv pre prozor (descriptive signal, ne PoP).",
            "ineffective" => $"Promet posle promene cene pada {Math.Abs(pct):0.##}% u odnosu na uporediv pre prozor (descriptive signal, ne PoP).",
            _ => $"Promet posle promene cene je blizu pre nivoa ({pct:0.##}%, descriptive signal, ne PoP)."
        };
    }

    public static string ToLabel(string status) => status switch
    {
        "effective" => "Efekat pozitivan",
        "neutral" => "Efekat neutralan",
        "ineffective" => "Efekat slab",
        "immature" => "Prozor u toku",
        _ => "Nedovoljno podataka"
    };
}
