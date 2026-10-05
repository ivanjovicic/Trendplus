namespace Application.Analytics;

public static class CategoricalDimensionCoverageStates
{
    public const string PopulatedInSource = "populated_in_source";
    public const string NotPopulatedInSource = "not_populated_in_source";
}

public sealed record CategoricalDimensionCoverageDto(
    decimal? KnownCoveragePct,
    string DimensionCoverageState);

/// <summary>
/// Shared rule for categorical analytics dimensions: 0% known revenue means the
/// dimension is not populated in source and must not be shown as a distribution.
/// </summary>
public static class CategoricalDimensionCoveragePolicy
{
    public static CategoricalDimensionCoverageDto ResolveFromRevenueBuckets(
        IEnumerable<(decimal Revenue, bool IsUnknown)> buckets)
    {
        var materialized = buckets.ToList();
        var totalRevenue = materialized.Sum(bucket => bucket.Revenue);
        if (totalRevenue <= 0m)
        {
            return new CategoricalDimensionCoverageDto(null, CategoricalDimensionCoverageStates.PopulatedInSource);
        }

        var knownRevenue = materialized.Where(bucket => !bucket.IsUnknown).Sum(bucket => bucket.Revenue);
        if (knownRevenue <= 0m)
        {
            return new CategoricalDimensionCoverageDto(0m, CategoricalDimensionCoverageStates.NotPopulatedInSource);
        }

        var knownCoveragePct = Math.Round(knownRevenue / totalRevenue * 100m, 2);
        return new CategoricalDimensionCoverageDto(
            knownCoveragePct,
            CategoricalDimensionCoverageStates.PopulatedInSource);
    }

    public static bool IsUnknownColor(string? label)
        => string.Equals(NormalizeLabel(label), "Nepoznato", StringComparison.OrdinalIgnoreCase);

    public static bool IsUnknownCategory(string? label)
        => string.Equals(NormalizeLabel(label), "Ostalo", StringComparison.OrdinalIgnoreCase);

    public static bool IsUnknownGender(string? label)
    {
        var normalized = NormalizeLabel(label);
        return string.Equals(normalized, "Neodređeno", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Neodredeno", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "NeodreÄ‘eno", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnknownPayment(string? label)
        => string.Equals(NormalizeLabel(label), "Nepoznato", StringComparison.OrdinalIgnoreCase);

    public static CategoricalDimensionCoverageDto ResolveHourDistribution(
        IEnumerable<(int Hour, decimal Revenue, int TransactionCount)> buckets)
    {
        var materialized = buckets.ToList();
        var totalTransactions = materialized.Sum(bucket => bucket.TransactionCount);
        if (totalTransactions <= 0)
        {
            return new CategoricalDimensionCoverageDto(null, CategoricalDimensionCoverageStates.PopulatedInSource);
        }

        var hourZeroTransactions = materialized
            .Where(bucket => bucket.Hour == 0)
            .Sum(bucket => bucket.TransactionCount);
        var hourZeroSharePct = Math.Round((decimal)hourZeroTransactions / totalTransactions * 100m, 2);
        var knownCoveragePct = Math.Round(100m - hourZeroSharePct, 2);
        if (knownCoveragePct <= 0m || hourZeroSharePct >= 99m)
        {
            return new CategoricalDimensionCoverageDto(
                Math.Max(0m, knownCoveragePct),
                CategoricalDimensionCoverageStates.NotPopulatedInSource);
        }

        return new CategoricalDimensionCoverageDto(
            knownCoveragePct,
            CategoricalDimensionCoverageStates.PopulatedInSource);
    }

    private static string NormalizeLabel(string? label)
        => string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
}
