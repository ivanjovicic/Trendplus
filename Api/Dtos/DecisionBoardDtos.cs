namespace Trendplus2.Dtos;

public sealed record DecisionBoardAggregateResponseDto(
    DateTime GeneratedAtUtc,
    DateTime? PeriodFromUtc,
    DateTime? PeriodToUtc,
    DateTime? LastRefreshAtUtc,
    string OverallDataQualityStatus,
    string RecommendationNote,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<DecisionBoardMetricDto> Metrics,
    IReadOnlyList<DecisionBoardSourceStateDto> SourceStates,
    IReadOnlyList<DecisionBoardSectionDto> Sections,
    AnalyticsResponseMetaDto? Meta = null);

public sealed record DecisionBoardMetricDto(
    string Label,
    string Value,
    string Tone,
    string? Note = null);

public sealed record DecisionBoardSourceStateDto(
    string SourceKey,
    string DisplayName,
    string Status,
    DateTime? GeneratedAtUtc,
    IReadOnlyList<string> WarningCodes,
    string? Message = null,
    string? SourceLink = null);

public sealed record DecisionBoardSectionDto(
    string Key,
    string Title,
    string Description,
    string SourceLink,
    string EmptyMessage,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<DecisionBoardCardDto> Cards);

public sealed record DecisionBoardCardDto(
    string Id,
    string Kind,
    string SectionKey,
    string SourceModule,
    string? SourceType,
    string? SourceKey,
    string Title,
    string? Summary,
    string ConfidenceLevel,
    decimal? ConfidenceScore,
    decimal? ReliabilityPct,
    decimal? ExpectedImpactRsd,
    decimal? MeasuredImpactRsd,
    decimal? RealizationRatio,
    string RiskIfIgnored,
    string RecommendedNextAction,
    string ActionHref,
    bool AlreadyInAction,
    bool AlreadyClosed,
    IReadOnlyList<string> WarningCodes,
    string DataQualityStatus,
    DateTime? GeneratedAtUtc,
    decimal PriorityScore,
    decimal ImpactScore,
    string? ConfidenceSource = null,
    IReadOnlyList<string>? ReasonCodes = null,
    bool? RecommendationAllowed = null);
