namespace Application.Analytics.DecisionPulse;

/// <summary>
/// Candidate exception from an existing decision family. Pulse does not invent scores.
/// </summary>
public sealed record DecisionPulseCandidate(
    string Id,
    string SourceType,
    string SourceKey,
    string Title,
    string? WhySummary,
    IReadOnlyList<string> ReasonCodes,
    string RecommendationStatus,
    string RecommendationLabel,
    string DataQualityStatus,
    string InputFreshnessStatus,
    bool RecommendationAllowed,
    string DeepLink,
    DateTime? GeneratedAtUtc);

public sealed record DecisionPulseItem(
    string Id,
    string SourceType,
    string SourceKey,
    string Title,
    string WhySummary,
    IReadOnlyList<string> ReasonCodes,
    string RecommendationStatus,
    string RecommendationLabel,
    string DataQualityStatus,
    string InputFreshnessStatus,
    string DeepLink,
    DateTime? GeneratedAtUtc,
    string TenantScope);

public sealed record DecisionPulseProjection(
    bool SourceSucceeded,
    string? FailureCategory,
    string? FailureMessage,
    IReadOnlyList<DecisionPulseItem> Items,
    int SuppressedCount,
    string TenantScope);

/// <summary>
/// Projects product-decision exceptions into Decision Pulse items.
/// Suppresses stale, empty, insufficient, blocked and error-as-zero evidence.
/// </summary>
public static class DecisionPulseProjector
{
    public const string DedicatedTenantScope = "n/a_dedicated";
    public const string ProductDeepLink = "/analytics/products";
    public const string SourceTypeProduct = "product";

    private static readonly HashSet<string> ActionableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "REPLENISH",
        "MARKDOWN",
        "TRANSFER",
        "BOOST",
        "HOLD_BUY",
        "WATCH"
    };

    private static readonly HashSet<string> InsufficientStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSUFFICIENT_DATA",
        "FIX_DATA"
    };

    private static readonly HashSet<string> StaleFreshness = new(StringComparer.OrdinalIgnoreCase)
    {
        "stale",
        "critical",
        "unknown"
    };

    private static readonly HashSet<string> BadDataQuality = new(StringComparer.OrdinalIgnoreCase)
    {
        "insufficient_data",
        "error",
        "failed",
        "critical"
    };

    public static DecisionPulseProjection Project(
        IEnumerable<DecisionPulseCandidate>? candidates,
        bool sourceSucceeded,
        string? failureCategory = null,
        string? failureMessage = null)
    {
        if (!sourceSucceeded)
        {
            return new DecisionPulseProjection(
                false,
                failureCategory ?? "source_error",
                failureMessage ?? "Decision source failed; Pulse will not invent alerts.",
                Array.Empty<DecisionPulseItem>(),
                0,
                DedicatedTenantScope);
        }

        var suppressed = 0;
        var items = new List<DecisionPulseItem>();

        foreach (var candidate in candidates ?? Array.Empty<DecisionPulseCandidate>())
        {
            if (!TryProject(candidate, out var item))
            {
                suppressed++;
                continue;
            }

            items.Add(item!);
        }

        return new DecisionPulseProjection(
            true,
            null,
            null,
            items,
            suppressed,
            DedicatedTenantScope);
    }

    public static bool TryProject(DecisionPulseCandidate candidate, out DecisionPulseItem? item)
    {
        item = null;

        if (!candidate.RecommendationAllowed)
            return false;

        if (InsufficientStatuses.Contains(candidate.RecommendationStatus ?? string.Empty))
            return false;

        if (!ActionableStatuses.Contains(candidate.RecommendationStatus ?? string.Empty))
            return false;

        if (StaleFreshness.Contains(candidate.InputFreshnessStatus ?? string.Empty))
            return false;

        if (!string.Equals(candidate.InputFreshnessStatus, "fresh", StringComparison.OrdinalIgnoreCase))
            return false;

        if (BadDataQuality.Contains(candidate.DataQualityStatus ?? string.Empty))
            return false;

        var why = (candidate.WhySummary ?? string.Empty).Trim();
        if (why.Length == 0)
            return false;

        var deepLink = string.IsNullOrWhiteSpace(candidate.DeepLink)
            ? ProductDeepLink
            : candidate.DeepLink.Trim();

        item = new DecisionPulseItem(
            candidate.Id,
            string.IsNullOrWhiteSpace(candidate.SourceType) ? SourceTypeProduct : candidate.SourceType.Trim(),
            candidate.SourceKey,
            string.IsNullOrWhiteSpace(candidate.Title) ? candidate.SourceKey : candidate.Title.Trim(),
            why,
            candidate.ReasonCodes?.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()).ToArray()
                ?? Array.Empty<string>(),
            candidate.RecommendationStatus?.Trim().ToUpperInvariant() ?? string.Empty,
            string.IsNullOrWhiteSpace(candidate.RecommendationLabel)
                ? (candidate.RecommendationStatus?.Trim() ?? string.Empty)
                : candidate.RecommendationLabel.Trim(),
            candidate.DataQualityStatus?.Trim() ?? "good",
            "fresh",
            deepLink,
            candidate.GeneratedAtUtc,
            DedicatedTenantScope);

        return true;
    }
}
