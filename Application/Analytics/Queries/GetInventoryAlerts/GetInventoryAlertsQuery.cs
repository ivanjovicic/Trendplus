using MediatR;

namespace Application.Analytics.Queries.GetInventoryAlerts;

public sealed record GetInventoryAlertsQuery(
    int? StoreId = null,
    int? SupplierId = null,
    string? Severity = null,
    int Top = 100
) : IRequest<InventoryAlertListDto>;

public sealed record InventoryAlertDto(
    string AlertType,
    int SkuId,
    int StoreId,
    string? SizeCode,
    string? Severity,
    string Title,
    string Message,
    decimal? ConfidenceScore
);

public sealed record InventoryAlertListDto(
    DateTime GeneratedAtUtc,
    int TotalCount,
    int ReturnedCount,
    int TotalMatchingCount,
    bool IsTruncated,
    bool SnapshotAvailable,
    string? Warning,
    IReadOnlyList<InventoryAlertDto> Items
);
