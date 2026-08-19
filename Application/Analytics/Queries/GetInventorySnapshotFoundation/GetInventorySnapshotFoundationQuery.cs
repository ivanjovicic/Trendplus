using MediatR;

namespace Application.Analytics.Queries.GetInventorySnapshotFoundation;

public sealed record GetInventorySnapshotFoundationQuery(
    DateTime? SnapshotDate = null,
    int? ArticleId = null,
    int Top = 200
) : IRequest<InventorySnapshotFoundationListDto>;

public sealed record InventorySnapshotFoundationItem(
    int ArticleId,
    string Sku,
    string ProductName,
    DateTime SnapshotDate,
    DateTime? ObservedAtUtc,
    decimal? ObservedStockQty,
    decimal? ReconstructedStockQty,
    decimal? StockQty,
    string SnapshotSourceStatus,
    bool HasMixedEvidence,
    int SourceRecords
);

public sealed record InventorySnapshotFoundationListDto(
    DateTime GeneratedAtUtc,
    DateTime? AsOfDate,
    int TotalCount,
    int ReturnedCount,
    int TotalMatchingCount,
    bool IsTruncated,
    bool SnapshotAvailable,
    string? Warning,
    IReadOnlyList<InventorySnapshotFoundationItem> Items
);
