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

/// <param name="GeneratedAtUtc">Response generation time — not proven snapshot freshness.</param>
/// <param name="ProvenanceStatus">missing_relation | owner_unknown | stale | trusted</param>
/// <param name="MaterializerOwner">Proven writer id, or null/none when unproven.</param>
/// <param name="IsAuthoritativeForecast">True only for trusted generated evidence.</param>
/// <param name="SnapshotFreshnessUtc">Materializer freshness when known; null when unproven.</param>
public sealed record InventoryForecastListDto(
    DateTime GeneratedAtUtc,
    int TotalCount,
    int ReturnedCount,
    int TotalMatchingCount,
    bool IsTruncated,
    bool SnapshotAvailable,
    string ProvenanceStatus,
    string? MaterializerOwner,
    bool IsAuthoritativeForecast,
    DateTime? SnapshotFreshnessUtc,
    string? Warning,
    IReadOnlyList<InventoryForecastDto> Items
);
