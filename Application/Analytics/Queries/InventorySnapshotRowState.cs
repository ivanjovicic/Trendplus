namespace Application.Analytics.Queries;

/// <summary>
/// Backend-owned row state shared by inventory alert and rebalance snapshot signals.
/// </summary>
public sealed record InventorySnapshotRowState(
    string Status,
    bool RecommendationAllowed,
    string DataQualityStatus,
    string ReasonLabel
);

public static class InventorySnapshotRowStateResolver
{
    public const string Actionable = "actionable";
    public const string Blocked = "blocked";
    public const string Good = "good";
    public const string Warning = "warning";
    public const string InsufficientData = "insufficient_data";

    public static InventorySnapshotRowState ForAlert(string? alertType, string? severity, decimal? confidenceScore)
    {
        if (severity is null || confidenceScore is null)
        {
            return new(
                Status: Blocked,
                RecommendationAllowed: false,
                DataQualityStatus: InsufficientData,
                ReasonLabel: "Alert nema dovoljno kompletnih podataka za preporuku.");
        }

        if (!IsKnownSeverity(severity) || !IsValidConfidence(confidenceScore.Value))
        {
            return new(
                Status: Blocked,
                RecommendationAllowed: false,
                DataQualityStatus: Warning,
                ReasonLabel: "Nivo inventory signala nije dovoljno precizan za preporuku.");
        }

        return new(
            Status: Actionable,
            RecommendationAllowed: true,
            DataQualityStatus: Good,
            ReasonLabel: AlertReasonLabel(alertType));
    }

    public static InventorySnapshotRowState ForRebalance(
        string? urgency,
        decimal? confidence,
        string? reason,
        int? recommendedQty,
        decimal? expectedSavedSales,
        decimal? expectedCapitalRelease)
    {
        if (recommendedQty is null || urgency is null || confidence is null || expectedSavedSales is null || expectedCapitalRelease is null || string.IsNullOrWhiteSpace(reason))
        {
            return new(
                Status: Blocked,
                RecommendationAllowed: false,
                DataQualityStatus: InsufficientData,
                ReasonLabel: "Predlog redistribucije nema dovoljno kompletnih podataka za preporuku.");
        }

        if (!IsKnownUrgency(urgency)
            || !IsValidConfidence(confidence.Value)
            || recommendedQty.Value < 0
            || expectedSavedSales.Value < 0
            || expectedCapitalRelease.Value < 0
            || IsUnknownReason(reason))
        {
            return new(
                Status: Blocked,
                RecommendationAllowed: false,
                DataQualityStatus: Warning,
                ReasonLabel: "Razlog redistribucije nije dovoljno precizan za preporuku.");
        }

        return new(
            Status: Actionable,
            RecommendationAllowed: true,
            DataQualityStatus: Good,
            ReasonLabel: RebalanceReasonLabel(reason));
    }

    private static bool IsKnownSeverity(string value)
        => value.Equals("critical", StringComparison.OrdinalIgnoreCase)
            || value.Equals("warning", StringComparison.OrdinalIgnoreCase)
            || value.Equals("info", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownUrgency(string value)
        => value.Equals("urgent", StringComparison.OrdinalIgnoreCase)
            || value.Equals("recommended", StringComparison.OrdinalIgnoreCase)
            || value.Equals("optional", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidConfidence(decimal value) => value is >= 0m and <= 1m;

    private static bool IsUnknownReason(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized == "snapshot"
            || normalized.Contains("unknown", StringComparison.Ordinal)
            || normalized.Contains("missing", StringComparison.Ordinal)
            || normalized.Contains("unavailable", StringComparison.Ordinal);
    }

    private static string AlertReasonLabel(string? alertType)
    {
        var normalized = alertType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("missing", StringComparison.Ordinal)
            || normalized.Contains("stockout", StringComparison.Ordinal)
            || normalized.Contains("oos", StringComparison.Ordinal))
        {
            return "Nedostatak zalihe zahteva proveru.";
        }

        if (normalized.Contains("dead", StringComparison.Ordinal)
            || normalized.Contains("slow", StringComparison.Ordinal))
        {
            return "Zaliha ima rizik sporog obrta.";
        }

        if (normalized.Contains("low", StringComparison.Ordinal))
        {
            return "Zaliha je ispod očekivanog nivoa.";
        }

        return "Inventory signal zahteva proveru.";
    }

    private static string RebalanceReasonLabel(string reason)
    {
        var normalized = reason.Trim().ToLowerInvariant();
        if (normalized.Contains("rebalance", StringComparison.Ordinal)
            || normalized.Contains("imbalance", StringComparison.Ordinal)
            || normalized.Contains("redistrib", StringComparison.Ordinal))
        {
            return "Raspodela zalihe između lokacija odstupa.";
        }

        if (normalized.Contains("stockout", StringComparison.Ordinal)
            || normalized.Contains("oos", StringComparison.Ordinal)
            || normalized.Contains("low_stock", StringComparison.Ordinal))
        {
            return "Rizik nestašice zalihe zahteva redistribuciju.";
        }

        if (normalized.Contains("overstock", StringComparison.Ordinal)
            || normalized.Contains("surplus", StringComparison.Ordinal))
        {
            return "Višak zalihe može opravdati redistribuciju.";
        }

        return "Predlog redistribucije zahteva proveru.";
    }
}
