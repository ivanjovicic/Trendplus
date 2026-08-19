namespace Application.Analytics.Inventory;

/// <summary>
/// Canonical provenance labels for SKU/store/day stock evidence.
/// Downstream analytics must not treat reconstructed proxy as observed stock.
/// </summary>
public static class InventoryDailyStockProvenance
{
    public const string Observed = "observed";
    public const string Reconstructed = "reconstructed";
    public const string Mixed = "mixed";
    public const string Missing = "missing";

    /// <summary>
    /// Classify a day. True observed zero (<paramref name="observedQty"/> = 0) is not missing.
    /// Reconstructed zero is still reconstructed, not observed empty.
    /// Both null is missing, never a fabricated 0.
    /// </summary>
    public static string Classify(decimal? observedQty, decimal? reconstructedQty)
    {
        if (observedQty is not null && reconstructedQty is not null && observedQty != reconstructedQty)
            return Mixed;

        if (observedQty is not null)
            return Observed;

        if (reconstructedQty is not null)
            return Reconstructed;

        return Missing;
    }

    public static decimal? AuthoritativeQuantity(decimal? observedQty, decimal? reconstructedQty)
        => observedQty ?? reconstructedQty;

    public static bool IsObservedAuthoritative(string provenance)
        => string.Equals(provenance, Observed, StringComparison.OrdinalIgnoreCase);
}
