using System.Text.Json;
using Application.Analytics;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class TopProductMarginCoverageTests
{
    [Fact]
    public void AllCostsKnown_AreConfirmedAndFullyCovered()
    {
        var result = AnalyticsMarginPolicy.ClassifyTopProductCoverage(
            totalRevenue: 1_000m,
            totalUnits: 10,
            costCoveredRevenue: 1_000m,
            costCoveredUnits: 10,
            totalLines: 2,
            costCoveredLines: 2);

        Assert.Equal(100m, result.CoveragePct);
        Assert.Equal("confirmed", result.Status);
        Assert.True(result.IsConfirmed);
    }

    [Fact]
    public void NoCostsKnown_RemainUnavailableInsteadOfConfirmedZero()
    {
        var result = AnalyticsMarginPolicy.ClassifyTopProductCoverage(
            totalRevenue: 1_000m,
            totalUnits: 10,
            costCoveredRevenue: 0m,
            costCoveredUnits: 0,
            totalLines: 2,
            costCoveredLines: 0);

        Assert.Equal(0m, result.CoveragePct);
        Assert.Equal("no_data", result.Status);
        Assert.False(result.IsConfirmed);
    }

    [Fact]
    public void MixedCostCoverage_IsPartialAndNotEligibleForConfirmedRanking()
    {
        var result = AnalyticsMarginPolicy.ClassifyTopProductCoverage(
            totalRevenue: 1_000m,
            totalUnits: 10,
            costCoveredRevenue: 400m,
            costCoveredUnits: 4,
            totalLines: 2,
            costCoveredLines: 1);

        Assert.Equal(40m, result.CoveragePct);
        Assert.Equal("partial", result.Status);
        Assert.False(result.IsConfirmed);
        Assert.False(AnalyticsMarginPolicy.IsConfirmedMarginRankingEvidence(result, 250m));
    }

    [Fact]
    public void FullyCoveredGenuineZeroMargin_RemainsEligibleForRanking()
    {
        var result = AnalyticsMarginPolicy.ClassifyTopProductCoverage(
            totalRevenue: 500m,
            totalUnits: 5,
            costCoveredRevenue: 500m,
            costCoveredUnits: 5,
            totalLines: 1,
            costCoveredLines: 1);

        const decimal genuineZeroMargin = 0m;
        Assert.True(AnalyticsMarginPolicy.IsConfirmedMarginRankingEvidence(result, genuineZeroMargin));
    }

    [Fact]
    public void InvalidCostsAreExcludedFromCoverageEvidence()
    {
        var unresolved = AnalyticsMarginPolicy.ResolveUnitCost(
            saleLineCost: null,
            productCostRsd: 0m,
            productCostLegacy: -10m);

        var result = AnalyticsMarginPolicy.ClassifyTopProductCoverage(
            totalRevenue: 500m,
            totalUnits: 5,
            costCoveredRevenue: 0m,
            costCoveredUnits: 0,
            totalLines: 1,
            costCoveredLines: 0);

        Assert.Null(unresolved);
        Assert.Equal("no_data", result.Status);
        Assert.False(result.IsConfirmed);
    }

    [Fact]
    public void UnknownDenominatorRemainsUnknownInsteadOfBecomingZeroCoverage()
    {
        var result = AnalyticsMarginPolicy.ClassifyTopProductCoverage(
            totalRevenue: 0m,
            totalUnits: 0,
            costCoveredRevenue: 0m,
            costCoveredUnits: 0,
            totalLines: 0,
            costCoveredLines: 0);

        Assert.Null(result.CoveragePct);
        Assert.Equal("unknown", result.Status);
        Assert.False(result.IsConfirmed);
    }

    [Fact]
    public void AggregateRevenueCannotConfirmWhenCostLinesAreMissing()
    {
        var result = AnalyticsMarginPolicy.ClassifyTopProductCoverage(
            totalRevenue: 1_000m,
            totalUnits: 10,
            costCoveredRevenue: 1_000m,
            costCoveredUnits: 10,
            totalLines: 2,
            costCoveredLines: 1);

        Assert.Equal("partial", result.Status);
        Assert.False(result.IsConfirmed);
    }

    [Fact]
    public void AdvancedDtoCarriesCoverageStateForSerializationParity()
    {
        var payload = new TopProductAdvancedItemDto
        {
            ProductId = 17,
            Revenue = 1_000m,
            Units = 10,
            MarginImpact = 250m,
            CostCoveredRevenue = 400m,
            CostCoveredUnits = 4,
            TotalLines = 2,
            CostCoveredLines = 1,
            MarginCoveragePct = 40m,
            MarginCoverageStatus = "partial",
            MarginQualityTier = "partial",
            DataQualityStatus = "warning"
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = document.RootElement;

        Assert.Equal(400m, root.GetProperty("costCoveredRevenue").GetDecimal());
        Assert.Equal(4, root.GetProperty("costCoveredUnits").GetInt32());
        Assert.Equal(2, root.GetProperty("totalLines").GetInt32());
        Assert.Equal(1, root.GetProperty("costCoveredLines").GetInt32());
        Assert.Equal(40m, root.GetProperty("marginCoveragePct").GetDecimal());
        Assert.Equal("partial", root.GetProperty("marginCoverageStatus").GetString());
    }

    [Fact]
    public void ConfirmedMarginRanking_ExcludesPartialRowsButKeepsGenuineZeroMargin()
    {
        var rows = new[]
        {
            new TopProductAdvancedItemDto
            {
                ProductId = 1,
                MarginImpact = 900m,
                MarginQualityTier = "partial"
            },
            new TopProductAdvancedItemDto
            {
                ProductId = 2,
                MarginImpact = 0m,
                MarginQualityTier = "confirmed"
            }
        };

        var ranked = CachedAnalyticsEndpoints.SelectConfirmedMarginRanking(rows, top: 10);

        var row = Assert.Single(ranked);
        Assert.Equal(2, row.ProductId);
        Assert.Equal(0m, row.MarginImpact);
    }
}
