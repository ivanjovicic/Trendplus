using Api.Models;
using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class VendorSalesNivelacijaDriverSummaryPolicyTests
{
    [Fact]
    public void SummarizesSameMetricPopulationsWithMedianSampleCountAndWeighting()
    {
        var comparableRows = new[]
        {
            new VendorSalesNivelacijaArticleStatDto
            {
                PriceElasticity = 1m,
                PostRevenue = 100m,
                MomentumRevenue = 2m,
                DidRevenue = 10m,
                LostSalesOOS = null
            },
            new VendorSalesNivelacijaArticleStatDto
            {
                PriceElasticity = 3m,
                PostRevenue = 300m,
                MomentumRevenue = null,
                DidRevenue = 20m,
                LostSalesOOS = 50m
            },
            new VendorSalesNivelacijaArticleStatDto
            {
                PriceElasticity = 100m,
                PostRevenue = 0m,
                MomentumRevenue = 4m,
                DidRevenue = null,
                LostSalesOOS = 150m
            }
        };

        var summary = VendorSalesNivelacijaDriverSummaryPolicy.Build(comparableRows, comparableRows);

        Assert.Equal(2.5m, summary.Elasticity.Mean);
        Assert.Equal(2m, summary.Elasticity.Median);
        Assert.Equal(2, summary.Elasticity.SampleCount);
        Assert.Equal(VendorSalesNivelacijaDriverSummaryPolicy.PostRevenueWeighted, summary.Elasticity.MeanWeighting);

        Assert.Equal(15m, summary.DidRevenue.Mean);
        Assert.Equal(15m, summary.DidRevenue.Median);
        Assert.Equal(2, summary.DidRevenue.SampleCount);
        Assert.Equal(VendorSalesNivelacijaDriverSummaryPolicy.Unweighted, summary.DidRevenue.MeanWeighting);

        Assert.Equal(100m, summary.LostSalesOOS.Mean);
        Assert.Equal(100m, summary.LostSalesOOS.Median);
        Assert.Equal(2, summary.LostSalesOOS.SampleCount);
        Assert.Equal(3m, summary.MomentumRevenue.Mean);
        Assert.Equal(2, summary.MomentumRevenue.SampleCount);
    }

    [Fact]
    public void MissingDriverEvidenceRemainsUnavailableInsteadOfBecomingZero()
    {
        var summary = VendorSalesNivelacijaDriverSummaryPolicy.Build(
            [new VendorSalesNivelacijaArticleStatDto { HasComparableSalesWindow = true }],
            [new VendorSalesNivelacijaArticleStatDto { HasComparableSalesWindow = true }]);

        Assert.Null(summary.Elasticity.Mean);
        Assert.Null(summary.Elasticity.Median);
        Assert.Equal(0, summary.Elasticity.SampleCount);
        Assert.Null(summary.DidRevenue.Mean);
        Assert.Null(summary.DidRevenue.Median);
        Assert.Equal(0, summary.DidRevenue.SampleCount);
        Assert.Null(summary.LostSalesOOS.Mean);
        Assert.Equal(0, summary.LostSalesOOS.SampleCount);
    }
}
