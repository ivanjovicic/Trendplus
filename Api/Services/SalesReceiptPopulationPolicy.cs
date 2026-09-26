using System.Linq.Expressions;
using Domain.Model.Prodaja;

namespace Api.Services;

/// <summary>
/// Canonical retail-sales receipt population shared by Daily, Supplier, Shoe Type and Color analytics.
/// Non-standard debt/correction documents remain in the database for audit, but are not retail turnover.
/// </summary>
public static class SalesReceiptPopulationPolicy
{
    public static readonly string[] ExcludedReceiptNumbers = ["DUG", "KOREKCIJA"];

    /// <summary>
    /// EF-translatable header predicate. Trim and upper are intentional so imported casing/spacing cannot
    /// make a certified surface disagree with Daily Sales.
    /// </summary>
    public static Expression<Func<ProdajaZaglavlje, bool>> IncludedHeaderPredicate =>
        header => !ExcludedReceiptNumbers.Contains((header.BrojRacuna ?? string.Empty).Trim().ToUpper());

    public static bool IsExcluded(string? receiptNumber)
    {
        var normalized = receiptNumber?.Trim();
        return string.Equals(normalized, "DUG", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "KOREKCIJA", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsIncluded(string? receiptNumber) => !IsExcluded(receiptNumber);
}
