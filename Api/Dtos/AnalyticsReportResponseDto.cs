namespace Trendplus2.Dtos;

public sealed record AnalyticsReportResponseDto(
    string ReportId,
    string StableQueryUrl,
    string Title,
    string Type,
    DateTime GeneratedAtUtc,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    AnalyticsReportPeriodDto Period,
    DateTime? LastRefreshAtUtc,
    string? DataFreshnessStatus,
    string DataQualityStatus,
    bool RecommendationAllowed,
    bool UsedFallback,
    string? FallbackReason,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<AnalyticsReportKpiDto> Kpis,
    IReadOnlyList<AnalyticsReportSectionDto> Sections,
    IReadOnlyList<AnalyticsReportActionDto> RecommendedActions,
    AnalyticsReportMethodologyDto Methodology,
    IReadOnlyList<AnalyticsLegacyReportRowDto> Rows,
    AnalyticsResolvedReportPayloadDto Payload,
    string? ReportTitle = null,
    string? ReportType = null,
    string? MethodologySummary = null,
    AnalyticsResponseMetaDto? Meta = null);

public sealed record AnalyticsReportPeriodDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string Label,
    string? RequestedDataset = null,
    string? EffectiveDataset = null,
    string? EffectivePeriodLabel = null,
    string? Scope = null,
    DateTime? RequestedFromUtc = null,
    DateTime? RequestedToUtc = null,
    DateTime? EffectiveFromUtc = null,
    DateTime? EffectiveToUtc = null,
    DateTime? ObservedFromUtc = null,
    DateTime? ObservedToUtc = null);

public sealed record AnalyticsReportKpiDto(
    string Key,
    string Label,
    object? Value,
    string? Unit = null,
    string? Tone = null,
    string? Note = null,
    string? ValueStatus = null,
    string? ValueReason = null);

public sealed record AnalyticsReportSectionDto(
    string Key,
    string Title,
    string? Description,
    IReadOnlyList<AnalyticsReportColumnDto> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    int RowCount,
    string? EmptyMessage = null);

public sealed record AnalyticsReportColumnDto(
    string Key,
    string Label,
    string DataType = "text");

public sealed record AnalyticsReportActionDto(
    string Title,
    string Description,
    string Href,
    string Priority = "medium");

public sealed record AnalyticsReportMethodologyDto(
    string Summary,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string>? SourceHints = null);

public sealed record AnalyticsLegacyReportRowDto(
    string Section,
    string Item,
    string Value,
    string? Secondary = null,
    string? Note = null);

public sealed record AnalyticsReportPayloadColumnDto(
    string Key,
    string Header,
    string DataType = "text");

public sealed record AnalyticsReportPayloadRowDto(
    string Section,
    string Item,
    string Value,
    string? Secondary = null,
    string? Note = null);

public sealed record AnalyticsReportNamedValueDto(
    string Key,
    string Label,
    string Value);

public sealed record AnalyticsResolvedReportPayloadDto(
    string TableKey,
    string TableTitle,
    IReadOnlyList<AnalyticsReportPayloadColumnDto> Columns,
    IReadOnlyList<AnalyticsReportPayloadRowDto> Rows,
    IReadOnlyList<AnalyticsReportNamedValueDto> Filters,
    IReadOnlyList<AnalyticsReportNamedValueDto> Metadata,
    string Locale,
    string DocumentType,
    string TemplateName,
    int TemplateVersion);
