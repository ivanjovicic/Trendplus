namespace Application.Analytics;

/// <summary>
/// Null quantity/minimum stay unavailable; measured zero remains distinct OOS.
/// </summary>
public static class InventoryStockEvidence
{
    public static bool IsMeasuredOutOfStock(int? quantity)
        => quantity == 0;

    public static bool IsMeasuredLowStock(int? quantity, int lowStockThreshold)
        => quantity is > 0 && quantity.Value <= lowStockThreshold;

    public static bool IsMeasuredLowStockAgainstMinimum(int? quantity, int? minimum)
        => quantity is > 0
           && minimum is not null
           && quantity.Value <= minimum.Value;

    public static int MeasuredOnHandUnits(int? quantity)
        => quantity is > 0 ? quantity.Value : 0;

    /// <summary>
    /// Estimated capital: unavailable when quantity or cost evidence is missing;
    /// measured zero quantity is true zero capital.
    /// </summary>
    public static decimal? ComputeEstimatedValue(int? quantity, decimal? unitCost)
    {
        if (quantity is null)
            return null;

        if (quantity.Value == 0)
            return 0m;

        if (unitCost is null)
            return null;

        return unitCost.Value * quantity.Value;
    }
}
