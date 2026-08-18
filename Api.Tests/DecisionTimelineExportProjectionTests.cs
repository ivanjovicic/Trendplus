using Application.Analytics;
using Domain.Model.Analytics;
using Infrastructure.Services.Analytics;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class DecisionTimelineExportProjectionTests
{
    private static readonly DateTime GeneratedAtUtc = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Export_OutsidePeriod_KeepsRequestedPeriodAndDoesNotInventRows()
    {
        var items = new[]
        {
            CreateAction(
                id: 2,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                recommendationType: "REPLENISH")
        };
        var periodFrom = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodTo = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

        var filtered = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:101",
                ProductId: 101,
                RecommendationType: "REPLENISH",
                PeriodFromUtc: periodFrom,
                PeriodToUtc: periodTo));
        var export = DecisionTimelineExportProjection.FromFilter(filtered, GeneratedAtUtc);
        var csv = DecisionTimelineExportProjection.ToCsv(export);

        Assert.True(export.Success);
        Assert.Equal(AnalyticsActionTimelineFilterProjection.EmptyReasonOutsidePeriod, export.Header.EmptyReason);
        Assert.Equal(periodFrom.Date, export.Header.RequestedPeriodFromUtc.Date);
        Assert.Equal(periodTo.Date, export.Header.RequestedPeriodToUtc.Date);
        Assert.Equal(export.Header.RequestedPeriodFromUtc, export.Header.EffectivePeriodFromUtc);
        Assert.Equal(export.Header.RequestedPeriodToUtc, export.Header.EffectivePeriodToUtc);
        Assert.Empty(export.Rows);
        Assert.NotNull(export.Funnel);
        Assert.Equal(0, export.Funnel.IssuedCount);
        Assert.Null(export.Funnel.SuccessRate);
        Assert.Null(export.Funnel.AcceptanceRate);
        Assert.Contains("emptyReason=outside_period", csv);
        Assert.Contains("requestedPeriodFromUtc=2026-08-01", csv);
        Assert.Contains("effectivePeriodFromUtc=2026-08-01", csv);
        Assert.DoesNotContain("0%", csv);
        Assert.DoesNotContain("successRate=0", csv);
        Assert.DoesNotContain("recommendation_issued", csv);
    }

    [Fact]
    public void Export_Error_DoesNotEmitZeroRatesOrFakeEvents()
    {
        var export = DecisionTimelineExportProjection.Error(
            requestedPeriodFromUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            requestedPeriodToUtc: new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc),
            generatedAtUtc: GeneratedAtUtc,
            errorCode: "ANALYTICS_UNEXPECTED_ERROR",
            errorMessage: "Decision Timeline export trenutno nije dostupan.");
        var csv = DecisionTimelineExportProjection.ToCsv(export);

        Assert.False(export.Success);
        Assert.Null(export.Funnel);
        Assert.Empty(export.Rows);
        Assert.Null(export.Header.EmptyReason);
        Assert.Equal(export.Header.RequestedPeriodFromUtc, export.Header.EffectivePeriodFromUtc);
        Assert.Contains("success=false", csv);
        Assert.Contains("errorCode=ANALYTICS_UNEXPECTED_ERROR", csv);
        Assert.DoesNotContain("issuedCount=", csv);
        Assert.DoesNotContain("successRate=", csv);
        Assert.DoesNotContain("0%", csv);
        Assert.DoesNotContain("timelineId,", csv);
    }

    [Fact]
    public void Export_DoesNotInventMissingStages_AndMarksAbsentSnapshots()
    {
        var items = new[]
        {
            CreateAction(
                id: 3,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc))
        };

        var filtered = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:101",
                ProductId: 101,
                RecommendationType: null,
                PeriodFromUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodToUtc: new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)));
        var export = DecisionTimelineExportProjection.FromFilter(filtered, GeneratedAtUtc);
        var csv = DecisionTimelineExportProjection.ToCsv(export);

        Assert.True(export.Success);
        var row = Assert.Single(export.Rows);
        Assert.Contains(row.Events, item => item.EventType == "recommendation_issued");
        Assert.DoesNotContain(row.Events, item => item.EventType == "action_accepted");
        Assert.DoesNotContain(row.Events, item => item.EventType == "action_executed");
        Assert.DoesNotContain(row.Events, item => item.EventType == "outcome_measured");
        Assert.Null(row.AcceptedAtUtc);
        Assert.Null(row.ExecutedAtUtc);
        Assert.Null(row.OutcomeMeasuredAtUtc);
        Assert.False(row.CreationSnapshotPresent);
        Assert.False(row.ResolutionSnapshotPresent);
        Assert.False(row.EvidenceSnapshotPresent);
        Assert.Equal(DecisionTimelineExportProjection.AbsenceLegacyPartialHistory, row.SnapshotAbsenceReason);
        Assert.Equal(DecisionTimelineExportProjection.WorkflowStatusSourceLiveLookup, row.WorkflowStatusSource);
        Assert.Equal(DecisionTimelineExportProjection.SnapshotCoverageAbsent, export.Header.SnapshotCoverage);
        Assert.Null(export.Funnel!.SuccessRate);
        Assert.Null(export.Funnel.MeasuredRate);
        Assert.Contains("creationSnapshotPresent,resolutionSnapshotPresent,evidenceSnapshotPresent", csv);
        Assert.Contains("legacy_partial_history", csv);
        Assert.DoesNotContain("0%", csv);
        Assert.DoesNotContain("action_accepted", csv);
    }

    [Fact]
    public void Export_RejectedIsNotDone_AndDoesNotCountAsExecuted()
    {
        var issuedAt = new DateTime(2026, 8, 5, 8, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            CreateAction(
                id: 11,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: issuedAt,
                recommendationType: "REPLENISH",
                status: AnalyticsActionConstants.Statuses.Rejected,
                notes: new[]
                {
                    Note(1, 11, AnalyticsActionConstants.Statuses.New, AnalyticsActionConstants.Statuses.Rejected, issuedAt.AddHours(1)),
                }),
            CreateAction(
                id: 12,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: issuedAt.AddMinutes(10),
                recommendationType: "REPLENISH",
                status: AnalyticsActionConstants.Statuses.Done,
                notes: new[]
                {
                    Note(2, 12, AnalyticsActionConstants.Statuses.New, AnalyticsActionConstants.Statuses.Accepted, issuedAt.AddHours(2)),
                    Note(3, 12, AnalyticsActionConstants.Statuses.Accepted, AnalyticsActionConstants.Statuses.Done, issuedAt.AddHours(3)),
                }),
        };

        var export = Export(items);
        var csv = DecisionTimelineExportProjection.ToCsv(export);
        var rejected = Assert.Single(export.Rows, row => row.ActionId == 11);
        var done = Assert.Single(export.Rows, row => row.ActionId == 12);

        Assert.Equal(AnalyticsActionConstants.Statuses.Rejected, rejected.CurrentStatus);
        Assert.NotEqual(AnalyticsActionConstants.Statuses.Done, rejected.CurrentStatus);
        Assert.Equal(AnalyticsActionConstants.Statuses.Rejected, rejected.ProjectionState);
        Assert.Contains(rejected.Events, item => item.EventType == "action_rejected");
        Assert.DoesNotContain(rejected.Events, item => item.EventType == "action_executed");
        Assert.Null(rejected.ExecutedAtUtc);
        Assert.Equal(AnalyticsActionConstants.Statuses.Done, done.CurrentStatus);
        Assert.Contains(done.Events, item => item.EventType == "action_executed");
        Assert.DoesNotContain(done.Events, item => item.EventType == "action_rejected");
        Assert.Equal(2, export.Funnel!.IssuedCount);
        Assert.Equal(1, export.Funnel.RejectedCount);
        Assert.Equal(1, export.Funnel.AcceptedCount);
        Assert.Equal(1, export.Funnel.ExecutedCount);
        Assert.Equal(0, export.Funnel.SuccessCount);
        Assert.Null(export.Funnel.SuccessRate);
        Assert.Contains("action_rejected", csv);
        Assert.Contains("action_executed", csv);
        Assert.DoesNotContain("currentStatus,done", csv);
        Assert.DoesNotContain("0%", csv);
        AssertParityWithSlice2(items, export);
    }

    [Fact]
    public void Export_NotMeasured_IsNotSuccessOrFailure()
    {
        var issuedAt = new DateTime(2026, 8, 5, 8, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            CreateAction(
                id: 21,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: issuedAt,
                recommendationType: "REPLENISH",
                status: AnalyticsActionConstants.Statuses.Done,
                outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.NotMeasured,
                resolvedAtUtc: issuedAt.AddDays(2),
                notes: new[]
                {
                    Note(1, 21, AnalyticsActionConstants.Statuses.New, AnalyticsActionConstants.Statuses.Accepted, issuedAt.AddHours(1)),
                    Note(2, 21, AnalyticsActionConstants.Statuses.Accepted, AnalyticsActionConstants.Statuses.Done, issuedAt.AddHours(4)),
                }),
        };

        var export = Export(items);
        var csv = DecisionTimelineExportProjection.ToCsv(export);
        var row = Assert.Single(export.Rows);

        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.NotMeasured, row.CurrentOutcomeStatus);
        Assert.Contains(row.Events, item => item.EventType == "action_executed");
        Assert.Contains(row.Events, item => item.EventType == "outcome_not_measured");
        Assert.DoesNotContain(row.Events, item => item.EventType == "outcome_measured");
        Assert.Null(row.OutcomeMeasuredAtUtc);
        Assert.Contains(row.Gaps, gap => gap.GapReason == "no_measurement_evidence");
        Assert.Equal(1, export.Funnel!.IssuedCount);
        Assert.Equal(1, export.Funnel.AcceptedCount);
        Assert.Equal(1, export.Funnel.ExecutedCount);
        Assert.Equal(0, export.Funnel.MeasuredCount);
        Assert.Equal(1, export.Funnel.NotMeasuredCount);
        Assert.Equal(0, export.Funnel.SuccessCount);
        Assert.Equal(0, export.Funnel.NegativeCount);
        Assert.Null(export.Funnel.SuccessRate);
        Assert.Equal(0m, export.Funnel.MeasuredRate);
        Assert.Contains("outcome_not_measured", csv);
        Assert.DoesNotContain("outcome_measured|", csv);
        Assert.DoesNotContain("successRate=0", csv);
        Assert.DoesNotContain("0%", csv);
        AssertParityWithSlice2(items, export);
    }

    [Fact]
    public void Export_DelayedOutcome_StaysPending_AndDoesNotInventMeasuredEvent()
    {
        var issuedAt = new DateTime(2026, 8, 5, 8, 0, 0, DateTimeKind.Utc);
        var resolvedAt = issuedAt.AddDays(3);
        var items = new[]
        {
            CreateAction(
                id: 31,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: issuedAt,
                recommendationType: "REPLENISH",
                status: AnalyticsActionConstants.Statuses.Done,
                outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Pending,
                resolvedAtUtc: resolvedAt,
                updatedAtUtc: resolvedAt,
                notes: new[]
                {
                    Note(1, 31, AnalyticsActionConstants.Statuses.New, AnalyticsActionConstants.Statuses.Accepted, issuedAt.AddHours(1)),
                    Note(2, 31, AnalyticsActionConstants.Statuses.Accepted, AnalyticsActionConstants.Statuses.Done, issuedAt.AddHours(5)),
                }),
        };

        var export = Export(items);
        var row = Assert.Single(export.Rows);

        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Pending, row.CurrentOutcomeStatus);
        Assert.Contains(row.Events, item => item.EventType == "action_executed");
        Assert.DoesNotContain(row.Events, item => item.EventType == "outcome_measured");
        Assert.DoesNotContain(row.Events, item => item.EventType == "outcome_not_measured");
        Assert.DoesNotContain(row.Events, item => item.EventType == "outcome_measurement_started");
        Assert.Null(row.OutcomeMeasuredAtUtc);
        Assert.Contains(row.Gaps, gap => gap.GapReason == "no_measurement_evidence");
        Assert.Equal(0, export.Funnel!.MeasuredCount);
        Assert.Equal(0, export.Funnel.NotMeasuredCount);
        Assert.Equal(0, export.Funnel.SuccessCount);
        Assert.Null(export.Funnel.SuccessRate);
        AssertParityWithSlice2(items, export);
    }

    [Fact]
    public void Export_MissingMeasurementEvidence_DoesNotCountAttemptedSuccess()
    {
        var issuedAt = new DateTime(2026, 8, 5, 8, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            CreateAction(
                id: 41,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: issuedAt,
                recommendationType: "REPLENISH",
                status: AnalyticsActionConstants.Statuses.Done,
                outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
                notes: new[]
                {
                    Note(1, 41, AnalyticsActionConstants.Statuses.New, AnalyticsActionConstants.Statuses.Accepted, issuedAt.AddHours(1)),
                    Note(2, 41, AnalyticsActionConstants.Statuses.Accepted, AnalyticsActionConstants.Statuses.Done, issuedAt.AddHours(2)),
                }),
        };

        var export = Export(items);
        var csv = DecisionTimelineExportProjection.ToCsv(export);
        var row = Assert.Single(export.Rows);

        Assert.DoesNotContain(row.Events, item => item.EventType == "outcome_measured");
        Assert.Null(row.OutcomeMeasuredAtUtc);
        Assert.Contains(row.Gaps, gap => gap.GapReason == "no_measurement_evidence");
        Assert.Equal(0, export.Funnel!.MeasuredCount);
        Assert.Equal(0, export.Funnel.SuccessCount);
        Assert.Null(export.Funnel.SuccessRate);
        Assert.Contains("no_measurement_evidence", csv);
        Assert.DoesNotContain("successRate=0", csv);
        Assert.DoesNotContain("0%", csv);
        AssertParityWithSlice2(items, export);
    }

    [Fact]
    public void Export_FullLifecycle_KeepsStageTimestampsAndValidSuccessRate()
    {
        var issuedAt = new DateTime(2026, 8, 5, 8, 0, 0, DateTimeKind.Utc);
        var acceptedAt = issuedAt.AddHours(2);
        var executedAt = issuedAt.AddHours(6);
        var measuredAt = issuedAt.AddDays(10);
        var resolvedAt = executedAt;
        var items = new[]
        {
            CreateAction(
                id: 51,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: issuedAt,
                recommendationType: "REPLENISH",
                status: AnalyticsActionConstants.Statuses.Done,
                outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
                outcomeMeasuredAtUtc: measuredAt,
                resolvedAtUtc: resolvedAt,
                updatedAtUtc: measuredAt,
                notes: new[]
                {
                    Note(1, 51, AnalyticsActionConstants.Statuses.New, AnalyticsActionConstants.Statuses.Accepted, acceptedAt),
                    Note(2, 51, AnalyticsActionConstants.Statuses.Accepted, AnalyticsActionConstants.Statuses.Done, executedAt),
                }),
        };

        var export = Export(items);
        var row = Assert.Single(export.Rows);

        Assert.Equal(issuedAt, row.IssuedAtUtc);
        Assert.Equal(acceptedAt, row.AcceptedAtUtc);
        Assert.Equal(executedAt, row.ExecutedAtUtc);
        Assert.Equal(measuredAt, row.OutcomeMeasuredAtUtc);
        Assert.NotEqual(row.OutcomeMeasuredAtUtc, row.IssuedAtUtc);
        Assert.Contains(row.Events, item => item.EventType == "recommendation_issued");
        Assert.Contains(row.Events, item => item.EventType == "action_accepted");
        Assert.Contains(row.Events, item => item.EventType == "action_executed");
        Assert.Contains(row.Events, item => item.EventType == "outcome_measured");
        Assert.Equal(1, export.Funnel!.IssuedCount);
        Assert.Equal(1, export.Funnel.AcceptedCount);
        Assert.Equal(1, export.Funnel.ExecutedCount);
        Assert.Equal(1, export.Funnel.MeasuredCount);
        Assert.Equal(1, export.Funnel.SuccessCount);
        Assert.Equal(1m, export.Funnel.SuccessRate);
        Assert.Equal(export.Header.RequestedPeriodFromUtc, export.Header.EffectivePeriodFromUtc);
        AssertParityWithSlice2(items, export);
    }

    private static DecisionTimelineExportDto Export(IReadOnlyList<AnalyticsActionItem> items)
    {
        var filtered = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:101",
                ProductId: 101,
                RecommendationType: "REPLENISH",
                PeriodFromUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodToUtc: new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)));
        return DecisionTimelineExportProjection.FromFilter(filtered, GeneratedAtUtc);
    }

    private static void AssertParityWithSlice2(
        IReadOnlyList<AnalyticsActionItem> items,
        DecisionTimelineExportDto export)
    {
        var filtered = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:101",
                ProductId: 101,
                RecommendationType: "REPLENISH",
                PeriodFromUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodToUtc: new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(filtered.MatchedActionCount, export.Header.MatchedActionCount);
        Assert.Equal(filtered.MatchedEventCount, export.Header.MatchedEventCount);
        Assert.Equal(filtered.EmptyReason, export.Header.EmptyReason);
        Assert.Equal(filtered.Timelines.Count, export.Rows.Count);
        foreach (var timeline in filtered.Timelines)
        {
            var row = Assert.Single(export.Rows, item => item.ActionId == timeline.ActionId);
            Assert.Equal(timeline.Events.Select(item => item.EventType), row.Events.Select(item => item.EventType));
            Assert.Equal(timeline.Gaps.Select(item => item.GapReason), row.Gaps.Select(item => item.GapReason));
            Assert.Equal(timeline.CurrentStatus, row.CurrentStatus);
            Assert.Equal(timeline.CurrentOutcomeStatus, row.CurrentOutcomeStatus);
        }
    }

    private static AnalyticsActionNote Note(long id, long actionId, string statusFrom, string statusTo, DateTime createdAtUtc)
        => new()
        {
            Id = id,
            ActionItemId = actionId,
            StatusFrom = statusFrom,
            StatusTo = statusTo,
            CreatedAtUtc = createdAtUtc,
        };

    private static AnalyticsActionItem CreateAction(
        long id,
        string sourceKey,
        string recommendationStatus,
        DateTime createdAtUtc,
        string? recommendationType = null,
        string? status = null,
        string? outcomeStatus = null,
        DateTime? outcomeMeasuredAtUtc = null,
        DateTime? resolvedAtUtc = null,
        DateTime? updatedAtUtc = null,
        IReadOnlyList<AnalyticsActionNote>? notes = null)
    {
        string? metadata = null;
        if (recommendationType is not null)
        {
            metadata = string.Concat(
                "{\"schemaVersion\":1,\"ledger\":{\"creationSnapshot\":{",
                "\"sourceRecommendationId\":\"", sourceKey, ":", recommendationType, "\",",
                "\"recommendationType\":\"", recommendationType, "\",",
                "\"expectedImpactBasis\":null,\"impactWindowDays\":14,\"confidenceLevel\":\"medium\",",
                "\"warningCodes\":[],\"primaryDrivers\":[],\"decisionReason\":\"test\",\"recommendedAction\":\"test\",",
                "\"generatedAtUtc\":\"", createdAtUtc.ToString("O"), "\",\"inputFreshnessStatus\":\"fresh\"}}}");
        }

        return new AnalyticsActionItem
        {
            Id = id,
            SourceType = AnalyticsActionConstants.SourceTypes.Product,
            SourceKey = sourceKey,
            SourceId = 101,
            Title = "DT08 test",
            Priority = AnalyticsActionConstants.Priorities.P2,
            Status = status ?? AnalyticsActionConstants.Statuses.New,
            RecommendationStatus = recommendationStatus,
            OutcomeStatus = outcomeStatus ?? AnalyticsActionConstants.OutcomeStatuses.Pending,
            OutcomeMeasuredAtUtc = outcomeMeasuredAtUtc,
            ResolvedAtUtc = resolvedAtUtc,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc ?? createdAtUtc,
            MetadataJson = metadata,
            Notes = notes?.ToList() ?? new List<AnalyticsActionNote>()
        };
    }
}
