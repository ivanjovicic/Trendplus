namespace Application.Analytics;

/// <summary>
/// Canonical retail-sales receipt population (RQ456 / SST-ACCURACY-1.0).
/// <c>DUG</c> and <c>KOREKCIJA</c> (trim + case-insensitive) are non-standard debt/adjustment
/// documents and are excluded from certified retail turnover on Daily Sales, Supplier,
/// Shoe Type, Color and the independent raw-fact oracle. Signed retail returns remain in population.
/// </summary>
public static class RetailSalesReceiptPopulation
{
    public const string DebtReceiptNumber = "DUG";
    public const string CorrectionReceiptNumber = "KOREKCIJA";

    /// <summary>
    /// Canonical upper-case forms for EF <c>Contains</c> after <c>Trim().ToUpper()</c>.
    /// Capture into a local variable before composing a LINQ-to-Entities query.
    /// </summary>
    public static readonly string[] ExcludedCanonicalReceiptNumbers =
    [
        DebtReceiptNumber,
        CorrectionReceiptNumber
    ];

    /// <summary>
    /// SQL predicate fragment for raw-fact oracles. Alias the sale header as <c>pz</c>
    /// with column <c>broj_racuna</c>.
    /// </summary>
    public const string SqlExclusionPredicate =
        "UPPER(TRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')";

    public static string NormalizeReceiptNumber(string? brojRacuna)
        => (brojRacuna ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsExcludedFromRetailSales(string? brojRacuna)
    {
        var normalized = NormalizeReceiptNumber(brojRacuna);
        return normalized.Length > 0
               && ExcludedCanonicalReceiptNumbers.Contains(normalized, StringComparer.Ordinal);
    }

    public static bool IsDebtReceiptNumber(string? brojRacuna)
        => string.Equals(NormalizeReceiptNumber(brojRacuna), DebtReceiptNumber, StringComparison.Ordinal);

    public static bool IsCorrectionReceiptNumber(string? brojRacuna)
        => string.Equals(NormalizeReceiptNumber(brojRacuna), CorrectionReceiptNumber, StringComparison.Ordinal);
}
