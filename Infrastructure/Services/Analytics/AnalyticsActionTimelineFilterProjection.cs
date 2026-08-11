using Domain.Model.Analytics;

namespace Infrastructure.Services.Analytics;

/// <summary>
/// DT05 read-only filtered timeline slice over Slice-1 projections.
/// Filters by entity, recommendation family and period without inventing history.
/// </summary>
public static class AnalyticsActionTimelineFilterProjection
{
    public const string EmptyReasonNoEvents = "no_events";
    public const string EmptyReasonOutsidePeriod = "outside_period";
    public const string EmptyReasonNoMeasurement = "no_measurement";

    public static DecisionTimelineFilterResponseDto Filter(
        IEnumerable<AnalyticsActionItem> items,
        DecisionTimelineFilterQuery query)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);

        var periodFromUtc = NormalizeDateUtc(query.PeriodFromUtc);
        var periodToUtc = NormalizeDateUtc(query.PeriodToUtc);
        if (periodFromUtc > periodToUtc)
        {
            (periodFromUtc, periodToUtc) = (periodToUtc, periodFromUtc);
        }

        var sourceType = NormalizeOptional(query.SourceType);
        var sourceKey = NormalizeOptional(query.SourceKey);
        var recommendationType = NormalizeOptional(query.RecommendationType);
        var productId = query.ProductId;

        var scope = new DecisionTimelineFilterScopeDto(
            SourceType: sourceType,
            SourceKey: sourceKey,
            ProductId: productId,
            RecommendationType: recommendationType,
            PeriodFromUtc: periodFromUtc,
            PeriodToUtc: periodToUtc,
            ScopeExplanation: BuildScopeExplanation(sourceType, sourceKey, productId, recommendationType, periodFromUtc, periodToUtc));

        var entityFamilyMatches = new List<AnalyticsActionTimelineProjectionDto>();
        foreach (var item in items)
        {
            if (!MatchesEntity(item, sourceType, sourceKey, productId))
            {
                continue;
            }

            var projection = AnalyticsActionTimelineProjection.Project(item);
            if (!MatchesRecommendationFamily(item, projection, recommendationType))
            {
                continue;
            }

            entityFamilyMatches.Add(projection);
        }

        if (entityFamilyMatches.Count == 0)
        {
            return new DecisionTimelineFilterResponseDto(
                Scope: scope,
                EmptyReason: EmptyReasonNoEvents,
                Timelines: Array.Empty<DecisionTimelineItemDto>(),
                MatchedActionCount: 0,
                MatchedEventCount: 0,
                WarningCodes: Array.Empty<string>());
        }

        var inPeriod = entityFamilyMatches
            .Where(projection => IsWithinPeriod(projection.IssuedAtUtc, periodFromUtc, periodToUtc))
            .OrderBy(projection => projection.IssuedAtUtc)
            .ThenBy(projection => projection.ActionId)
            .Select(ToTimelineItem)
            .ToArray();

        if (inPeriod.Length == 0)
        {
            return new DecisionTimelineFilterResponseDto(
                Scope: scope,
                EmptyReason: EmptyReasonOutsidePeriod,
                Timelines: Array.Empty<DecisionTimelineItemDto>(),
                MatchedActionCount: 0,
                MatchedEventCount: 0,
                WarningCodes: ["filtered_outside_period"]);
        }

        var matchedEventCount = inPeriod.Sum(item => item.Events.Count);
        var hasAnyMeasuredEvent = inPeriod.Any(item =>
            item.Events.Any(eventItem => string.Equals(eventItem.EventType, "outcome_measured", StringComparison.OrdinalIgnoreCase)));

        // Collection can still return timelines with explicit gaps; no_measurement is advisory only when none measured.
        string? emptyReason = null;
        var warningCodes = new List<string>();
        if (!hasAnyMeasuredEvent)
        {
            warningCodes.Add(EmptyReasonNoMeasurement);
        }

        return new DecisionTimelineFilterResponseDto(
            Scope: scope,
            EmptyReason: emptyReason,
            Timelines: inPeriod,
            MatchedActionCount: inPeriod.Length,
            MatchedEventCount: matchedEventCount,
            WarningCodes: warningCodes);
    }

    private static DecisionTimelineItemDto ToTimelineItem(AnalyticsActionTimelineProjectionDto projection)
        => new(
            TimelineId: $"{projection.ActionId}:{projection.SourceRecommendationId}",
            ActionId: projection.ActionId,
            SourceRecommendationId: projection.SourceRecommendationId,
            CorrelationId: projection.CorrelationId,
            SourceType: projection.SourceType,
            SourceKey: projection.SourceKey,
            RecommendationType: projection.RecommendationType,
            ProjectionState: projection.ProjectionState,
            IssuedAtUtc: projection.IssuedAtUtc,
            CurrentStatus: projection.CurrentStatus,
            CurrentOutcomeStatus: projection.CurrentOutcomeStatus,
            Events: projection.Events,
            Gaps: projection.Gaps,
            LedgerSnapshot: projection.LedgerSnapshot);

    private static bool MatchesEntity(
        AnalyticsActionItem item,
        string? sourceType,
        string? sourceKey,
        int? productId)
    {
        if (!string.IsNullOrWhiteSpace(sourceType)
            && !string.Equals(item.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sourceKey)
            && !string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!productId.HasValue)
        {
            return true;
        }

        if (item.SourceId == productId.Value)
        {
            return true;
        }

        var expectedKey = $"product:{productId.Value}";
        if (string.Equals(item.SourceKey, expectedKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(sourceKey)
            && item.SourceKey.StartsWith(expectedKey + ":", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesRecommendationFamily(
        AnalyticsActionItem item,
        AnalyticsActionTimelineProjectionDto projection,
        string? recommendationType)
    {
        if (string.IsNullOrWhiteSpace(recommendationType))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(projection.RecommendationType)
            && string.Equals(projection.RecommendationType, recommendationType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(item.RecommendationStatus)
            && string.Equals(item.RecommendationStatus, recommendationType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWithinPeriod(DateTime issuedAtUtc, DateTime periodFromUtc, DateTime periodToUtc)
    {
        var issuedDate = NormalizeDateUtc(issuedAtUtc);
        return issuedDate >= periodFromUtc && issuedDate <= periodToUtc;
    }

    private static string BuildScopeExplanation(
        string? sourceType,
        string? sourceKey,
        int? productId,
        string? recommendationType,
        DateTime periodFromUtc,
        DateTime periodToUtc)
    {
        var entity = !string.IsNullOrWhiteSpace(sourceKey)
            ? sourceKey
            : productId.HasValue
                ? $"product:{productId.Value}"
                : sourceType ?? "svi entiteti";
        var family = string.IsNullOrWhiteSpace(recommendationType) ? "sve porodice" : recommendationType;
        return $"Entitet: {entity} · Porodica: {family} · Period: {periodFromUtc:yyyy-MM-dd} – {periodToUtc:yyyy-MM-dd}";
    }

    private static DateTime NormalizeDateUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.Date;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record DecisionTimelineFilterQuery(
    string? SourceType,
    string? SourceKey,
    int? ProductId,
    string? RecommendationType,
    DateTime PeriodFromUtc,
    DateTime PeriodToUtc);

public sealed record DecisionTimelineFilterScopeDto(
    string? SourceType,
    string? SourceKey,
    int? ProductId,
    string? RecommendationType,
    DateTime PeriodFromUtc,
    DateTime PeriodToUtc,
    string ScopeExplanation);

public sealed record DecisionTimelineItemDto(
    string TimelineId,
    long ActionId,
    string SourceRecommendationId,
    string CorrelationId,
    string SourceType,
    string SourceKey,
    string? RecommendationType,
    string ProjectionState,
    DateTime IssuedAtUtc,
    string CurrentStatus,
    string CurrentOutcomeStatus,
    IReadOnlyList<AnalyticsActionTimelineEventDto> Events,
    IReadOnlyList<AnalyticsActionTimelineGapDto> Gaps,
    AnalyticsActionLedgerSnapshot? LedgerSnapshot);

public sealed record DecisionTimelineFilterResponseDto(
    DecisionTimelineFilterScopeDto Scope,
    string? EmptyReason,
    IReadOnlyList<DecisionTimelineItemDto> Timelines,
    int MatchedActionCount,
    int MatchedEventCount,
    IReadOnlyList<string> WarningCodes);
