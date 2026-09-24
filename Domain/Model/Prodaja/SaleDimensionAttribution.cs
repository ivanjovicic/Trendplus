namespace Domain.Model.Prodaja;

/// <summary>
/// Describes how supplier and shoe-type dimensions were assigned to a sale line.
/// These values are persisted so later article-master edits cannot rewrite history.
/// </summary>
public static class SaleDimensionAttribution
{
    public const string SaleSnapshot = "sale_snapshot";
    public const string ReconstructedHistory = "reconstructed_history";
    public const string FrozenCurrentMasterBackfill = "frozen_current_master_backfill";
    public const string Unknown = "unknown";

    public static bool IsAuthoritative(string? basis)
        => string.Equals(basis, SaleSnapshot, StringComparison.Ordinal)
            || string.Equals(basis, ReconstructedHistory, StringComparison.Ordinal);

    public static void CaptureSaleSnapshot(ProdajaStavka line, int? supplierId, int? shoeTypeId)
    {
        if (!string.IsNullOrWhiteSpace(line.AttributionBasis)
            && !string.Equals(line.AttributionBasis, Unknown, StringComparison.Ordinal))
        {
            return;
        }

        line.SupplierIdAtSale = supplierId;
        line.ShoeTypeIdAtSale = shoeTypeId;
        line.AttributionBasis = SaleSnapshot;
    }

    public static void FreezeCurrentMasterBackfill(ProdajaStavka line, int? supplierId, int? shoeTypeId)
    {
        if (!string.IsNullOrWhiteSpace(line.AttributionBasis)
            && !string.Equals(line.AttributionBasis, Unknown, StringComparison.Ordinal))
        {
            return;
        }

        line.SupplierIdAtSale = supplierId;
        line.ShoeTypeIdAtSale = shoeTypeId;
        line.AttributionBasis = FrozenCurrentMasterBackfill;
    }
}
