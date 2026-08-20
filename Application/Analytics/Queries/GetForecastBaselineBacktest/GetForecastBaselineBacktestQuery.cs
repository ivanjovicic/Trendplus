using MediatR;

namespace Application.Analytics.Queries.GetForecastBaselineBacktest;

public sealed record GetForecastBaselineBacktestQuery(
    int? StoreId = null,
    int? SupplierId = null,
    int HorizonDays = 14
) : IRequest<ForecastBaselineBacktestDto>;

/// <param name="EvaluationStatus">unavailable | partial | ready</param>
/// <param name="IsAuthoritativeMeasurement">True only when measured aggregates are trusted.</param>
/// <param name="Aggregates">Null when unavailable — never invent 0.0 quality scores.</param>
public sealed record ForecastBaselineBacktestDto(
    DateTime GeneratedAtUtc,
    string EvaluationStatus,
    bool IsAuthoritativeMeasurement,
    string? ComparisonWindowStatus,
    DateTime? WindowStartUtc,
    DateTime? WindowEndUtc,
    int HorizonDays,
    string PrimaryBaselineId,
    IReadOnlyList<string> AllowedBaselineIds,
    IReadOnlyList<string> AllowedMetricIds,
    IReadOnlyList<string> AllowedCohortIds,
    IReadOnlyList<string> MissingEvidenceReasons,
    IReadOnlyList<ForecastBaselineBacktestCohortDto> Cohorts,
    ForecastBaselineBacktestAggregatesDto? Aggregates,
    string? Warning
);

public sealed record ForecastBaselineBacktestCohortDto(
    string CohortId,
    int SkuStoreCount,
    ForecastBaselineBacktestAggregatesDto? Aggregates,
    string? Note
);

public sealed record ForecastBaselineBacktestAggregatesDto(
    decimal? Wape,
    decimal? Bias,
    decimal? Mae,
    int? SampleCount
);
