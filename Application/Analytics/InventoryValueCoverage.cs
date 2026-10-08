namespace Application.Analytics;

public sealed record InventoryValueAggregate(
    decimal? Value,
    int KnownRows,
    int UnknownRows,
    decimal CoveragePct);

public static class InventoryValueCoverage
{
    /// <summary>
    /// Known-only stock value aggregate with the same population as the Inventory
    /// balance valuation: only positive on-hand rows carry capital. Zero or negative
    /// stock rows are measured "no capital" and must not inflate value coverage;
    /// positive rows without a known value stay unknown instead of becoming 0.
    /// </summary>
    public static InventoryValueAggregate Aggregate(IEnumerable<(int Quantity, decimal? Value)> rows)
    {
        var stocked = rows.Where(row => row.Quantity > 0).ToArray();
        if (stocked.Length == 0)
        {
            return new InventoryValueAggregate(0m, 0, 0, 100m);
        }

        var known = stocked.Where(row => row.Value.HasValue).Select(row => row.Value!.Value).ToArray();
        return new InventoryValueAggregate(
            known.Length == 0 ? null : known.Sum(),
            known.Length,
            stocked.Length - known.Length,
            Math.Round((decimal)known.Length / stocked.Length * 100m, 1));
    }

    /// <summary>
    /// Pareto ABC over known positive values. An item's class is decided by the
    /// cumulative share of the items ranked before it, so the largest value is
    /// always A (a single-SKU population is not C). Unknown values stay N/A and
    /// zero/negative values are C.
    /// </summary>
    public static string[] ClassifyAbc(IReadOnlyList<(decimal? Value, string Name)> values)
    {
        var result = Enumerable.Repeat("N/A", values.Count).ToArray();
        var known = values
            .Select((entry, index) => (entry, index))
            .Where(item => item.entry.Value.HasValue)
            .ToArray();
        if (known.Length == 0)
        {
            return result;
        }

        foreach (var item in known)
        {
            result[item.index] = "C";
        }

        var positive = known.Where(item => item.entry.Value!.Value > 0m).ToArray();
        var total = positive.Sum(item => item.entry.Value!.Value);
        if (total <= 0m)
        {
            return result;
        }

        var runningBefore = 0m;
        foreach (var item in positive
            .OrderByDescending(item => item.entry.Value!.Value)
            .ThenBy(item => item.entry.Name, StringComparer.CurrentCulture))
        {
            var shareBefore = runningBefore / total;
            result[item.index] = shareBefore < 0.80m ? "A" : shareBefore < 0.95m ? "B" : "C";
            runningBefore += item.entry.Value!.Value;
        }

        return result;
    }
}
