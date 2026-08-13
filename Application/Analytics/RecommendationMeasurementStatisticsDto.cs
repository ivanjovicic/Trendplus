namespace Application.Analytics;

/// <summary>
/// RL06 read-only measurement-only statistics projection.
/// Counts come from <see cref="RecommendationLifecycleSemantics"/> flags; confidence is never mutated.
/// </summary>
public sealed record RecommendationMeasurementStatisticsDto(
    bool Success,
    int IssuedCount,
    int AcceptedCount,
    int RejectedCount,
    int IgnoredCount,
    int ExecutedCount,
    int MeasuredCount,
    int NotMeasuredCount,
    int SuccessCount,
    int NeutralCount,
    int NegativeCount,
    int PendingCount,
    decimal? AcceptanceRate,
    decimal? RejectionRate,
    decimal? IgnoredRate,
    decimal? ExecutionRate,
    decimal? MeasurementCoverageRate,
    decimal? NotMeasuredShare,
    decimal? PositiveOutcomeRate,
    decimal? NeutralOutcomeRate,
    decimal? NegativeOutcomeRate,
    IReadOnlyList<string> WarningCodes,
    string? EmptyReason
);
