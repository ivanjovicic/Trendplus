namespace Application.Analytics;

public static class AnalyticsDecisionReadinessStates
{
    public const string DecisionReady = "decision_ready";
    public const string SignalOnly = "signal_only";
    public const string Blocked = "blocked";
    public const string Unavailable = "unavailable";
}

public sealed record AnalyticsDecisionReadinessDto(
    string State,
    string SurfaceRole,
    bool? RecommendationAllowed,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> EvidenceReferences,
    string? RepairPath);

/// <summary>
/// Maps existing authoritative trust/recommendation metadata to a categorical
/// readiness state. It intentionally has no score or new business threshold.
/// </summary>
public static class AnalyticsDecisionReadinessPolicy
{
    private static readonly string[] BlockingQualityStates = ["critical", "insufficient_data"];
    private static readonly string[] BlockingFreshnessStates = ["stale", "critical", "unknown"];

    public static AnalyticsDecisionReadinessDto Resolve(
        bool success,
        string surfaceRole,
        bool? recommendationAllowed,
        string? dataQualityStatus,
        bool isPartial,
        string? freshnessStatus = null,
        IEnumerable<string>? reasonCodes = null,
        IEnumerable<string>? evidenceReferences = null,
        string? repairPath = null)
    {
        var normalizedRole = NormalizeRole(surfaceRole);
        var reasons = (reasonCodes ?? Array.Empty<string>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var evidence = (evidenceReferences ?? Array.Empty<string>())
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var normalizedQuality = dataQualityStatus?.Trim().ToLowerInvariant();
        var normalizedFreshness = freshnessStatus?.Trim().ToLowerInvariant();
        var qualityBlocked = BlockingQualityStates.Contains(normalizedQuality, StringComparer.Ordinal);
        var freshnessBlocked = BlockingFreshnessStates.Contains(normalizedFreshness, StringComparer.Ordinal);

        string state;
        if (!success)
        {
            state = AnalyticsDecisionReadinessStates.Unavailable;
            AddReason(reasons, "analytics_response_unavailable");
        }
        else if (!recommendationAllowed.HasValue)
        {
            state = AnalyticsDecisionReadinessStates.Unavailable;
            AddReason(reasons, "recommendation_eligibility_unknown");
        }
        else if (normalizedRole == "signal")
        {
            state = AnalyticsDecisionReadinessStates.SignalOnly;
            if (recommendationAllowed == false || qualityBlocked || freshnessBlocked || isPartial)
            {
                if (reasons.Count == 0)
                {
                    AddReason(reasons, ResolveBlockReason(qualityBlocked, freshnessBlocked, isPartial));
                }
            }
        }
        else if (recommendationAllowed == false || qualityBlocked || freshnessBlocked || isPartial)
        {
            state = AnalyticsDecisionReadinessStates.Blocked;
            if (reasons.Count == 0)
            {
                AddReason(reasons, ResolveBlockReason(qualityBlocked, freshnessBlocked, isPartial));
            }
        }
        else
        {
            state = AnalyticsDecisionReadinessStates.DecisionReady;
        }

        return new AnalyticsDecisionReadinessDto(
            state,
            normalizedRole,
            recommendationAllowed,
            reasons,
            evidence,
            string.IsNullOrWhiteSpace(repairPath) ? null : repairPath.Trim());
    }

    private static string NormalizeRole(string? role)
        => role?.Trim().ToLowerInvariant() switch
        {
            "signal" or "report" or "recommendation" => role!.Trim().ToLowerInvariant(),
            _ => "recommendation"
        };

    private static string ResolveBlockReason(bool qualityBlocked, bool freshnessBlocked, bool isPartial)
        => qualityBlocked
            ? "data_quality_blocks_decision"
            : freshnessBlocked
                ? "freshness_blocks_decision"
                : isPartial
                    ? "partial_evidence_blocks_decision"
                    : "recommendation_not_allowed";

    private static void AddReason(ICollection<string> reasons, string reason)
    {
        if (!reasons.Contains(reason, StringComparer.Ordinal)) reasons.Add(reason);
    }
}
