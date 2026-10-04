using Api.Models;

namespace Api.Services;

/// <summary>
/// Exposes descriptive driver distributions over the same event rows used by
/// the existing Pre/Post aggregate values. These summaries do not feed policy.
/// </summary>
public static class VendorSalesNivelacijaDriverSummaryPolicy
{
    public const string Unweighted = "unweighted";
    public const string PostRevenueWeighted = "post_revenue_weighted";

    public static VendorSalesNivelacijaDriverSummaryDto Build(
        IEnumerable<VendorSalesNivelacijaArticleStatDto> comparableRows,
        IEnumerable<VendorSalesNivelacijaArticleStatDto> matureComparableRows)
    {
        ArgumentNullException.ThrowIfNull(comparableRows);
        ArgumentNullException.ThrowIfNull(matureComparableRows);

        var comparable = comparableRows.ToArray();
        var matureComparable = matureComparableRows.ToArray();
        var elasticityRows = matureComparable
            .Where(row => row.PriceElasticity.HasValue && row.PostRevenue > 0m)
            .ToArray();

        return new VendorSalesNivelacijaDriverSummaryDto
        {
            MomentumRevenue = BuildUnweighted(comparable.Select(row => row.MomentumRevenue)),
            Elasticity = new VendorSalesNivelacijaDriverMetricSummaryDto
            {
                Mean = VendorSalesNivelacijaTypeInsightPolicy.WeightedMeanElasticity(elasticityRows),
                Median = Median(elasticityRows.Select(row => row.PriceElasticity)),
                SampleCount = elasticityRows.Length,
                MeanWeighting = PostRevenueWeighted
            },
            DidRevenue = BuildUnweighted(comparable.Select(row => row.DidRevenue)),
            LostSalesOOS = BuildUnweighted(comparable.Select(row => row.LostSalesOOS))
        };
    }

    private static VendorSalesNivelacijaDriverMetricSummaryDto BuildUnweighted(IEnumerable<decimal?> source)
    {
        var values = source
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return new VendorSalesNivelacijaDriverMetricSummaryDto
        {
            Mean = values.Length == 0 ? null : values.Sum() / values.Length,
            Median = Median(values),
            SampleCount = values.Length,
            MeanWeighting = Unweighted
        };
    }

    private static decimal? Median(IEnumerable<decimal?> source) =>
        Median(source.Where(value => value.HasValue).Select(value => value!.Value));

    private static decimal? Median(IEnumerable<decimal> source)
    {
        var values = source.Order().ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        var middle = values.Length / 2;
        return values.Length % 2 == 1
            ? values[middle]
            : values[middle - 1] + ((values[middle] - values[middle - 1]) / 2m);
    }
}
