using MediatR;
using Application.Analytics.Queries;

namespace Application.Analytics.Queries.GetRebalanceSuggestions;

public sealed record GetRebalanceSuggestionsQuery(
    int? FromStoreId = null,
    int? ToStoreId = null,
    int? SupplierId = null,
    string? Urgency = null,
    int Top = 100
) : IRequest<RebalanceSuggestionListDto>;

public sealed record RebalanceSuggestionDto(
    int FromStoreId,
    int ToStoreId,
    int SkuId,
    string SizeCode,
    int? RecommendedQty,
    string? Urgency,
    decimal? Confidence,
    string? Reason,
    decimal? ExpectedSavedSales,
    decimal? ExpectedCapitalRelease,
    InventorySnapshotRowState Actionability
);

public sealed record RebalanceSuggestionListDto(
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
    IReadOnlyList<RebalanceSuggestionDto> Items
);
