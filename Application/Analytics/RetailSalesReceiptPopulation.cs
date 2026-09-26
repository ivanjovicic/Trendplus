namespace Application.Analytics;

/// <summary>
/// Canonical retail-sales receipt population (RQ456 / SST-ACCURACY-1.0).
/// Shared SQL fragment for independent oracles; EF predicates live in Api.Services.SalesReceiptPopulationPolicy.
/// </summary>
public static class RetailSalesReceiptPopulation
{
    public const string DebtReceiptNumber = "DUG";
    public const string CorrectionReceiptNumber = "KOREKCIJA";

    public static readonly string[] ExcludedCanonicalReceiptNumbers =
    [
        DebtReceiptNumber,
        CorrectionReceiptNumber
    ];

    public const string SqlExclusionPredicate =
        "UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')";

    public static string NormalizeReceiptNumber(string? brojRacuna)
        => (brojRacuna ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsExcludedFromRetailSales(string? brojRacuna)
    {
        var normalized = NormalizeReceiptNumber(brojRacuna);
        return normalized.Length > 0
               && ExcludedCanonicalReceiptNumbers.Contains(normalized, StringComparer.Ordinal);
    }
}
