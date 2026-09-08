using MediatR;

namespace Application.Analytics.Queries.GetInventoryStatus
{
    public record GetInventoryStatusQuery(
        int LowStockThreshold = 2,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        int? StoreId = null,
        int? SupplierId = null,
        string? DataScope = null) : IRequest<InventoryStatusDto>;

    public record InventoryStatusDto(
        int TotalSkuCount,
        int TotalOnHand,
        int LowStockCount,
        int OutOfStockCount,
        bool UsedOperationalFallback = false
    );
}
