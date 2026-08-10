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
    DateTime GeneratedAtUtc,
    int TotalCount,
    int ReturnedCount,
    int TotalMatchingCount,
    bool IsTruncated,
    bool SnapshotAvailable,
    string? Warning,
    IReadOnlyList<InventorySizeCurveDto> Items
);
