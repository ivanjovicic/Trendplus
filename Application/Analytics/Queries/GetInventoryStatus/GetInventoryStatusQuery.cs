using MediatR;

namespace Application.Analytics.Queries.GetInventoryStatus
{
    public record GetInventoryStatusQuery(int LowStockThreshold = 2) : IRequest<InventoryStatusDto>;

    public record InventoryStatusDto(
        int TotalSkuCount,
        int TotalOnHand,
        int LowStockCount,
        int OutOfStockCount,
        bool UsedOperationalFallback = false
    );
}
