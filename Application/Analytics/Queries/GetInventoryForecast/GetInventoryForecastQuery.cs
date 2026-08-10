using MediatR;

namespace Application.Analytics.Queries.GetInventoryForecast;

public sealed record GetInventoryForecastQuery(
    int? StoreId = null,
    int? SupplierId = null,
    int? SkuId = null,
    string? SizeCode = null,
    int Top = 200
) : IRequest<InventoryForecastListDto>;

public sealed record InventoryForecastDto(
    int SkuId,
    int StoreId,
    string SizeCode,
    decimal? Forecast7d,
    decimal? Forecast14d,
    decimal? Forecast28d,
    decimal? ProbabilityOfOOSIn7d,
    decimal? OverstockRisk,
    decimal? ConfidenceScore,
    string Explanation
);

public sealed record InventoryForecastListDto(
    DateTime GeneratedAtUtc,
    int TotalCount,
    int ReturnedCount,
    int TotalMatchingCount,
    bool IsTruncated,
    bool SnapshotAvailable,
    string? Warning,
    IReadOnlyList<InventoryForecastDto> Items
);
