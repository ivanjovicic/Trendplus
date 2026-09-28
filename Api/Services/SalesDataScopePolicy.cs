using System.Linq.Expressions;
using Domain.Model.Prodaja;

namespace Api.Services;

/// <summary>
/// Canonical data-scope predicate for certified sales. Scope follows the sale header
/// because article/master origin is mutable and must not reclassify historical sales.
/// </summary>
public static class SalesDataScopePolicy
{
    public const string Source = "sale_header.data_origin";

    public static string Normalize(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    /// <summary>
    /// EF-translatable sale-header predicate. Article.DataOrigin remains available only
    /// for explicitly article-scoped quality/membership metrics, never certified sales.
    /// </summary>
    public static Expression<Func<ProdajaZaglavlje, bool>> HeaderPredicate(string? rawScope)
    {
        var normalized = Normalize(rawScope);
        return header => normalized == "all"
            || (normalized == "imported" && header.DataOrigin == "access")
            || (normalized == "existing" && (header.DataOrigin == "existing" || header.DataOrigin == null || header.DataOrigin == ""));
    }

    public static bool IsIncluded(string? headerOrigin, string? rawScope)
    {
        var normalized = Normalize(rawScope);
        return normalized == "all"
            || (normalized == "imported" && string.Equals(headerOrigin, "access", StringComparison.Ordinal))
            || (normalized == "existing" && (string.Equals(headerOrigin, "existing", StringComparison.Ordinal) || string.IsNullOrEmpty(headerOrigin)));
    }
}
