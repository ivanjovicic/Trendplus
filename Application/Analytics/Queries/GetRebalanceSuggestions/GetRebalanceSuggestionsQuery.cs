using MediatR;

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
    string Reason,
    decimal? ExpectedSavedSales,
    decimal? ExpectedCapitalRelease
);

public sealed record RebalanceSuggestionListDto(
    DateTime GeneratedAtUtc,
    int TotalCount,
    int ReturnedCount,
    int TotalMatchingCount,
    bool IsTruncated,
    bool SnapshotAvailable,
    string? Warning,
    IReadOnlyList<RebalanceSuggestionDto> Items
);
