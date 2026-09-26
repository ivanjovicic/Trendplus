using Api.Models;
using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class VendorSalesNivelacijaCohortPolicyTests
{
    [Fact]
    public void SelectLatestEventPerArticleRemovesOverlappingEventRowsWithoutRemovingDistinctArticles()
    {
        var rows = new[]
        {
            new VendorSalesNivelacijaArticleStatDto
            {
                ArticleId = 10,
                PriceEventId = 100,
                EventDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                PostRevenue = 100m,
            },
            new VendorSalesNivelacijaArticleStatDto
            {
                ArticleId = 10,
                PriceEventId = 101,
                EventDate = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc),
                PostRevenue = 200m,
            },
            new VendorSalesNivelacijaArticleStatDto
            {
                ArticleId = 11,
                PriceEventId = 102,
                EventDate = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc),
                PostRevenue = 300m,
            },
        };

        var cohort = VendorSalesNivelacijaCohortPolicy.SelectLatestEventPerArticle(rows);

        Assert.Equal(2, cohort.Count);
        Assert.Contains(cohort, row => row.ArticleId == 10 && row.PriceEventId == 101 && row.PostRevenue == 200m);
        Assert.Contains(cohort, row => row.ArticleId == 11 && row.PriceEventId == 102);
        Assert.Equal(VendorSalesNivelacijaCohortPolicy.LatestEventPerArticle, "latest_event_per_article");
    }
}
