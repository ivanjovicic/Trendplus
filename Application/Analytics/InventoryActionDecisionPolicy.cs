namespace Application.Analytics;

public static class InventoryActionDecisionPolicy
{
    public sealed record SignalWindow(DateTime FromUtc, DateTime ToExclusiveUtc, DateTime AsOfUtc, string Basis);

    public static SignalWindow ResolveSignalWindow(DateTime nowUtc, DateTime? observedHorizonUtc, int days = 30)
    {
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var horizon = observedHorizonUtc.HasValue
            ? DateTime.SpecifyKind(observedHorizonUtc.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var observedEnd = horizon?.Date.AddDays(1);
        var end = observedEnd.HasValue && observedEnd.Value < now
            ? observedEnd.Value
            : now;
        return new SignalWindow(end.AddDays(-Math.Max(days, 1)), end, end.AddTicks(-1),
            observedEnd.HasValue && observedEnd.Value < now ? "source_horizon" : "current_source_or_now");
    }

    public static int? ResolveReceiptAgeDays(DateTime? lastReceiptAtUtc, DateTime asOfUtc)
        => lastReceiptAtUtc.HasValue
            ? Math.Max((asOfUtc.Date - DateTime.SpecifyKind(lastReceiptAtUtc.Value, DateTimeKind.Utc).Date).Days, 0)
            : null;

    public static bool IsSlowStockActionEligible(int quantity, int minimum, int? receiptAgeDays, int soldUnits, int windowDays = 30)
        => receiptAgeDays is >= 60 and < 90
           && quantity >= Math.Max(minimum * 2, 8)
           && HasSlowStockCover(quantity, soldUnits, windowDays);

    public static bool IsClearanceEligible(int quantity, int minimum, int? receiptAgeDays, int soldUnits, int windowDays = 30)
        => receiptAgeDays is >= 90
           && quantity >= Math.Max(minimum, 3)
           && HasSlowStockCover(quantity, soldUnits, windowDays);

    public static bool HasSlowStockCover(int quantity, int soldUnits, int windowDays = 30)
        => quantity > 0
           && soldUnits >= 0
           && (soldUnits == 0 || quantity / (soldUnits / (decimal)Math.Max(windowDays, 1)) > 60m);

    public static bool HasMateriallyStrongerDemand(int sourceUnits, int destinationUnits)
        => destinationUnits >= sourceUnits + Math.Max(1, (int)Math.Ceiling(sourceUnits * 0.20m));

    public static int SafeSourceMinimum(int sourceMinimum, int sourceDemandUnits, int windowDays = 30)
    {
        var weeklyDemandCover = (int)Math.Ceiling(sourceDemandUnits / (decimal)Math.Max(windowDays, 1) * 7m);
        return Math.Max(Math.Max(sourceMinimum, 1), weeklyDemandCover);
    }

    public static bool CanTransfer(
        int? sourceStoreId,
        string? sourceStoreName,
        int? destinationStoreId,
        string? destinationStoreName,
        int sourceDemandUnits,
        int destinationDemandUnits,
        int sourceQuantity,
        int transferQuantity,
        int safeSourceMinimum)
        => sourceStoreId.HasValue
           && destinationStoreId.HasValue
           && sourceStoreId != destinationStoreId
           && !string.IsNullOrWhiteSpace(sourceStoreName)
           && !string.IsNullOrWhiteSpace(destinationStoreName)
           && HasMateriallyStrongerDemand(sourceDemandUnits, destinationDemandUnits)
           && transferQuantity > 0
           && sourceQuantity - transferQuantity >= safeSourceMinimum;
}
