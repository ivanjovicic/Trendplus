namespace Domain.Model.Analytics;

public sealed record AnalyticsActionLedgerSnapshot(
    int SchemaVersion,
    AnalyticsActionCreationSnapshot? CreationSnapshot,
    AnalyticsActionResolutionSnapshot? ResolutionSnapshot
);

public sealed record AnalyticsActionCreationSnapshot(
    string SourceRecommendationId,
    string RecommendationType,
    string? ExpectedImpactBasis,
    int? ImpactWindowDays,
    string ConfidenceLevel,
    IReadOnlyList<string> WarningCodes,
    IReadOnlyList<string> PrimaryDrivers,
    string DecisionReason,
    string RecommendedAction,
    DateTime? GeneratedAtUtc,
    string InputFreshnessStatus
);

public sealed record AnalyticsActionResolutionSnapshot(
    int? MeasuredWindowDays,
    string? EvidenceSource,
    string? EvidenceReference,
    string? ResolutionNote
);
