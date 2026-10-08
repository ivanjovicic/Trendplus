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
    DateTime? GeneratedAtUtc,
    DateTime? AsOfUtc = null,
    string EvidenceBasis = "source_latest_known",
    decimal? ExpectedImpactRsd = null,
    string? PriorityEvidence = null);

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
    string TenantScope,
    DateTime? AsOfUtc = null,
    string EvidenceBasis = "source_latest_known",
    decimal? ExpectedImpactRsd = null,
    string? PriorityEvidence = null);

public sealed record DecisionPulseProjection(
    bool SourceSucceeded,
    string? FailureCategory,
    string? FailureMessage,
    IReadOnlyList<DecisionPulseItem> Items,
    int SuppressedCount,
    string TenantScope);

/// <summary>
/// Projects product, inventory and supplier exceptions into Decision Pulse items.
/// Suppresses stale, empty, insufficient, blocked and error-as-zero evidence.
/// </summary>
public static class DecisionPulseProjector
{
    public const int MaxDigestItems = 10;
    public const string DedicatedTenantScope = "n/a_dedicated";
    public const string ProductDeepLink = "/analytics/products";
    public const string InventoryDeepLink = "/analytics/inventory";
    public const string SupplierDeepLink = "/analytics/supplier?tab=overview";
    public const string SourceTypeProduct = "product";
    public const string SourceTypeInventory = "inventory";
    public const string SourceTypeSupplier = "supplier";

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

    private static readonly HashSet<string> AllowedDataQuality = new(StringComparer.OrdinalIgnoreCase)
    {
        "good",
        "warning"
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

        var rankedItems = items
            .OrderByDescending(item => PriorityRank(item.PriorityEvidence))
            .ThenByDescending(item => item.ExpectedImpactRsd.HasValue)
            .ThenByDescending(item => item.ExpectedImpactRsd)
            .ToArray();
        suppressed += Math.Max(0, rankedItems.Length - MaxDigestItems);

        return new DecisionPulseProjection(
            true,
            null,
            null,
            rankedItems.Take(MaxDigestItems).ToArray(),
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

        if (!AllowedDataQuality.Contains(candidate.DataQualityStatus ?? string.Empty))
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
            candidate.InputFreshnessStatus?.Trim() ?? "unknown",
            deepLink,
            candidate.GeneratedAtUtc,
            DedicatedTenantScope,
            candidate.AsOfUtc,
            string.IsNullOrWhiteSpace(candidate.EvidenceBasis) ? "source_latest_known" : candidate.EvidenceBasis.Trim(),
            candidate.ExpectedImpactRsd,
            candidate.PriorityEvidence);

        return true;
    }

    private static int PriorityRank(string? priority)
        => (priority ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "p1" or "urgent" or "high" => 3,
            "p2" or "medium" => 2,
            "p3" or "low" => 1,
            _ => 0
        };
}
