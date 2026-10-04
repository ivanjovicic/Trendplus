namespace Application.Analytics;

public static class OperationsAnalyticsIntegrityStates
{
    public const string Verified = "verified";
    public const string Unverified = "unverified";
    public const string Degraded = "degraded";
    public const string DriftDetected = "drift_detected";

    public static bool BlocksDecisionSignals(string? status)
        => string.Equals(status, DriftDetected, StringComparison.Ordinal);
}

public static class OperationsAnalyticsIntegrityFamilies
{
    public const string SupplierShoeType = "supplier_shoe_type";
    public const string SalesDashboard = "sales_dashboard";
    public const string Inventory = "inventory";
    public const string DataQuality = "data_quality";
    public const string DecisionBoard = "decision_board";
    public const string Nivelacija = "nivelacija";

    public static IReadOnlyList<OperationsAnalyticsIntegrityFamilyDefinition> Enrolled { get; } =
    [
        new(SupplierShoeType, "Analytics Reliability / Supplier and Shoe Type certification", 10000, 31, true),
        new(SalesDashboard, "Analytics Reliability / Sales and Dashboard totals", 10000, 31, false),
        new(Inventory, "Analytics Reliability / Inventory identity", 10000, 31, true),
        new(DataQuality, "Analytics Reliability / Data Quality identity", 10000, 31, false),
        new(DecisionBoard, "Analytics Reliability / Decision Board contributors", 10000, 31, true),
        new(Nivelacija, "Analytics Reliability / Nivelacija Pre/Post and Priorities", 10000, 31, true)
    ];

    public static IReadOnlyList<string> ResolveAffected(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
            return Enrolled.Select(static definition => definition.Family).ToArray();

        if (family.Contains(',', StringComparison.Ordinal))
        {
            return family
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .SelectMany(ResolveAffected)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        var normalized = family.Trim().ToLowerInvariant();
        if (normalized is "all" or "analytics" or "cache_clear" or "access_import")
            return Enrolled.Select(static definition => definition.Family).ToArray();

        var resolved = normalized switch
        {
            "supplier-sales" or "supplier" or "shoe-type" or "shoetype" or "color" => SupplierShoeType,
            "sales" or "dashboard" or "sales-dashboard" => SalesDashboard,
            "inventory" or "inventory-alerts" or "inventory-snapshot" => Inventory,
            "data-quality" or "dataquality" => DataQuality,
            "decision-board" or "decisionboard" => DecisionBoard,
            "nivelacija" or "pre-post" or "prepost" or "vendor-sales-nivelacija" or "pre-nivelacija" or "pre-nivelacija-prioriteti" => Nivelacija,
            _ => null
        };

        return resolved is null
            ? Enrolled.Select(static definition => definition.Family).ToArray()
            : [resolved];
    }

    public static OperationsAnalyticsIntegrityFamilyDefinition DefinitionFor(string family)
        => Enrolled.Single(definition => string.Equals(definition.Family, family, StringComparison.Ordinal));
}

/// <summary>
/// Builds the exact context identity used by a bounded Operations integrity probe.
/// Endpoint metadata can use it to avoid presenting proof from another filter or
/// invalidation generation as current for the selected request.
/// </summary>
public static class OperationsAnalyticsIntegrityContextPolicy
{
    public static string CreateFingerprint(
        string family,
        string sourceGeneration,
        DateTime fromUtc,
        DateTime toUtc,
        string dataScope,
        int? storeId)
        => AnalyticsContextFingerprintPolicy.Create(
            sourceDataset: $"integrity:{family}",
            sourceGeneration: sourceGeneration,
            formulaVersion: "analytics_integrity_probe_v2",
            materializerGeneration: "bounded-independent-probe",
            rowLimitSemantics: $"max_rows:{OperationsAnalyticsIntegrityFamilies.DefinitionFor(family).MaxRows}",
            requestedPeriodFromUtc: fromUtc,
            requestedPeriodToUtc: toUtc,
            effectivePeriodFromUtc: fromUtc,
            effectivePeriodToUtc: toUtc,
            observedPeriodFromUtc: fromUtc,
            observedPeriodToUtc: toUtc,
            requestedDataScope: dataScope,
            effectiveDataScope: dataScope,
            dataScopeSource: "operations_integrity_probe",
            populationKey: family,
            populationFilters: new Dictionary<string, string?>
            {
                ["store_id"] = storeId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["family"] = family
            },
            cacheGeneration: null,
            resultState: AnalyticsContextFingerprintPolicy.StateAvailable).Fingerprint!;
}

public sealed record OperationsAnalyticsIntegrityFamilyDefinition(
    string Family,
    string Owner,
    int MaxRows,
    int MaxWindowDays,
    bool BlocksDecisionSignals);

public sealed record OperationsAnalyticsIntegrityProbeRequest(
    OperationsAnalyticsIntegrityFamilyDefinition Definition,
    string ContextFingerprint,
    string SourceGeneration,
    DateTime FromUtc,
    DateTime ToUtc,
    string DataScope,
    string Trigger,
    int MaxRows,
    CancellationToken CancellationToken);

public sealed record OperationsAnalyticsIntegrityProbeResult(
    string Status,
    string Summary,
    IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta> Deltas,
    bool BlocksDecisionSignals,
    int? ProbeRowCount = null)
{
    public static OperationsAnalyticsIntegrityProbeResult Verified(
        string summary,
        IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta>? deltas = null,
        int? probeRowCount = null)
        => new(
            OperationsAnalyticsIntegrityStates.Verified,
            summary,
            deltas ?? Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false,
            ProbeRowCount: probeRowCount);

    public static OperationsAnalyticsIntegrityProbeResult Unverified(string summary, int? probeRowCount = null)
        => new(
            OperationsAnalyticsIntegrityStates.Unverified,
            summary,
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false,
            ProbeRowCount: probeRowCount);

    public static OperationsAnalyticsIntegrityProbeResult Degraded(string summary, int? probeRowCount = null)
        => new(
            OperationsAnalyticsIntegrityStates.Degraded,
            summary,
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false,
            ProbeRowCount: probeRowCount);

    public static OperationsAnalyticsIntegrityProbeResult DriftDetected(
        string summary,
        IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta> deltas,
        int? probeRowCount = null)
        => new(
            OperationsAnalyticsIntegrityStates.DriftDetected,
            summary,
            deltas,
            BlocksDecisionSignals: true,
            ProbeRowCount: probeRowCount);
}

/// <summary>
/// Family-owned, independently derived bounded integrity probe. A probe must not
/// call the production aggregation it certifies or silently turn missing proof into zero.
/// </summary>
public interface IOperationsAnalyticsIntegrityFamilyProbe
{
    string Family { get; }

    Task<OperationsAnalyticsIntegrityProbeResult> ProbeAsync(
        OperationsAnalyticsIntegrityProbeRequest request);
}

public sealed record OperationsAnalyticsIntegrityProbeDelta(
    string Dimension,
    decimal EndpointOrLiveRevenue,
    decimal OracleRevenue,
    decimal RevenueDelta,
    int EndpointOrLiveUnits,
    int OracleUnits,
    int UnitsDelta);

public sealed record OperationsAnalyticsIntegritySnapshot(
    string Status,
    string EvidenceId,
    DateTime CheckedAtUtc,
    DateTime? LastVerifiedAtUtc,
    string? Trigger,
    string? Summary,
    IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta> Deltas,
    bool BlocksDecisionSignals)
{
    public string Family { get; set; } = OperationsAnalyticsIntegrityFamilies.SupplierShoeType;
    public string? ContextFingerprint { get; set; }
    public string? SourceGeneration { get; set; }
    public DateTime? ProbeWindowFromUtc { get; set; }
    public DateTime? ProbeWindowToUtc { get; set; }
    public int? ProbeRowCount { get; set; }

    public static OperationsAnalyticsIntegritySnapshot Unverified(string evidenceId, string trigger, string summary)
        => new(
            OperationsAnalyticsIntegrityStates.Unverified,
            evidenceId,
            DateTime.UtcNow,
            null,
            trigger,
            summary,
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false);

    public static OperationsAnalyticsIntegritySnapshot Degraded(string evidenceId, string trigger, string summary)
        => new(
            OperationsAnalyticsIntegrityStates.Degraded,
            evidenceId,
            DateTime.UtcNow,
            null,
            trigger,
            summary,
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false);
}
