using Domain.Model.Analytics;

namespace Application.Analytics;

/// <summary>
/// RL06 deterministic measurement-only statistics over existing action rows.
/// Lifecycle counts stay separate from measured outcome evidence; empty denominators stay null.
/// </summary>
public static class RecommendationMeasurementStatisticsProjection
{
    public const string EmptyReasonNoRows = "no_rows";
    public const string WarningSmallSample = "small_sample";
    public const string WarningSmallMeasuredSample = "small_measured_sample";
    public const string WarningOutcomeCoverageLow = "outcome_coverage_low";
    public const string WarningRejectedActionsPresent = "rejected_actions_present";

    public static RecommendationMeasurementStatisticsDto Project(
        IEnumerable<AnalyticsActionItem> items,
        DateTime? asOfUtc = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        var nowUtc = asOfUtc ?? DateTime.UtcNow;
        var captures = items
            .Select(item => RecommendationLifecycleSemantics.Project(item, nowUtc))
            .ToArray();

        if (captures.Length == 0)
        {
            return CreateEmpty(EmptyReasonNoRows);
        }

        var issuedCount = captures.Count(capture => capture.CountsTowardIssued);
        var acceptedCount = captures.Count(capture => capture.CountsTowardAccepted);
        var rejectedCount = captures.Count(capture => capture.CountsTowardRejected);
        var ignoredCount = captures.Count(capture => capture.CountsTowardIgnored);
        var executedCount = captures.Count(capture => capture.CountsTowardExecuted);
        var measuredCount = captures.Count(capture => capture.CountsTowardMeasured);
        var notMeasuredCount = captures.Count(capture => capture.CountsTowardNotMeasured);
        var successCount = captures.Count(capture => capture.CountsTowardSuccess);
        var neutralCount = captures.Count(capture => capture.CountsTowardNeutral);
        var negativeCount = captures.Count(capture => capture.CountsTowardNegative);
        var pendingCount = captures.Count(capture =>
            string.Equals(
                capture.OutcomeEvidenceState,
                RecommendationLifecycleSemantics.OutcomeEvidenceStates.Pending,
                StringComparison.OrdinalIgnoreCase));

        var measurementCoverageRate = Rate(measuredCount, executedCount);
        var warningCodes = BuildWarningCodes(
            issuedCount,
            measuredCount,
            rejectedCount,
            measurementCoverageRate);

        return new RecommendationMeasurementStatisticsDto(
            Success: true,
            IssuedCount: issuedCount,
            AcceptedCount: acceptedCount,
            RejectedCount: rejectedCount,
            IgnoredCount: ignoredCount,
            ExecutedCount: executedCount,
            MeasuredCount: measuredCount,
            NotMeasuredCount: notMeasuredCount,
            SuccessCount: successCount,
            NeutralCount: neutralCount,
            NegativeCount: negativeCount,
            PendingCount: pendingCount,
            AcceptanceRate: Rate(acceptedCount, issuedCount),
            RejectionRate: Rate(rejectedCount, issuedCount),
            IgnoredRate: Rate(ignoredCount, issuedCount),
            ExecutionRate: Rate(executedCount, acceptedCount),
            MeasurementCoverageRate: measurementCoverageRate,
            NotMeasuredShare: Rate(notMeasuredCount, executedCount),
            PositiveOutcomeRate: Rate(successCount, measuredCount),
            NeutralOutcomeRate: Rate(neutralCount, measuredCount),
            NegativeOutcomeRate: Rate(negativeCount, measuredCount),
            WarningCodes: warningCodes,
            EmptyReason: null);
    }

    public static RecommendationMeasurementStatisticsDto CreateEmpty(string emptyReason)
        => new(
            Success: true,
            IssuedCount: 0,
            AcceptedCount: 0,
            RejectedCount: 0,
            IgnoredCount: 0,
            ExecutedCount: 0,
            MeasuredCount: 0,
            NotMeasuredCount: 0,
            SuccessCount: 0,
            NeutralCount: 0,
            NegativeCount: 0,
            PendingCount: 0,
            AcceptanceRate: null,
            RejectionRate: null,
            IgnoredRate: null,
            ExecutionRate: null,
            MeasurementCoverageRate: null,
            NotMeasuredShare: null,
            PositiveOutcomeRate: null,
            NeutralOutcomeRate: null,
            NegativeOutcomeRate: null,
            WarningCodes: Array.Empty<string>(),
            EmptyReason: emptyReason);

    private static decimal? Rate(int numerator, int denominator)
        => denominator > 0
            ? Math.Round((decimal)numerator / denominator, 4, MidpointRounding.AwayFromZero)
            : null;

    private static IReadOnlyList<string> BuildWarningCodes(
        int issuedCount,
        int measuredCount,
        int rejectedCount,
        decimal? measurementCoverageRate)
    {
        var warnings = new List<string>();
        if (issuedCount < 10)
        {
            warnings.Add(WarningSmallSample);
        }

        if (measuredCount < 10)
        {
            warnings.Add(WarningSmallMeasuredSample);
        }

        if (measurementCoverageRate.HasValue && measurementCoverageRate.Value < 0.5m)
        {
            warnings.Add(WarningOutcomeCoverageLow);
        }

        if (rejectedCount > 0)
        {
            warnings.Add(WarningRejectedActionsPresent);
        }

        return warnings;
    }
}
