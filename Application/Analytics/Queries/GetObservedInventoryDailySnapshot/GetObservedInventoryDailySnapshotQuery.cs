using Application.Analytics.Inventory;
using MediatR;

namespace Application.Analytics.Queries.GetObservedInventoryDailySnapshot;

public sealed record GetObservedInventoryDailySnapshotQuery(
    int? ArticleId = null,
    int? StoreId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Top = 200
) : IRequest<ObservedInventoryDailySnapshotListDto>;

public sealed record ObservedInventoryDailySnapshotDto(
    int ArticleId,
    int StoreId,
    DateTime Date,
    decimal? ObservedQty,
    decimal? ReconstructedQty,
    decimal? StockQty,
    string Provenance,
    DateTime? CapturedAtUtc,
    string? SourceSystem
);

public sealed record ObservedInventoryDailySnapshotListDto(
    DateTime GeneratedAtUtc,
    int TotalCount,
    int ReturnedCount,
    int TotalMatchingCount,
    bool IsTruncated,
    bool SnapshotAvailable,
    string? Warning,
    IReadOnlyList<ObservedInventoryDailySnapshotDto> Items
);

public static class ObservedInventoryDailySnapshotMapper
{
    public static ObservedInventoryDailySnapshotDto Map(
        int articleId,
        int storeId,
        DateTime date,
        decimal? observedQty,
        decimal? reconstructedQty,
        decimal? stockQty,
        string? provenance,
        DateTime? capturedAtUtc,
        string? sourceSystem)
    {
        var classified = InventoryDailyStockProvenance.Classify(observedQty, reconstructedQty);
        var resolvedProvenance = string.IsNullOrWhiteSpace(provenance) ? classified : provenance.Trim();

        return new ObservedInventoryDailySnapshotDto(
            ArticleId: articleId,
            StoreId: storeId,
            Date: date,
            ObservedQty: observedQty,
            ReconstructedQty: reconstructedQty,
            StockQty: stockQty ?? InventoryDailyStockProvenance.AuthoritativeQuantity(observedQty, reconstructedQty),
            Provenance: resolvedProvenance,
            CapturedAtUtc: capturedAtUtc,
            SourceSystem: sourceSystem);
    }
}
