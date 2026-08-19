using Application.Analytics;
using Domain.Model.Analytics;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class RecommendationMeasurementStatisticsProjectionTests
{
    private static readonly DateTime AsOfUtc = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Project_EmptyCohort_KeepsNullRatesAndExplicitEmptyReason()
    {
        var stats = RecommendationMeasurementStatisticsProjection.Project(Array.Empty<AnalyticsActionItem>(), AsOfUtc);

        Assert.True(stats.Success);
        Assert.Equal(RecommendationMeasurementStatisticsProjection.EmptyReasonNoRows, stats.EmptyReason);
        Assert.Equal(0, stats.IssuedCount);
        Assert.Equal(0, stats.MeasuredCount);
        Assert.Null(stats.AcceptanceRate);
        Assert.Null(stats.ExecutionRate);
        Assert.Null(stats.MeasurementCoverageRate);
        Assert.Null(stats.PositiveOutcomeRate);
        Assert.Null(stats.NegativeOutcomeRate);
        Assert.Empty(stats.WarningCodes);
    }

    [Fact]
    public void Project_AcceptedWithoutExecution_DoesNotCountAsSuccess()
    {
        var accepted = CreateItem(
            status: AnalyticsActionConstants.Statuses.Accepted,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: "action_outcome_summary");

        var stats = RecommendationMeasurementStatisticsProjection.Project([accepted], AsOfUtc);

        Assert.Equal(1, stats.IssuedCount);
        Assert.Equal(1, stats.AcceptedCount);
        Assert.Equal(0, stats.ExecutedCount);
        Assert.Equal(0, stats.MeasuredCount);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(1.0000m, stats.AcceptanceRate);
        Assert.Equal(0.0000m, stats.ExecutionRate);
        Assert.Null(stats.PositiveOutcomeRate);
        Assert.Null(stats.EmptyReason);
    }

    [Fact]
    public void Project_ExecutedWithoutMeasurement_DoesNotCountAsSuccess()
    {
        var executed = CreateItem(
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.NotMeasured);

        var stats = RecommendationMeasurementStatisticsProjection.Project([executed], AsOfUtc);

        Assert.Equal(1, stats.ExecutedCount);
        Assert.Equal(0, stats.MeasuredCount);
        Assert.Equal(1, stats.NotMeasuredCount);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Null(stats.PositiveOutcomeRate);
        Assert.Equal(1.0000m, stats.NotMeasuredShare);
        Assert.Equal(0.0000m, stats.MeasurementCoverageRate);
    }

    [Fact]
    public void Project_MissingMeasuredEvidence_StaysNotMeasured()
    {
        var claimedWithoutEvidence = CreateItem(
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: null);

        var stats = RecommendationMeasurementStatisticsProjection.Project([claimedWithoutEvidence], AsOfUtc);

        Assert.Equal(1, stats.ExecutedCount);
        Assert.Equal(0, stats.MeasuredCount);
        Assert.Equal(1, stats.NotMeasuredCount);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Null(stats.PositiveOutcomeRate);
    }

    [Fact]
    public void Project_MeasuredSuccessAndNegative_UsesMeasuredDenominator()
    {
        var success = CreateItem(
            id: 1,
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: "action_outcome_summary");
        var negative = CreateItem(
            id: 2,
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Negative,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: "action_outcome_summary");
        var pending = CreateItem(
            id: 3,
            status: AnalyticsActionConstants.Statuses.New,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Pending);

        var stats = RecommendationMeasurementStatisticsProjection.Project([success, negative, pending], AsOfUtc);

        Assert.Equal(3, stats.IssuedCount);
        Assert.Equal(2, stats.AcceptedCount);
        Assert.Equal(2, stats.ExecutedCount);
        Assert.Equal(2, stats.MeasuredCount);
        Assert.Equal(1, stats.SuccessCount);
        Assert.Equal(1, stats.NegativeCount);
        Assert.Equal(1, stats.PendingCount);
        Assert.Equal(0.5000m, stats.PositiveOutcomeRate);
        Assert.Equal(0.5000m, stats.NegativeOutcomeRate);
        Assert.Null(stats.EmptyReason);
    }

    [Fact]
    public void Project_Rejected_IsNotSuccessAndWarns()
    {
        var rejected = CreateItem(
            status: AnalyticsActionConstants.Statuses.Rejected,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Negative,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: "action_outcome_summary");

        var stats = RecommendationMeasurementStatisticsProjection.Project([rejected], AsOfUtc);

        Assert.Equal(1, stats.RejectedCount);
        Assert.Equal(0, stats.ExecutedCount);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(0, stats.NegativeCount);
        Assert.Null(stats.PositiveOutcomeRate);
        Assert.Contains(RecommendationMeasurementStatisticsProjection.WarningRejectedActionsPresent, stats.WarningCodes);
    }

    private static AnalyticsActionItem CreateItem(
        string status,
        string outcomeStatus,
        DateTime? outcomeMeasuredAtUtc = null,
        string? evidenceSource = null,
        long id = 1)
    {
        var item = new AnalyticsActionItem
        {
            Id = id,
            SourceType = AnalyticsActionConstants.SourceTypes.Product,
            SourceKey = $"product:rl06:{id}",
            Title = "RL06 test",
            Priority = AnalyticsActionConstants.Priorities.P2,
            Status = status,
            OutcomeStatus = outcomeStatus,
            OutcomeMeasuredAtUtc = outcomeMeasuredAtUtc,
            CreatedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        if (!string.IsNullOrWhiteSpace(evidenceSource))
        {
            item.LedgerSnapshot = new AnalyticsActionLedgerSnapshot(
                SchemaVersion: 1,
                CreationSnapshot: null,
                ResolutionSnapshot: new AnalyticsActionResolutionSnapshot(
                    OutcomeStatus: outcomeStatus,
                    MeasuredImpactRsd: 10m,
                    OutcomeMeasuredAtUtc: outcomeMeasuredAtUtc,
                    MeasuredWindowDays: 14,
                    EvidenceSource: evidenceSource,
                    EvidenceReference: "ref-1",
                    ResolutionNote: null));
        }

        return item;
    }
}
