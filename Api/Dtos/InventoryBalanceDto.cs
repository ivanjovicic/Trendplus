namespace Trendplus2.Dtos;

public sealed record InventoryBalanceDto(
    int TotalSku,
    int TotalOnHand,
    int LowStockCount,
    int OutOfStockCount,
    decimal EstimatedInventoryValue,
    AnalyticsResponseMetaDto? Meta = null
);
