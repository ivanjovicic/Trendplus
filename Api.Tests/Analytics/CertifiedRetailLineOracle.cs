using Api.Services;

namespace Trendplus2.Tests.Analytics;

/// <summary>
/// Implementation-independent in-memory oracle for certified retail promet.
/// Mirrors the shared Daily/Supplier/Shoe Type/Color population rules without
/// calling production aggregation endpoints (so both FE and BE can be wrong
/// and these still fail).
/// </summary>
public static class CertifiedRetailLineOracle
{
    public sealed record SaleLine(
        DateTime SoldAtUtc,
        int? StoreId,
        string? ReceiptNumber,
        string? DataOrigin,
        int Quantity,
        decimal UnitPrice,
        decimal? UnitCost = null,
        int? SupplierIdAtSale = null,
        int? ShoeTypeIdAtSale = null,
        string? ColorKey = null);

    public sealed record Filters(
        DateTime FromUtc,
        DateTime ToExclusiveUtc,
        int? StoreId = null,
        string DataScope = "all");

    public sealed record Totals(int LineCount, int Units, decimal Revenue);

    public sealed record Bucket(string Key, int Units, decimal Revenue, int LineCount);

    public static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    public static bool MatchesDataScope(string? dataOrigin, string dataScope)
    {
        var scope = NormalizeDataScope(dataScope);
        var origin = dataOrigin?.Trim() ?? string.Empty;
        return scope switch
        {
            "imported" => string.Equals(origin, "access", StringComparison.OrdinalIgnoreCase),
            "existing" => origin.Length == 0
                || string.Equals(origin, "existing", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    public static bool IsInHalfOpenWindow(DateTime soldAtUtc, DateTime fromUtc, DateTime toExclusiveUtc)
        => soldAtUtc >= fromUtc && soldAtUtc < toExclusiveUtc;

    public static IEnumerable<SaleLine> Filter(IEnumerable<SaleLine> lines, Filters filters)
    {
        foreach (var line in lines)
        {
            if (!IsInHalfOpenWindow(line.SoldAtUtc, filters.FromUtc, filters.ToExclusiveUtc))
                continue;
            if (filters.StoreId.HasValue && line.StoreId != filters.StoreId.Value)
                continue;
            if (SalesReceiptPopulationPolicy.IsExcluded(line.ReceiptNumber))
                continue;
            if (!MatchesDataScope(line.DataOrigin, filters.DataScope))
                continue;

            yield return line;
        }
    }

    public static Totals QueryTotals(IEnumerable<SaleLine> lines, Filters filters)
    {
        var matched = Filter(lines, filters).ToArray();
        return new Totals(
            matched.Length,
            matched.Sum(line => line.Quantity),
            matched.Sum(line => line.Quantity * line.UnitPrice));
    }

    public static IReadOnlyList<Bucket> QueryBuckets(
        IEnumerable<SaleLine> lines,
        Filters filters,
        Func<SaleLine, string> keySelector)
    {
        return Filter(lines, filters)
            .GroupBy(keySelector, StringComparer.Ordinal)
            .Select(group => new Bucket(
                group.Key,
                group.Sum(line => line.Quantity),
                group.Sum(line => line.Quantity * line.UnitPrice),
                group.Count()))
            .OrderBy(bucket => bucket.Key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Hand-checkable adversarial kit: 2 stores, multi supplier/type/color/day,
    /// boundary dates, sale, return, DUG/KOREKCIJA, missing cost, unknown FK,
    /// current-only and previous-only entities, negative margin, inventory-like
    /// movement via signed return.
    /// </summary>
    public static IReadOnlyList<SaleLine> CreateCanonicalKit()
    {
        var day = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var next = day.AddDays(1);
        var previousOnly = day.AddDays(-1);
        var upperExclusive = next; // sold exactly at toExclusive must be excluded

        return
        [
            // Store 1 — current window retail
            new(day.AddHours(9), 1, "R-100", "existing", 2, 100m, 50m, 1, 1, "CRNA"),
            new(day.AddHours(10), 1, "R-101", "existing", 1, 80m, null, null, null, null), // missing cost + unknown dims
            new(day.AddHours(11), 1, "R-102", "existing", -1, 100m, 50m, 1, 1, "CRNA"), // full unit return
            new(day.AddHours(12), 1, "R-103", "existing", 1, 80m, 100m, 2, 2, "PLAVA"), // negative margin
            new(day.AddHours(13), 1, "  dUg  ", "existing", 5, 200m, 10m, 1, 1, "CRNA"), // excluded
            new(day.AddHours(14), 1, " KoReKcIjA ", "existing", 3, 150m, 10m, 1, 1, "CRNA"), // excluded

            // Store 2 — imported, must not leak into store 1
            new(day.AddHours(15), 2, "R-200", "access", 2, 120m, 40m, 3, 2, "PLAVA"),

            // Boundary: included at from, excluded at toExclusive
            new(day, 1, "R-FROM", "existing", 1, 50m, 20m, 1, 1, "CRNA"),
            new(upperExclusive, 1, "R-TOEXCL", "existing", 9, 999m, 1m, 1, 1, "CRNA"),

            // Previous-only entity (type 5 / supplier 9) — outside current window
            new(previousOnly.AddHours(16), 1, "R-PREV", "existing", 2, 90m, 50m, 9, 5, "CRVENA"),

            // Next-day adjacent — for [A,C)=[A,B)+[B,C) additivity
            new(next.AddHours(8), 1, "R-NEXT", "existing", 1, 120m, 60m, 1, 1, "CRNA"),
        ];
    }
}
