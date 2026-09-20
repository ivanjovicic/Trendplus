namespace Trendplus2.Dtos;

/// <summary>
/// Machine-readable provenance for a critical analytics metric.
/// Values are strings for backward-compatible JSON evolution.
/// </summary>
public sealed class AnalyticsMetricProvenanceDto
{
    public string Kind { get; set; } = AnalyticsMetricProvenanceKinds.Unknown;
    public string Authority { get; set; } = AnalyticsMetricAuthority.Unknown;
    public string Actionability { get; set; } = AnalyticsMetricActionability.Unknown;
    public string? Unit { get; set; }
    public string? Denominator { get; set; }
}

public static class AnalyticsMetricProvenanceKinds
{
    public const string AuthoritativeBackendAggregate = "authoritative_backend_aggregate";
    public const string ObservedRowValue = "observed_row_value";
    public const string FrontendDisplayDerivation = "frontend_display_derivation";
    public const string ModeledEstimated = "modeled_estimated";
    public const string Unknown = "unknown";
}

public static class AnalyticsMetricAuthority
{
    public const string Authoritative = "authoritative";
    public const string Observed = "observed";
    public const string Derived = "derived";
    public const string Modeled = "modeled";
    public const string Unknown = "unknown";
}

public static class AnalyticsMetricActionability
{
    public const string Actionable = "actionable";
    public const string Informational = "informational";
    public const string Blocked = "blocked";
    public const string Unknown = "unknown";
}
