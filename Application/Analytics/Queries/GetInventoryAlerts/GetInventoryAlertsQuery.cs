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
    /// <summary>HTTP/query response generation time, not source snapshot freshness.</summary>
    DateTime GeneratedAtUtc,
    int TotalCount,
    int ReturnedCount,
    int TotalMatchingCount,
    bool IsTruncated,
    bool SnapshotAvailable,
    /// <summary>Proven source snapshot freshness; null when no lineage is available.</summary>
    DateTime? SnapshotFreshnessUtc,
    /// <summary>fresh | stale | critical | unknown.</summary>
    string SnapshotFreshnessStatus,
    string? Warning,
    IReadOnlyList<InventoryAlertDto> Items
);
