namespace Domain.Model.Analytics;

public sealed record AnalyticsActionLedgerSnapshot(
    int SchemaVersion,
    AnalyticsActionCreationSnapshot? CreationSnapshot,
    AnalyticsActionResolutionSnapshot? ResolutionSnapshot,
    AnalyticsActionDecisionEvidenceSnapshot? EvidenceSnapshot = null
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
/// DEX10 immutable decision-evidence freeze captured when a recommendation is acted on.
/// </summary>
public sealed record AnalyticsActionDecisionEvidenceSnapshot(
    int SchemaVersion,
    DateTime CapturedAtUtc,
    string RecommendationId,
    string RecommendationType,
    string? PeriodFromUtc,
    string? PeriodToUtc,
    string DataQualityStatus,
    string ConfidenceLevel,
    int? ConfidenceScore,
    int ConfidencePct,
    int ReliabilityPct,
    string InputFreshnessStatus,
    string ExplainabilityText,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> WarningCodes,
    IReadOnlyList<string> PrimaryDrivers,
    IReadOnlyList<AnalyticsActionEvidenceNodeSnapshot> EvidenceChain,
    IReadOnlyList<AnalyticsActionEvidenceNodeSnapshot> ConfidenceBreakdown
);

public sealed record AnalyticsActionEvidenceNodeSnapshot(
    string Category,
    string Code,
    string Label,
    string ValueText,
    IReadOnlyList<string> SourceFields,
    bool IsMissing,
    string? Detail
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
