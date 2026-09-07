using MediatR;

namespace Application.Analytics.Queries.GetInventorySizeCurve;

public sealed record GetInventorySizeCurveQuery(
    int? StoreId = null,
    int? SupplierId = null,
    int? SkuId = null,
    int Top = 200
) : IRequest<InventorySizeCurveListDto>;

public sealed record InventorySizeCurveDto(
    int SkuId,
    int StoreId,
    string SizeCode,
    decimal? ActualSizeShare,
    decimal? IdealSizeShare,
    decimal? DeviationPct,
    bool? IsCoreSizeMissing,
    bool? IsDeadSize,
    bool? BrokenRun,
    decimal? CurveConfidence,
    string? EvidenceStatus,
    IReadOnlyList<string> ReasonCodes
);

public sealed record InventorySizeCurveListDto(
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
    IReadOnlyList<InventorySizeCurveDto> Items
);
