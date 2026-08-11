using Application.Analytics;
using Domain.Model.Analytics;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class RecommendationLifecycleSemanticsTests
{
    [Fact]
    public void ProjectIssuedRecommendation_IsNotLearningEligible()
    {
        var capture = RecommendationLifecycleSemantics.ProjectIssuedRecommendation();

        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Issued, capture.LifecycleState);
        Assert.Equal(RecommendationLifecycleSemantics.OutcomeEvidenceStates.Pending, capture.OutcomeEvidenceState);
        Assert.False(capture.LearningEligible);
        Assert.True(capture.CountsTowardIssued);
        Assert.False(capture.CountsTowardAccepted);
        Assert.False(capture.CountsTowardExecuted);
        Assert.False(capture.CountsTowardMeasured);
        Assert.False(capture.CountsTowardSuccess);
        Assert.Contains("acceptance_is_not_success", capture.LearningEligibilityReasonCodes);
    }

    [Fact]
    public void Project_AcceptedWithoutExecution_IsNotLearningEligible()
    {
        var item = CreateItem(
            status: AnalyticsActionConstants.Statuses.Accepted,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: "action_outcome_summary");

        var capture = RecommendationLifecycleSemantics.Project(item);

        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Accepted, capture.LifecycleState);
        Assert.False(capture.LearningEligible);
        Assert.True(capture.CountsTowardAccepted);
        Assert.False(capture.CountsTowardExecuted);
        Assert.False(capture.CountsTowardSuccess);
        Assert.Contains("execution_required_for_learning", capture.LearningEligibilityReasonCodes);
        Assert.Contains("acceptance_is_not_success", capture.LearningEligibilityReasonCodes);
    }

    [Fact]
    public void Project_ExecutedButNotMeasured_IsNotLearningEligible()
    {
        var item = CreateItem(
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.NotMeasured);

        var capture = RecommendationLifecycleSemantics.Project(item);

        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Executed, capture.LifecycleState);
        Assert.Equal(RecommendationLifecycleSemantics.OutcomeEvidenceStates.NotMeasured, capture.OutcomeEvidenceState);
        Assert.False(capture.LearningEligible);
        Assert.True(capture.CountsTowardExecuted);
        Assert.True(capture.CountsTowardNotMeasured);
        Assert.False(capture.CountsTowardMeasured);
        Assert.Contains("outcome_not_measured", capture.LearningEligibilityReasonCodes);
    }

    [Fact]
    public void Project_ExecutedMeasuredWithEvidence_IsLearningEligible()
    {
        var item = CreateItem(
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Negative,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: "action_outcome_summary");

        var capture = RecommendationLifecycleSemantics.Project(item);

        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Executed, capture.LifecycleState);
        Assert.Equal(RecommendationLifecycleSemantics.OutcomeEvidenceStates.Measured, capture.OutcomeEvidenceState);
        Assert.True(capture.LearningEligible);
        Assert.True(capture.CountsTowardMeasured);
        Assert.True(capture.CountsTowardNegative);
        Assert.False(capture.CountsTowardSuccess);
        Assert.Contains("measured_learning_eligible", capture.LearningEligibilityReasonCodes);
    }

    [Fact]
    public void Project_SuccessClaimWithoutEvidence_DoesNotCountAsMeasuredLearning()
    {
        var item = CreateItem(
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: null);

        var capture = RecommendationLifecycleSemantics.Project(item);

        Assert.Equal(RecommendationLifecycleSemantics.OutcomeEvidenceStates.NotMeasured, capture.OutcomeEvidenceState);
        Assert.False(capture.LearningEligible);
        Assert.False(capture.CountsTowardSuccess);
        Assert.Contains("missing_evidence_source", capture.LearningEligibilityReasonCodes);
    }

    [Fact]
    public void Project_PastDueNewAction_IsIgnored()
    {
        var item = CreateItem(
            status: AnalyticsActionConstants.Statuses.New,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Pending);
        item.DueAtUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        var capture = RecommendationLifecycleSemantics.Project(
            item,
            asOfUtc: new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Ignored, capture.LifecycleState);
        Assert.True(capture.CountsTowardIgnored);
        Assert.False(capture.LearningEligible);
    }

    [Fact]
    public void Project_Rejected_IsNotSuccessLearning()
    {
        var item = CreateItem(
            status: AnalyticsActionConstants.Statuses.Rejected,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Negative,
            outcomeMeasuredAtUtc: new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc),
            evidenceSource: "action_outcome_summary");

        var capture = RecommendationLifecycleSemantics.Project(item);

        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Rejected, capture.LifecycleState);
        Assert.True(capture.CountsTowardRejected);
        Assert.False(capture.LearningEligible);
        Assert.False(capture.CountsTowardNegative);
    }

    private static AnalyticsActionItem CreateItem(
        string status,
        string outcomeStatus,
        DateTime? outcomeMeasuredAtUtc = null,
        string? evidenceSource = null)
    {
        var item = new AnalyticsActionItem
        {
            Id = 1,
            SourceType = AnalyticsActionConstants.SourceTypes.Product,
            SourceKey = "product:rl04:1",
            Title = "RL04 test",
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
