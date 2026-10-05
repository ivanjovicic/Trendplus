using System.Linq.Expressions;

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

    /// <summary>
    /// Shared Inventory/Dashboard low-stock predicate: configured minimum wins,
    /// otherwise the default threshold applies.
    /// </summary>
    public static bool IsMeasuredLowStockForSurface(
        int? quantity,
        int? minimum,
        int defaultThreshold = InventoryValuationAndAgingPolicy.DefaultLowStockThreshold)
    {
        if (quantity is null or <= 0)
        {
            return false;
        }

        if (minimum is > 0)
        {
            return quantity.Value <= minimum.Value;
        }

        return quantity.Value <= defaultThreshold;
    }

    public static Expression<Func<Domain.Model.Artikli, bool>> MatchesLowStockSurface(
        int defaultThreshold = InventoryValuationAndAgingPolicy.DefaultLowStockThreshold)
        => article =>
            article.Kolicina != null
            && article.Kolicina > 0
            && (
                (article.MinimalnaKolicina != null
                 && article.MinimalnaKolicina > 0
                 && article.Kolicina <= article.MinimalnaKolicina)
                || ((article.MinimalnaKolicina == null || article.MinimalnaKolicina <= 0)
                    && article.Kolicina <= defaultThreshold));

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
