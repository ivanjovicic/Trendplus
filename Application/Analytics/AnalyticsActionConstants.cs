namespace Application.Analytics;

/// <summary>
/// Centralized constants for Analytics Action Queue validation and standardization.
/// Ensures consistency across backend endpoints and services.
/// </summary>
public static class AnalyticsActionConstants
{
    // ── Source Types ────────────────────────────────────────────────────────

    /// <summary>
    /// Valid source types for analytics actions.
    /// Each represents a domain that can originate recommendations.
    /// </summary>
    public static class SourceTypes
    {
        public const string Dashboard = "dashboard";
        public const string Product = "product";
        public const string Supplier = "supplier";
        public const string Inventory = "inventory";
        public const string Nivelacija = "nivelacija";
        public const string DataQuality = "data_quality";

        public static readonly string[] AllValues = { Dashboard, Product, Supplier, Inventory, Nivelacija, DataQuality };
    }

    // ── Priorities ──────────────────────────────────────────────────────────

    /// <summary>
    /// Priority levels for actions.
    /// P1 = urgent, P2 = important, P3 = optional
    /// </summary>
    public static class Priorities
    {
        public const string P1 = "P1";
        public const string P2 = "P2";
        public const string P3 = "P3";

        public static readonly string[] AllValues = { P1, P2, P3 };
    }

    // ── Statuses ────────────────────────────────────────────────────────────

    /// <summary>
    /// Workflow statuses for actions.
    /// new → accepted/deferred → done/rejected
    /// </summary>
    public static class Statuses
    {
        public const string New = "new";
        public const string Accepted = "accepted";
        public const string Deferred = "deferred";
        public const string Rejected = "rejected";
        public const string Done = "done";

        public static readonly string[] AllValues = { New, Accepted, Deferred, Rejected, Done };

        /// <summary>
        /// Statuses that indicate an action is still open (not resolved).
        /// </summary>
        public static readonly string[] OpenStatuses = { New, Accepted, Deferred };

        /// <summary>
        /// Statuses that indicate an action is closed/resolved.
        /// </summary>
        public static readonly string[] ClosedStatuses = { Rejected, Done };
    }

    // ── Data Quality Statuses ───────────────────────────────────────────────

    /// <summary>
    /// Canonical data quality status values aligned with rest of analytics system.
    /// Legacy values (fair, poor) are normalized to canonical values at ingestion.
    /// </summary>
    public static class DataQualityStatuses
    {
        public const string Good = "good";
        public const string Warning = "warning";
        public const string Critical = "critical";
        public const string InsufficientData = "insufficient_data";

        public static readonly string[] AllValues = { Good, Warning, Critical, InsufficientData };

        /// <summary>
        /// Legacy data quality values that may appear in existing data.
        /// These should be normalized to canonical values when encountered.
        /// </summary>
        public static readonly Dictionary<string, string> LegacyMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            { "fair", Warning },
            { "poor", Critical }
        };
    }

    // ── Validation Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Validates that sourceType is one of the allowed values.
    /// </summary>
    public static bool IsValidSourceType(string? sourceType)
        => !string.IsNullOrWhiteSpace(sourceType) && SourceTypes.AllValues.Contains(sourceType);

    /// <summary>
    /// Validates that priority is one of the allowed values.
    /// </summary>
    public static bool IsValidPriority(string? priority)
        => !string.IsNullOrWhiteSpace(priority) && Priorities.AllValues.Contains(priority);

    /// <summary>
    /// Validates that status is one of the allowed values.
    /// </summary>
    public static bool IsValidStatus(string? status)
        => !string.IsNullOrWhiteSpace(status) && Statuses.AllValues.Contains(status);

    /// <summary>
    /// Validates that dataQualityStatus is one of the canonical values.
    /// </summary>
    public static bool IsValidDataQualityStatus(string? dataQualityStatus)
        => !string.IsNullOrWhiteSpace(dataQualityStatus) && DataQualityStatuses.AllValues.Contains(dataQualityStatus);

    /// <summary>
    /// Normalizes dataQualityStatus: converts legacy values (fair, poor) to canonical ones (warning, critical).
    /// Returns null if the input is already null/whitespace.
    /// Returns normalized value if valid/legacy, otherwise returns input unchanged for logging.
    /// </summary>
    public static string? NormalizeDataQualityStatus(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        var lower = rawValue.ToLowerInvariant();
        
        // Check if it's a legacy value that needs normalization
        if (DataQualityStatuses.LegacyMappings.TryGetValue(lower, out var normalized))
            return normalized;

        // If it's already a canonical value, keep it
        if (DataQualityStatuses.AllValues.Contains(lower))
            return lower;

        // Unknown value - return as-is (will fail validation downstream if needed)
        return rawValue;
    }
}
