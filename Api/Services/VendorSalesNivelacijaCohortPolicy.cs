using Api.Models;

namespace Api.Services;

/// <summary>
/// Defines the canonical Pre/Post cohort used for aggregate and recommendation evidence.
/// One latest price event is retained per article so overlapping event windows cannot count
/// the same article sales more than once in a single request.
/// </summary>
public static class VendorSalesNivelacijaCohortPolicy
{
    public const string LatestEventPerArticle = "latest_event_per_article";

    public static IReadOnlyList<VendorSalesNivelacijaArticleStatDto> SelectLatestEventPerArticle(
        IEnumerable<VendorSalesNivelacijaArticleStatDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows
            .GroupBy(row => row.ArticleId)
            .Select(group => group
                .OrderByDescending(row => row.EventDate)
                .ThenByDescending(row => row.PriceEventId)
                .First())
            .ToList();
    }
}
