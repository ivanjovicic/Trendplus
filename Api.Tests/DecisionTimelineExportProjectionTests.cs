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

    private static AnalyticsActionItem CreateAction(
        long id,
        string sourceKey,
        string recommendationStatus,
        DateTime createdAtUtc,
        string? recommendationType = null)
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
            Title = "DT07 test",
            Priority = AnalyticsActionConstants.Priorities.P2,
            Status = AnalyticsActionConstants.Statuses.New,
            RecommendationStatus = recommendationStatus,
            OutcomeStatus = AnalyticsActionConstants.OutcomeStatuses.Pending,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
            MetadataJson = metadata,
            Notes = Array.Empty<AnalyticsActionNote>()
        };
    }
}
