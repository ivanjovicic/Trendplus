namespace Application.Analytics;

public static class OperationsRecommendationGatePolicy
{
    public const double SupplierUnknownRevenueWarningThresholdPct = 15d;
    public const double SupplierUnknownRevenueCriticalThresholdPct = 25d;
    public const string SupplierTrustProvenance = "supplier_trust_contract_revenue_unknown_gate_v1";

    public static bool IsSupplierUnknownRevenueCritical(double unknownRevenueSharePct)
        => unknownRevenueSharePct >= SupplierUnknownRevenueCriticalThresholdPct;

    public static AnalyticsDecisionRecommendationEngine.RecommendationResult ApplySupplierPolicy(
        AnalyticsDecisionRecommendationEngine.RecommendationResult recommendation,
        bool hasComparableSignal,
        bool claimsPriceEvent)
    {
        if (claimsPriceEvent)
        {
            if (recommendation.Status == "do_not_trust"
                || recommendation.ReasonCodes.Contains("unknown_entity"))
            {
                return recommendation with
                {
                    ReasonCodes = recommendation.ReasonCodes
                        .Append("missing_comparable_signal")
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                };
            }

            return AnalyticsDecisionRecommendationEngine.ApplyComparableSignalGate(
                recommendation,
                hasComparableSignal);
        }

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
            ConfidencePct = Math.Round(Math.Max(0d, recommendation.ConfidencePct - 15d), 2),
            ReasonCodes = reasonCodes
        };
    }
}
