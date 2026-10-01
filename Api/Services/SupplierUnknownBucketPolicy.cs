namespace Api.Services;

/// <summary>
/// One unknown-supplier bucket per Supplier surface: a missing id, an id that does
/// not resolve to a named supplier, and a supplier literally named "Nepoznato"
/// all collapse into a single row so unknown revenue is not split across rows.
/// </summary>
public static class SupplierUnknownBucketPolicy
{
    public const string Policy = "single_unknown_bucket";
    public const string UnknownName = "Nepoznato";

    public static bool IsUnresolved(int? supplierId, string? supplierName) =>
        !supplierId.HasValue
        || string.IsNullOrWhiteSpace(supplierName)
        || string.Equals(supplierName.Trim(), UnknownName, StringComparison.OrdinalIgnoreCase);

    public static (int? SupplierId, string SupplierName, bool IsUnknown) Resolve(int? supplierId, string? supplierName) =>
        IsUnresolved(supplierId, supplierName)
            ? (null, UnknownName, true)
            : (supplierId, supplierName!.Trim(), false);

    /// <summary>
    /// Number of distinct source supplier ids folded into the unknown bucket;
    /// a missing id counts as one source.
    /// </summary>
    public static int CountUnresolvedSourceIds(IEnumerable<(int? SupplierId, string? SupplierName)> sources) =>
        sources
            .Where(source => IsUnresolved(source.SupplierId, source.SupplierName))
            .Select(source => source.SupplierId)
            .Distinct()
            .Count();
}
