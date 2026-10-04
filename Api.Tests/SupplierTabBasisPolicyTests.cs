using Api.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace Api.Tests;

public sealed class SupplierTabBasisPolicyTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void OverviewBasisDeclaresSaleTimeAttributionAndHistoricalCostFallback()
    {
        var basis = SupplierTabBasisPolicy.Overview(NowUtc);

        Assert.Equal("overview", basis.Tab);
        Assert.Equal(SupplierTabBasisPolicy.Version, basis.Version);
        Assert.Equal("sale_time_supplier", basis.SupplierAttribution);
        Assert.Equal("sale_line_then_snapshot_then_current_article_cost", basis.CostBasis);
        Assert.Equal(SupplierTabBasisPolicy.RetailReceiptsExcludingDugKorekcija, basis.ReceiptPopulation);
        Assert.Equal("all_sales_in_period", basis.Cohort);
        Assert.Equal("sale_date_in_period", basis.PeriodSemantics);
        Assert.Equal(SupplierTabBasisPolicy.EqualDurationObservedWindowRevenueAndUnitsChangePct, basis.EffectMetric);
        Assert.Equal(SupplierTabBasisPolicy.LatestNivelacijaBeforePeriodEndPerArticle, basis.EventSelection);
        Assert.Equal(SupplierUnknownBucketPolicy.Policy, basis.UnknownSupplierPolicy);
        Assert.Equal("2026-10-01", basis.AsOfDate);
        Assert.Equal("UTC", basis.Timezone);
    }

    [Theory]
    [InlineData("all_time", "all_time_markdowns_with_30d_pre_post")]
    [InlineData("90d", "first_markdown_in_rolling_window_with_30d_pre_post")]
    [InlineData("180d", "first_markdown_in_rolling_window_with_30d_pre_post")]
    [InlineData(null, "first_markdown_in_rolling_window_with_30d_pre_post")]
    public void ScorecardBasisPeriodSemanticsFollowEffectiveDataset(string? effectiveDataset, string expectedPeriod)
    {
        var basis = SupplierTabBasisPolicy.Scorecard(effectiveDataset, NowUtc);

        Assert.Equal("scorecard", basis.Tab);
        Assert.Equal(expectedPeriod, basis.PeriodSemantics);
        Assert.Equal("first_markdown_per_article", basis.Cohort);
        Assert.Equal("markdown_event_supplier_with_sale_time_returns", basis.SupplierAttribution);
        Assert.Equal("unresolved_supplier_not_collapsed", basis.UnknownSupplierPolicy);
        Assert.Equal(SupplierTabBasisPolicy.Fixed30DayRevenueAndUnitsPct, basis.EffectMetric);
        Assert.Equal(SupplierTabBasisPolicy.FirstMarkdownPerArticleEvent, basis.EventSelection);
    }

    [Fact]
    public void AssortmentBasisDeclaresLatestEventCohortCurrentCostAndStoreScope()
    {
        var basis = SupplierTabBasisPolicy.Assortment(NowUtc);

        Assert.Equal("assortment", basis.Tab);
        Assert.Equal("markdown_event_supplier", basis.SupplierAttribution);
        Assert.Equal("current_article_cost", basis.CostBasis);
        Assert.Equal("latest_price_event_per_article_including_increases", basis.Cohort);
        Assert.Equal("price_event_date_in_period_with_30d_pre_post", basis.PeriodSemantics);
        Assert.Equal("store_filter_applies_to_events_and_sales", basis.StoreScope);
        Assert.Equal(SupplierUnknownBucketPolicy.Policy, basis.UnknownSupplierPolicy);
        Assert.Equal(SupplierTabBasisPolicy.Fixed30DayRevenueAndUnitsPct, basis.EffectMetric);
        Assert.Equal(SupplierTabBasisPolicy.LatestPriceEventPerArticleEvent, basis.EventSelection);
    }

    [Fact]
    public void ShoeTypeAndColorBasisDeclareSplitEffectAndArticleEventCohort()
    {
        var bases = new[]
        {
            SupplierTabBasisPolicy.ShoeType(NowUtc),
            SupplierTabBasisPolicy.Color(NowUtc)
        };

        Assert.Equal(new[] { "shoe_type", "color" }, bases.Select(basis => basis.Tab));
        Assert.All(bases, basis =>
        {
            Assert.Equal("articles_with_nivelacija_event_and_sales_in_period", basis.Cohort);
            Assert.Equal(SupplierTabBasisPolicy.EqualDurationObservedWindowRevenueAndUnitsChangePct, basis.EffectMetric);
            Assert.Equal(SupplierTabBasisPolicy.LatestNivelacijaBeforePeriodEndPerArticle, basis.EventSelection);
        });
    }

    [Fact]
    public void AllTabsShareOneReceiptPopulationAndVersion()
    {
        var bases = new[]
        {
            SupplierTabBasisPolicy.Overview(NowUtc),
            SupplierTabBasisPolicy.Scorecard("90d", NowUtc),
            SupplierTabBasisPolicy.Assortment(NowUtc),
            SupplierTabBasisPolicy.ShoeType(NowUtc),
            SupplierTabBasisPolicy.Color(NowUtc),
        };

        Assert.All(bases, basis =>
        {
            Assert.Equal(SupplierTabBasisPolicy.RetailReceiptsExcludingDugKorekcija, basis.ReceiptPopulation);
            Assert.Equal(SupplierTabBasisPolicy.Version, basis.Version);
            Assert.Equal("UTC", basis.Timezone);
        });
    }

    [Fact]
    public void UnknownBucketCollapsesMissingUnmappedAndNamedUnknownSuppliers()
    {
        Assert.Equal((null, "Nepoznato", true), SupplierUnknownBucketPolicy.Resolve(null, null));
        Assert.Equal((null, "Nepoznato", true), SupplierUnknownBucketPolicy.Resolve(17, "  "));
        Assert.Equal((null, "Nepoznato", true), SupplierUnknownBucketPolicy.Resolve(18, " nepoznato "));
        Assert.Equal((5, "Dobavljač A", false), SupplierUnknownBucketPolicy.Resolve(5, " Dobavljač A "));
    }

    [Fact]
    public void UnknownBucketCountsDistinctFoldedSourceIds()
    {
        var sources = new (int?, string?)[]
        {
            (null, null),
            (null, "Nepoznato"),
            (17, "Nepoznato"),
            (17, null),
            (18, ""),
            (5, "Dobavljač A"),
        };

        Assert.Equal(3, SupplierUnknownBucketPolicy.CountUnresolvedSourceIds(sources));
    }

    [Fact]
    public void VendorNivelacijaStartupViewsExcludeDugAndKorekcijaInBothSalesDailyCtes()
    {
        var sql = ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");

        var exclusions = Regex.Matches(
            sql,
            @"JOIN prodaja_zaglavlje pz\s+ON pz\.id = ps\.id_prodaja\s+(?:--[^\n]*\n\s*)?WHERE UPPER\(TRIM\(COALESCE\(pz\.broj_racuna, ''\)\)\) NOT IN \('DUG', 'KOREKCIJA'\)");
        var salesDailyCtes = Regex.Matches(sql, @"sales_daily AS \(");

        Assert.Equal(2, salesDailyCtes.Count);
        Assert.Equal(2, exclusions.Count);
    }

    [Fact]
    public void ScopedVendorNivelacijaSourceSqlExcludesDugAndKorekcija()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var start = source.IndexOf("static string BuildVendorSalesNivelacijaScopedSourceSql(", StringComparison.Ordinal);
        Assert.True(start >= 0, "Scoped vendor nivelacija SQL builder not found.");
        var body = source.Substring(start, Math.Min(8000, source.Length - start));

        Assert.Contains("AND UPPER(TRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')", body);
    }

    [Fact]
    public void EverySupplierTabResponsePathPublishesAndPreservesBasis()
    {
        var allEndpoints = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var hub = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("supplierTrustMeta.Basis = SupplierTabBasisPolicy.Overview(", allEndpoints);
        Assert.Contains("response.Meta.Basis = SupplierTabBasisPolicy.Assortment(", allEndpoints);
        Assert.Contains("shoeTrustMeta.Basis = SupplierTabBasisPolicy.ShoeType(", allEndpoints);
        Assert.Contains("trustMeta.Basis = SupplierTabBasisPolicy.Color(", allEndpoints);
        Assert.Contains("meta.Basis = SupplierTabBasisPolicy.Scorecard(", hub);
        Assert.Contains("Basis = meta.Basis", allEndpoints);
        Assert.Contains("Basis = source.Basis", hub);
    }

    [Fact]
    public void FrontendCountingBasisLabelsCoverEveryBackendCode()
    {
        var labels = ReadRepoFile("Klijent/clientapp/src/utils/supplierTabBasisLabels.ts");
        var codes = typeof(SupplierTabBasisPolicy)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Where(field => !field.Name.EndsWith("Tab", StringComparison.Ordinal) && field.Name != nameof(SupplierTabBasisPolicy.Version))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.Contains(code + ":", labels));
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not find repository root.");
        }

        return File.ReadAllText(Path.Combine(directory.FullName, relativePath)).ReplaceLineEndings("\n");
    }
}
