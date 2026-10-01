using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class SupplierCrossTabParityContractTests
{
    private static readonly DateTime FixtureNowUtc = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OneFixtureDeclaresSharedBasisAndExactExplainedCrossTabDifferences()
    {
        var fixture = new SupplierCrossTabFixture(
            FixtureNowUtc,
            EffectiveDataset: "90d",
            SupplierRows: new (int? SupplierId, string? SupplierName)[]
            {
                (SupplierId: 17, SupplierName: "Supplier A"),
                (SupplierId: 18, SupplierName: " nepoznato "),
                (SupplierId: (int?)null, SupplierName: null),
            });

        var overview = SupplierTabBasisPolicy.Overview(fixture.NowUtc);
        var scorecard = SupplierTabBasisPolicy.Scorecard(fixture.EffectiveDataset, fixture.NowUtc);
        var assortment = SupplierTabBasisPolicy.Assortment(fixture.NowUtc);

        var tabs = new[] { overview, scorecard, assortment };
        Assert.Equal(new[] { "overview", "scorecard", "assortment" }, tabs.Select(tab => tab.Tab));
        Assert.All(tabs, tab => Assert.Equal(SupplierTabBasisPolicy.Version, tab.Version));
        Assert.All(tabs, tab => Assert.Equal(SupplierTabBasisPolicy.RetailReceiptsExcludingDugKorekcija, tab.ReceiptPopulation));
        Assert.All(tabs, tab => Assert.Equal(SupplierTabBasisPolicy.Timezone, tab.Timezone));
        Assert.All(tabs, tab => Assert.Equal("2026-10-01", tab.AsOfDate));

        Assert.Equal(SupplierTabBasisPolicy.SaleTimeSupplier, overview.SupplierAttribution);
        Assert.Equal(SupplierTabBasisPolicy.MarkdownEventSupplierWithSaleTimeReturns, scorecard.SupplierAttribution);
        Assert.Equal(SupplierTabBasisPolicy.MarkdownEventSupplier, assortment.SupplierAttribution);

        Assert.Equal(SupplierTabBasisPolicy.SaleLineThenSnapshotThenCurrentCost, overview.CostBasis);
        Assert.Equal(SupplierTabBasisPolicy.SaleLineThenCurrentCost, scorecard.CostBasis);
        Assert.Equal(SupplierTabBasisPolicy.CurrentArticleCost, assortment.CostBasis);

        Assert.Equal(SupplierTabBasisPolicy.AllSalesInPeriod, overview.Cohort);
        Assert.Equal(SupplierTabBasisPolicy.FirstMarkdownPerArticle, scorecard.Cohort);
        Assert.Equal(SupplierTabBasisPolicy.LatestPriceEventPerArticle, assortment.Cohort);

        Assert.Equal(SupplierTabBasisPolicy.SaleDateInPeriod, overview.PeriodSemantics);
        Assert.Equal(SupplierTabBasisPolicy.RollingWindowMarkdownsWithPrePostWindows, scorecard.PeriodSemantics);
        Assert.Equal(SupplierTabBasisPolicy.PriceEventDateInPeriodWithPrePostWindows, assortment.PeriodSemantics);

        Assert.Equal(SupplierTabBasisPolicy.ReceiptStoreWithChainWideMarkdowns, overview.StoreScope);
        Assert.Equal(SupplierTabBasisPolicy.StoreFilterLimitsSalesEvidence, scorecard.StoreScope);
        Assert.Equal(SupplierTabBasisPolicy.StoreFilterAppliesToEventsAndSales, assortment.StoreScope);

        Assert.Equal(SupplierTabBasisPolicy.SingleUnknownBucket, overview.UnknownSupplierPolicy);
        Assert.Equal(SupplierTabBasisPolicy.UnresolvedSupplierNotCollapsed, scorecard.UnknownSupplierPolicy);
        Assert.Equal(SupplierTabBasisPolicy.SingleUnknownBucket, assortment.UnknownSupplierPolicy);

        var normalizedIdentities = fixture.SupplierRows
            .Select(row => SupplierUnknownBucketPolicy.Resolve(row.SupplierId, row.SupplierName))
            .ToArray();

        Assert.Equal((17, "Supplier A", false), normalizedIdentities[0]);
        Assert.Equal((null, "Nepoznato", true), normalizedIdentities[1]);
        Assert.Equal((null, "Nepoznato", true), normalizedIdentities[2]);
    }

    private sealed record SupplierCrossTabFixture(
        DateTime NowUtc,
        string EffectiveDataset,
        IReadOnlyList<(int? SupplierId, string? SupplierName)> SupplierRows);
}
