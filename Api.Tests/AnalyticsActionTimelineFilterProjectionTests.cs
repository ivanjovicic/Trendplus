using Application.Analytics;
using Domain.Model.Analytics;
using Infrastructure.Services.Analytics;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class AnalyticsActionTimelineFilterProjectionTests
{
    [Fact]
    public void Filter_NoMatchingEntity_ReturnsNoEvents()
    {
        var items = new[]
        {
            CreateAction(
                id: 1,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc))
        };

        var result = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:999",
                ProductId: 999,
                RecommendationType: null,
                PeriodFromUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodToUtc: new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(AnalyticsActionTimelineFilterProjection.EmptyReasonNoEvents, result.EmptyReason);
        Assert.Empty(result.Timelines);
        Assert.Contains("product:999", result.Scope.ScopeExplanation);
        Assert.Contains("Porodica: sve porodice", result.Scope.ScopeExplanation);
    }

    [Fact]
    public void Filter_EntityFamilyOutsidePeriod_ReturnsOutsidePeriod()
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

        var result = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:101",
                ProductId: 101,
                RecommendationType: "REPLENISH",
                PeriodFromUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodToUtc: new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(AnalyticsActionTimelineFilterProjection.EmptyReasonOutsidePeriod, result.EmptyReason);
        Assert.Empty(result.Timelines);
        Assert.Contains("filtered_outside_period", result.WarningCodes);
    }

    [Fact]
    public void Filter_EntityFamilyAndPeriod_ReturnsDeterministicTimelineWithoutInventingEvents()
    {
        var items = new[]
        {
            CreateAction(
                id: 3,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc),
                recommendationType: "REPLENISH"),
            CreateAction(
                id: 4,
                sourceKey: "product:101",
                recommendationStatus: "MARKDOWN",
                createdAtUtc: new DateTime(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc),
                recommendationType: "MARKDOWN")
        };

        var result = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:101",
                ProductId: 101,
                RecommendationType: "REPLENISH",
                PeriodFromUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                PeriodToUtc: new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Null(result.EmptyReason);
        var timeline = Assert.Single(result.Timelines);
        Assert.Equal(3, timeline.ActionId);
        Assert.Equal("REPLENISH", timeline.RecommendationType);
        Assert.Contains(timeline.Events, eventItem => eventItem.EventType == "recommendation_issued");
        Assert.DoesNotContain(timeline.Events, eventItem => eventItem.EventType == "outcome_measured");
        Assert.Contains(AnalyticsActionTimelineFilterProjection.EmptyReasonNoMeasurement, result.WarningCodes);
        Assert.Contains("Porodica: Dopuni", result.Scope.ScopeExplanation);
        Assert.DoesNotContain("Porodica: REPLENISH", result.Scope.ScopeExplanation);
    }

    [Fact]
    public void Filter_ReversedPeriod_FailsClosedWithoutSwappingScope()
    {
        var periodFrom = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);
        var periodTo = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            CreateAction(
                id: 5,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc),
                recommendationType: "REPLENISH")
        };

        var result = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:101",
                ProductId: 101,
                RecommendationType: "REPLENISH",
                PeriodFromUtc: periodFrom,
                PeriodToUtc: periodTo));

        Assert.Equal(AnalyticsActionTimelineFilterProjection.EmptyReasonInvalidPeriod, result.EmptyReason);
        Assert.Empty(result.Timelines);
        Assert.Equal(0, result.MatchedActionCount);
        Assert.Equal(periodFrom.Date, result.Scope.PeriodFromUtc);
        Assert.Equal(periodTo.Date, result.Scope.PeriodToUtc);
        Assert.Contains(AnalyticsActionTimelineFilterProjection.EmptyReasonInvalidPeriod, result.WarningCodes);
        Assert.DoesNotContain("2026-08-01 – 2026-08-11", result.Scope.ScopeExplanation);
    }

    [Fact]
    public void Filter_EqualPeriod_IsValidOneDayWindow()
    {
        var day = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            CreateAction(
                id: 6,
                sourceKey: "product:101",
                recommendationStatus: "REPLENISH",
                createdAtUtc: day.AddHours(10),
                recommendationType: "REPLENISH")
        };

        var result = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: "product",
                SourceKey: "product:101",
                ProductId: 101,
                RecommendationType: "REPLENISH",
                PeriodFromUtc: day,
                PeriodToUtc: day));

        Assert.Null(result.EmptyReason);
        Assert.Single(result.Timelines);
        Assert.Equal(day, result.Scope.PeriodFromUtc);
        Assert.Equal(day, result.Scope.PeriodToUtc);
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
            Title = "DT05 test",
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
