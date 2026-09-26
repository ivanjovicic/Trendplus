namespace Application.Analytics;

/// <summary>
/// Shared receipt identity for Daily Sales and Access import diagnostics.
/// Journal sale <c>Iznos</c> is stored signed (Access import copies the source amount unchanged);
/// reconcile against signed <c>ProdajaStavke</c> line totals — never absolute-value the journal side.
/// </summary>
public static class ReceiptIdentityKeys
{
    public readonly record struct Key(DateTime SaleDateUtc, string ReceiptNumberNormalized, int? StoreId);

    public static Key? TryBuild(DateTime saleDate, string? receiptNumber, int? storeId)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            return null;
        }

        return new Key(
            DateTime.SpecifyKind(saleDate.Date, DateTimeKind.Utc),
            receiptNumber.Trim().ToUpperInvariant(),
            storeId);
    }

    /// <summary>Proven Access/sale journal convention: keep the stored sign.</summary>
    public static decimal NormalizeJournalSaleAmount(decimal iznos) => iznos;
}
