using Api.Models;

namespace Api.Services;

/// <summary>
/// Builds Supplier Footwear type insights from the complete comparable cohort.
/// The returned article detail may be truncated, but these aggregates must never
/// be reconstructed from that returned detail array.
/// </summary>
public static class VendorSalesNivelacijaTypeInsightPolicy
{
    public const string Source = "full_comparable_cohort";
    public const string Denominator = "comparable_post_revenue";
    public const string ElasticityWeighting = "post_revenue_weighted";

    public static IReadOnlyList<VendorSalesNivelacijaTypeInsightAggregate> Build(
        IEnumerable<VendorSalesNivelacijaArticleStatDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var comparableRows = rows
            .Where(row => row.HasComparableSalesWindow)
            .ToArray();
        var totalPostRevenue = comparableRows.Sum(row => row.PostRevenue);

        return comparableRows
            .GroupBy(row => NormalizeCategory(row.Category), StringComparer.Ordinal)
            .Select(group =>
            {
                var preRevenue = group.Sum(row => row.PreRevenue);
                var postRevenue = group.Sum(row => row.PostRevenue);

                return new VendorSalesNivelacijaTypeInsightAggregate(
                    Category: group.Key,
                    ArticlesCount: group
                        .Select(row => row.Sku)
                        .Where(sku => !string.IsNullOrWhiteSpace(sku))
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    VendorsCount: group.Select(row => row.VendorId).Distinct().Count(),
                    PreQty: group.Sum(row => row.PreQty),
                    PreRevenue: preRevenue,
                    PostQty: group.Sum(row => row.PostQty),
                    PostRevenue: postRevenue,
                    ChangeQty: group.Sum(row => row.ChangeQty),
                    ChangeRevenue: group.Sum(row => row.ChangeRevenue),
                    ChangePercent: CalculatePercent(preRevenue, postRevenue),
                    ComparableArticleCount: group
                        .Select(row => row.Sku)
                        .Where(sku => !string.IsNullOrWhiteSpace(sku))
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    AvgElasticity: WeightedAverageElasticity(group),
                    PostRevenueSharePercent: totalPostRevenue > 0m
                        ? Math.Round(postRevenue / totalPostRevenue * 100m, 2)
                        : null);
            })
            .OrderByDescending(aggregate => aggregate.PostRevenue)
            .ThenBy(aggregate => aggregate.Category, StringComparer.Ordinal)
            .ToList();
    }

    private static string NormalizeCategory(string? category) =>
        string.IsNullOrWhiteSpace(category) ? "Nepoznato" : category.Trim();

    private static decimal CalculatePercent(decimal preRevenue, decimal postRevenue)
    {
        if (preRevenue == 0m)
            return postRevenue > 0m ? 100m : 0m;

        return Math.Round((postRevenue - preRevenue) / preRevenue * 100m, 2);
    }

    private static decimal? WeightedAverageElasticity(IEnumerable<VendorSalesNivelacijaArticleStatDto> rows)
    {
        decimal weightedSum = 0m;
        decimal weightTotal = 0m;

        foreach (var row in rows)
        {
            if (!row.PriceElasticity.HasValue || row.PostRevenue <= 0m)
            {
                continue;
            }

            weightedSum += row.PriceElasticity.Value * row.PostRevenue;
            weightTotal += row.PostRevenue;
        }

        if (weightTotal > 0m)
        {
            return Math.Round(weightedSum / weightTotal, 4);
        }

        return AverageFinite(rows.Select(row => row.PriceElasticity));
    }

    private static decimal? AverageFinite(IEnumerable<decimal?> values)
    {
        var finite = values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return finite.Length == 0 ? null : Math.Round(finite.Average(), 4);
    }
}

public sealed record VendorSalesNivelacijaTypeInsightAggregate(
    string Category,
    int ArticlesCount,
    int VendorsCount,
    int PreQty,
    decimal PreRevenue,
    int PostQty,
    decimal PostRevenue,
    int ChangeQty,
    decimal ChangeRevenue,
    decimal ChangePercent,
    int ComparableArticleCount,
    decimal? AvgElasticity,
    decimal? PostRevenueSharePercent);
