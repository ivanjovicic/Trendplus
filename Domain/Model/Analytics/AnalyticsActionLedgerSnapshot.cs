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
    string? OutcomeStatus,
    decimal? MeasuredImpactRsd,
    DateTime? OutcomeMeasuredAtUtc,
    int? MeasuredWindowDays,
    string? EvidenceSource,
    string? EvidenceReference,
    string? ResolutionNote
);

/// <summary>
/// RL04 read-only capture of recommendation lifecycle and measured-learning eligibility.
/// </summary>
public sealed record RecommendationLifecycleCaptureDto(
    string LifecycleState,
    string OutcomeEvidenceState,
    string OutcomeResult,
    bool LearningEligible,
    IReadOnlyList<string> LearningEligibilityReasonCodes,
    bool CountsTowardIssued,
    bool CountsTowardAccepted,
    bool CountsTowardRejected,
    bool CountsTowardIgnored,
    bool CountsTowardExecuted,
    bool CountsTowardMeasured,
    bool CountsTowardSuccess,
    bool CountsTowardNeutral,
    bool CountsTowardNegative,
    bool CountsTowardNotMeasured
);
