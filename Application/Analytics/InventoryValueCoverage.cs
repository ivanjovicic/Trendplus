namespace Application.Analytics;

public sealed record InventoryValueAggregate(
    decimal? Value,
    int KnownRows,
    int UnknownRows,
    decimal CoveragePct);

public static class InventoryValueCoverage
{
    public static InventoryValueAggregate Aggregate(IEnumerable<decimal?> values)
    {
        var materialized = values.ToArray();
        var known = materialized.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return new InventoryValueAggregate(
            known.Length == 0 ? null : known.Sum(),
            known.Length,
            materialized.Length - known.Length,
            materialized.Length == 0 ? 100m : Math.Round((decimal)known.Length / materialized.Length * 100m, 1));
    }

    public static string[] ClassifyAbc(IReadOnlyList<(decimal? Value, string Name)> values)
    {
        var result = Enumerable.Repeat("N/A", values.Count).ToArray();
        var known = values
            .Select((entry, index) => (entry, index))
            .Where(item => item.entry.Value.HasValue)
            .ToArray();
        var total = known.Sum(item => item.entry.Value!.Value);
        if (known.Length == 0)
        {
            return result;
        }

        if (total <= 0)
        {
            foreach (var item in known)
            {
                result[item.index] = "C";
            }

            return result;
        }

        var running = 0m;
        foreach (var item in known
            .OrderByDescending(item => item.entry.Value!.Value)
            .ThenBy(item => item.entry.Name, StringComparer.CurrentCulture))
        {
            running += item.entry.Value!.Value;
            var share = running / total;
            result[item.index] = share <= 0.80m ? "A" : share <= 0.95m ? "B" : "C";
        }

        return result;
    }
}
