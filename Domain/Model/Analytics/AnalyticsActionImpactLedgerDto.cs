namespace Domain.Model.Analytics;

public sealed record AnalyticsActionImpactLedgerDto(
    int Version,
    string? SourceRecommendationId,
    string SourceRecommendationIdDerivation,
    DateTime CapturedAtUtc,
    AnalyticsActionImpactLedgerSnapshotDto Snapshot,
    AnalyticsActionImpactLedgerResolutionDto Resolution,
    AnalyticsActionImpactLedgerDerivedDto Derived);

public sealed record AnalyticsActionImpactLedgerSnapshotDto(
    string ExpectedImpactBasis,
    IReadOnlyList<string> PrimaryDrivers,
    string DecisionReason,
    int? ImpactWindowDays,
    string RecommendedAction,
    string InputFreshnessStatus,
    string? SourceModule,
    DateTime? SourcePeriodStartUtc,
    DateTime? SourcePeriodEndUtc);

public sealed record AnalyticsActionImpactLedgerResolutionDto(
    string OutcomeStatus,
    decimal? MeasuredImpactRsd,
    DateTime? OutcomeMeasuredAtUtc,
    DateTime? ResolvedAtUtc,
    string? EvidenceSource,
    int? MeasuredWindowDays,
    string? ResolutionNote,
    string? MeasurementMethod);

public sealed record AnalyticsActionImpactLedgerDerivedDto(
    decimal? ImpactDeltaRsd,
    decimal? RealizationRatio,
    string CalibrationBucket,
    bool HasEvidence);
