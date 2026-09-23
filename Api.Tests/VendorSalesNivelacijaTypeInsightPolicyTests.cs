using Api.Models;
using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class VendorSalesNivelacijaTypeInsightPolicyTests
{
    [Fact]
    public void FullComparableCohortKeepsTypeShareIndependentFromTruncatedArticleDetail()
    {
        var analyzed = new[]
        {
            ComparableRow("SKU-PATIKE", "Patike", 100m, 10m, 0.4m),
            ComparableRow("SKU-CIPELE", "Cipele", 50m, 100m, 0.8m),
            new VendorSalesNivelacijaArticleStatDto
            {
                Sku = "SKU-NON-COMPARABLE",
                Category = "Sandale",
                PostRevenue = 999m,
                HasComparableSalesWindow = false
            }
        };

        var returnedArticleDetail = analyzed.Take(1).ToArray();
        var fullAggregates = VendorSalesNivelacijaTypeInsightPolicy.Build(analyzed);
        var truncatedAggregates = VendorSalesNivelacijaTypeInsightPolicy.Build(returnedArticleDetail);

        Assert.Equal("Cipele", fullAggregates[0].Category);
        Assert.Equal(90.91m, fullAggregates[0].PostRevenueSharePercent);
        Assert.Equal("Patike", truncatedAggregates[0].Category);
        Assert.Equal(100m, truncatedAggregates[0].PostRevenueSharePercent);
        Assert.DoesNotContain(fullAggregates, aggregate => aggregate.Category == "Sandale");
    }

    [Fact]
    public void MissingPostRevenueDenominatorRemainsUnavailable()
    {
        var aggregates = VendorSalesNivelacijaTypeInsightPolicy.Build(
        [
            ComparableRow("SKU-1", "Patike", 0m, 0m, null)
        ]);

        Assert.Single(aggregates);
        Assert.Null(aggregates[0].PostRevenueSharePercent);
        Assert.Null(aggregates[0].AvgElasticity);
    }

    private static VendorSalesNivelacijaArticleStatDto ComparableRow(
        string sku,
        string category,
        decimal preRevenue,
        decimal postRevenue,
        decimal? elasticity) => new()
        {
            Sku = sku,
            Category = category,
            PreRevenue = preRevenue,
            PostRevenue = postRevenue,
            HasComparableSalesWindow = true,
            PriceElasticity = elasticity,
            PreQty = 1,
            PostQty = 1,
            ChangeQty = 0,
            ChangeRevenue = postRevenue - preRevenue,
            VendorId = 1
        };
}
