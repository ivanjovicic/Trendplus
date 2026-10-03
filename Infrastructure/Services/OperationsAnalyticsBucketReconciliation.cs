using Application.Analytics;

namespace Infrastructure.Services;

/// <summary>Compares bounded bucket aggregates without collapsing unknown or signed buckets.</summary>
public static class OperationsAnalyticsBucketReconciliation
{
    public static IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta> Compare(
        string dimension,
        IEnumerable<BucketValue> expected,
        IEnumerable<BucketValue> actual,
        decimal revenueToleranceRsd = 0m)
    {
        var expectedByKey = expected.ToDictionary(row => row.Key, StringComparer.Ordinal);
        var actualByKey = actual.ToDictionary(row => row.Key, StringComparer.Ordinal);
        var keys = expectedByKey.Keys
            .Union(actualByKey.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal);
        var deltas = new List<OperationsAnalyticsIntegrityProbeDelta>();

        foreach (var key in keys)
        {
            expectedByKey.TryGetValue(key, out var expectedBucket);
            actualByKey.TryGetValue(key, out var actualBucket);
            var expectedRevenue = expectedBucket?.Revenue ?? 0m;
            var actualRevenue = actualBucket?.Revenue ?? 0m;
            var expectedUnits = expectedBucket?.Units ?? 0;
            var actualUnits = actualBucket?.Units ?? 0;
            var revenueDelta = actualRevenue - expectedRevenue;
            var unitsDelta = actualUnits - expectedUnits;

            if (Math.Abs(revenueDelta) <= Math.Max(0m, revenueToleranceRsd) && unitsDelta == 0)
                continue;

            deltas.Add(new OperationsAnalyticsIntegrityProbeDelta(
                dimension + ":" + key,
                actualRevenue,
                expectedRevenue,
                revenueDelta,
                actualUnits,
                expectedUnits,
                unitsDelta));
        }

        return deltas;
    }

    public sealed record BucketValue(string Key, decimal Revenue, int Units);
}
