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
    public void ElasticityUsesPostRevenueWeightingWithinCategory()
    {
        var aggregates = VendorSalesNivelacijaTypeInsightPolicy.Build(
        [
            ComparableRow("SKU-LOW", "Patike", 10m, 100m, 0.2m),
            ComparableRow("SKU-HIGH", "Patike", 10m, 900m, 0.8m),
        ]);

        Assert.Single(aggregates);
        Assert.Equal(0.74m, aggregates[0].AvgElasticity);
    }

    [Fact]
    public void PointEstimateRequiresMatureNonLowSignalMarkdownAndFivePercentChange()
    {
        var eligible = new VendorSalesNivelacijaArticleStatDto
        {
            HasComparableSalesWindow = true,
            IsPostWindowMature = true,
            OldPrice = 100m,
            NewPrice = 80m,
            PreQty = 10,
            PostQty = 4
        };
        Assert.Equal(3m, VendorSalesNivelacijaTypeInsightPolicy.ComputePointEstimate(eligible));
        eligible.IsLowSignal = true;
        Assert.Null(VendorSalesNivelacijaTypeInsightPolicy.ComputePointEstimate(eligible));
        eligible.IsLowSignal = false;
        eligible.IsPostWindowMature = false;
        Assert.Null(VendorSalesNivelacijaTypeInsightPolicy.ComputePointEstimate(eligible));
        eligible.IsPostWindowMature = true;
        eligible.NewPrice = 105m;
        Assert.Null(VendorSalesNivelacijaTypeInsightPolicy.ComputePointEstimate(eligible));
        eligible.NewPrice = 96m;
        Assert.Null(VendorSalesNivelacijaTypeInsightPolicy.ComputePointEstimate(eligible));
        eligible.NewPrice = 80m;
        eligible.PreQty = 1;
        eligible.PostQty = 1000;
        Assert.Equal(-10m, VendorSalesNivelacijaTypeInsightPolicy.ComputePointEstimate(eligible));
    }

    [Fact]
    public void WeightedElasticityDoesNotFallBackToUnweightedWhenRevenueWeightIsMissing()
    {
        var rows = new[]
        {
            ComparableRow("SKU-ZERO", "Patike", 10m, 0m, 3m),
            ComparableRow("SKU-NEGATIVE", "Patike", 10m, -5m, 9m)
        };

        Assert.Null(VendorSalesNivelacijaTypeInsightPolicy.WeightedMeanElasticity(rows));
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

    [Fact]
    public void MissingRevenueBaselineKeepsCategoryChangePercentUnavailable()
    {
        var aggregates = VendorSalesNivelacijaTypeInsightPolicy.Build(
        [
            ComparableRow("SKU-1", "Patike", 0m, 120m, null)
        ]);

        Assert.Null(aggregates[0].ChangePercent);
    }

    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(100, 0, -100)]
    public void MeasuredPostRevenueZeroRemainsDistinctFromMissingBaseline(
        decimal preRevenue,
        decimal postRevenue,
        decimal expectedChangePercent)
    {
        var aggregates = VendorSalesNivelacijaTypeInsightPolicy.Build(
        [
            ComparableRow("SKU-1", "Patike", preRevenue, postRevenue, null)
        ]);

        Assert.Equal(expectedChangePercent, aggregates[0].ChangePercent);
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
