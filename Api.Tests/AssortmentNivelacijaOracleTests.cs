using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Api.Models;
using Npgsql;
using NpgsqlTypes;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

public sealed class AssortmentNivelacijaOracleTests : IClassFixture<PostgresContainerFixture>
{
    private const string GoldenFile = "assortment-nivelacija-oracle.json";
    private static readonly DateOnly PureAnchor = new(2026, 10, 1);

    private readonly PostgresContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AssortmentNivelacijaOracleTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ------------------------------------------------------------------
    // Hand-computed values: prove the oracle before comparing it with SQL.
    // ------------------------------------------------------------------

    [Fact]
    public void GoldenSourceRows_MatchHandComputedWindowsAndBaselines()
    {
        var rows = AssortmentNivelacijaOracle.SourceRows(GoldenFixture.Build(PureAnchor), AssortmentSourceOptions.View(PureAnchor))
            .ToDictionary(r => r.PriceEventId);

        // E1 (A2011): DUG and " korekcija " receipts are not retail turnover; far sale outside both windows.
        Assert.Equal(5m, rows[1].PreQty);
        Assert.Equal(500m, rows[1].PreRevenue);
        Assert.Equal(7m, rows[1].PostQty);
        Assert.Equal(560m, rows[1].PostRevenue);
        Assert.Equal(12.00m, rows[1].ChangePercentRevenueSemantic);
        Assert.Equal(2m / 30m, rows[1].CoveragePre30);

        // Non-nivelacija DnevnikPromena rows are not price events.
        Assert.False(rows.ContainsKey(2));

        // E3 (A2012): mature valid baseline without post sales -> post 0 and -100%.
        Assert.Equal(0m, rows[3].PostQty);
        Assert.Equal(0m, rows[3].PostRevenue);
        Assert.Null(rows[3].CoveragePost30);
        Assert.Equal(-100.00m, rows[3].ChangePercentRevenueSemantic);

        // E9 wins the duplicate (A2016, same day/prices) over E8.
        Assert.False(rows.ContainsKey(8));
        Assert.Equal(60.00m, rows[9].ChangePercentRevenueSemantic);

        // E11 (A2018): no pre sales -> missing window, no baseline, no percentage.
        Assert.Null(rows[11].PreRevenue);
        Assert.Equal("missing_pre_revenue_window", rows[11].RevenueBaselineReason);
        Assert.False(rows[11].HasRevenueBaseline);
        Assert.Null(rows[11].ChangePercentRevenueSemantic);

        // E12 (A2019): a sale and its return net to a zero baseline -> no fake +100%.
        Assert.Equal(0m, rows[12].PreRevenue);
        Assert.Equal("no_pre_revenue_baseline_uplift", rows[12].RevenueBaselineReason);
        Assert.Null(rows[12].ChangePercentRevenueSemantic);

        // View collapses the store-1 and store-2 rows of A2021 into the higher id.
        Assert.False(rows.ContainsKey(13));
        Assert.Equal(300m, rows[14].PreRevenue);
        Assert.Equal(420m, rows[14].PostRevenue);

        // E17 (A2032): immature and no post sales -> post stays unknown, not zero.
        Assert.Null(rows[17].PostQty);
        Assert.Null(rows[17].PostRevenue);
        Assert.Null(rows[17].ChangeRevenue);
    }

    [Fact]
    public void GoldenAggregate_MatchesHandComputedPopulationAndVendorStates()
    {
        var aggregate = Aggregate(GoldenFixture.Build(PureAnchor), AssortmentSourceOptions.View(PureAnchor));

        // Cohort: latest event per article; E4 (earlier A2013 event) and the unchanged-price E7 drop out.
        Assert.DoesNotContain(4L, aggregate.Rows.Select(r => r.PriceEventId));
        Assert.DoesNotContain(7L, aggregate.Rows.Select(r => r.PriceEventId));
        Assert.Contains(5L, aggregate.Rows.Select(r => r.PriceEventId));

        // Totals use only mature comparable rows: A2011/2012/2013/2014/2016, A2021/2022, A2041/2042.
        var totals = aggregate.Totals;
        Assert.Equal(9, totals.ComparableRows);
        Assert.Equal(22m, totals.PreQty);
        Assert.Equal(2250m, totals.PreRevenue);
        Assert.Equal(28m, totals.PostQty);
        Assert.Equal(2318m, totals.PostRevenue);
        Assert.Equal(68m, totals.ChangeRevenue);
        Assert.Equal(3.02m, totals.SemanticChangePercentRevenue);
        Assert.Equal(3, totals.VendorsCount);
        Assert.Equal(9, totals.ArticlesCount);

        var vendors = aggregate.Vendors.ToDictionary(v => (HasId: v.VendorId.HasValue, Id: v.VendorId.GetValueOrDefault()));

        // Alfa is partially comparable: an immature row (A2017), a missing-window row (A2018)
        // and a netted zero baseline (A2019) stay out of Pre/Post/Change.
        var alfa = vendors[(true, 201)];
        Assert.Equal(1590m, alfa.PreRevenue);
        Assert.Equal(1518m, alfa.PostRevenue);
        Assert.Equal(-72m, alfa.ChangeRevenue);
        Assert.Equal(-4.53m, alfa.SemanticChangePercentRevenue);
        Assert.Equal(5, alfa.MatureComparableRows);
        Assert.Equal(1, alfa.ImmatureComparableRows);
        Assert.Equal(1, alfa.IncreasedPriceArticles);
        Assert.Equal(4, alfa.DecreasedPriceArticles);
        Assert.Equal("neutral", alfa.EffectStatus);

        Assert.Equal(30.43m, vendors[(true, 202)].SemanticChangePercentRevenue);
        Assert.Equal("effective", vendors[(true, 202)].EffectStatus);

        // Gama has only an immature comparable row: the state is explicit, not "insufficient".
        Assert.Equal(0, vendors[(true, 203)].MatureComparableRows);
        Assert.Equal(1, vendors[(true, 203)].ImmatureComparableRows);
        Assert.Equal("immature", vendors[(true, 203)].EffectStatus);
        Assert.Null(vendors[(true, 203)].SemanticChangePercentRevenue);

        // Unresolved vendor 999 and a missing vendor share one unknown bucket.
        var unknown = vendors[(false, 0)];
        Assert.Equal("Nepoznato", unknown.VendorName);
        Assert.Equal(2, unknown.MatureComparableRows);
        Assert.Equal("insufficient_data", unknown.EffectStatus);
    }

    [Fact]
    public void GoldenAggregate_ChangeEqualsPostMinusPreAndTotalsEqualVendorSums()
    {
        foreach (var options in new[]
                 {
                     AssortmentSourceOptions.View(PureAnchor),
                     AssortmentSourceOptions.Scoped(PureAnchor, storeId: 1)
                 })
        {
            var aggregate = Aggregate(GoldenFixture.Build(PureAnchor), options);

            Assert.Equal(aggregate.Totals.PostRevenue - aggregate.Totals.PreRevenue, aggregate.Totals.ChangeRevenue);
            Assert.Equal(aggregate.Totals.PostQty - aggregate.Totals.PreQty, aggregate.Totals.ChangeQty);
            Assert.All(aggregate.Vendors, v =>
            {
                Assert.Equal(v.PostRevenue - v.PreRevenue, v.ChangeRevenue);
                Assert.Equal(v.PostQty - v.PreQty, v.ChangeQty);
            });
            Assert.Equal(aggregate.Totals.PreRevenue, aggregate.Vendors.Sum(v => v.PreRevenue));
            Assert.Equal(aggregate.Totals.PostRevenue, aggregate.Vendors.Sum(v => v.PostRevenue));
            Assert.Equal(aggregate.Totals.ComparableRows, aggregate.Vendors.Sum(v => v.MatureComparableRows));
        }
    }

    [Fact]
    public void StoreScopedAggregate_IncludesChainEventsAndExcludesOtherStoreSales()
    {
        var aggregate = Aggregate(GoldenFixture.Build(PureAnchor), AssortmentSourceOptions.Scoped(PureAnchor, storeId: 1));

        Assert.Contains(15L, aggregate.CohortEventIds);
        var beta = aggregate.Vendors.Single(v => v.VendorId == 202);
        Assert.Equal(360m, beta.PreRevenue);
        Assert.Equal(460m, beta.PostRevenue);
        Assert.Equal(27.78m, beta.SemanticChangePercentRevenue);
        Assert.Equal(2150m, aggregate.Totals.PreRevenue);
        Assert.Equal(2178m, aggregate.Totals.PostRevenue);
    }

    [Fact]
    public void GoldenAggregate_MatchesCheckedInSnapshot()
    {
        var fixture = GoldenFixture.Build(PureAnchor);
        var projection = new
        {
            ChainWide = Project(Aggregate(fixture, AssortmentSourceOptions.View(PureAnchor)), PureAnchor),
            Store1 = Project(Aggregate(fixture, AssortmentSourceOptions.Scoped(PureAnchor, storeId: 1)), PureAnchor)
        };

        if (string.Equals(Environment.GetEnvironmentVariable("TRENDPLUS_UPDATE_GOLDEN"), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(
                Path.Combine(FindRepoRoot(), "Api.Tests", "Golden", GoldenFile),
                JsonSerializer.Serialize(projection, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }

        Trendplus2.Tests.GoldenSnapshotAssert.Matches(GoldenFile, projection);
    }

    // ------------------------------------------------------------------
    // Real PostgreSQL: startup view 014 and the bounded scoped source SQL.
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartupView_MatchesOracleRowForRow()
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_view");
        if (db is null)
        {
            return;
        }

        var fixture = GoldenFixture.Build(db.Anchor);
        await SeedAsync(db.Connection, fixture);

        AssertRowsEqual(
            "vw_vendor_sales_nivelacija",
            AssortmentNivelacijaOracle.SourceRows(fixture, AssortmentSourceOptions.View(db.Anchor)),
            await ReadViewRowsAsync(db.Connection));
    }

    [Fact]
    public async Task StartupView_SeparatesMeasuredZeroFromMissingRevenueBaseline()
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_nullability");
        if (db is null)
        {
            return;
        }

        await SeedAsync(db.Connection, GoldenFixture.Build(db.Anchor));
        var rows = (await ReadViewRowsAsync(db.Connection)).ToDictionary(row => row.PriceEventId);

        var validBaseline = rows[1];
        Assert.True(validBaseline.HasRevenueBaseline);
        Assert.Equal(12.00m, validBaseline.ChangePercentRevenueSemantic);

        var measuredZeroPostWindow = rows[3];
        Assert.True(measuredZeroPostWindow.HasRevenueBaseline);
        Assert.Equal(0m, measuredZeroPostWindow.PostRevenue);
        Assert.Equal(-100.00m, measuredZeroPostWindow.ChangePercentRevenueSemantic);

        var missingBaseline = rows[11];
        Assert.False(missingBaseline.HasRevenueBaseline);
        Assert.Null(missingBaseline.ChangePercentRevenueSemantic);

        var immatureWithoutPostEvidence = rows[17];
        Assert.Null(immatureWithoutPostEvidence.PostRevenue);
        Assert.Null(immatureWithoutPostEvidence.ChangePercentRevenueSemantic);
    }

    [Fact]
    public async Task SemanticRevenueContract_IsDetectablyMissingWhenViewOrColumnIsMissing()
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_missing_contract");
        if (db is null)
        {
            return;
        }

        Assert.True(await HasSemanticRevenueColumnAsync(db.Connection));

        await ExecuteAsync(db.Connection, "DROP VIEW vw_vendor_sales_nivelacija CASCADE;");
        Assert.False(await HasSemanticRevenueColumnAsync(db.Connection));

        await ExecuteAsync(
            db.Connection,
            """
            CREATE VIEW vw_vendor_sales_nivelacija AS
            SELECT 1::bigint AS price_event_id;
            """);
        Assert.False(await HasSemanticRevenueColumnAsync(db.Connection));
    }

    [Fact]
    public async Task EventSemantics_ExposeDirectionOverlapSameDayCountAndPostMaturity()
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_event_semantics");
        if (db is null)
        {
            return;
        }

        var fixture = new AssortmentFixture();
        fixture.Vendors.Add(new AssortmentVendor(901, "Semantics"));
        fixture.Articles.Add(new AssortmentArticle(9011, 901, "Patike", "SKU-9011"));
        fixture.Articles.Add(new AssortmentArticle(9012, 901, "Patike", "SKU-9012"));
        fixture.Events.Add(new AssortmentEvent(1, 9011, db.Anchor.AddDays(-40), 100m, 80m, 1));
        fixture.Events.Add(new AssortmentEvent(2, 9011, db.Anchor.AddDays(-30), 80m, 90m, 1));
        fixture.Events.Add(new AssortmentEvent(3, 9011, db.Anchor.AddDays(-5), 90m, 90m, 1));
        fixture.Events.Add(new AssortmentEvent(4, 9012, db.Anchor.AddDays(-5), 50m, 40m, 1));
        fixture.Events.Add(new AssortmentEvent(5, 9012, db.Anchor.AddDays(-5), 40m, 50m, 1));
        await SeedAsync(db.Connection, fixture);

        var semantics = new Dictionary<long, (string? Direction, decimal? Depth, bool Complete, bool Overlaps, DateOnly? Next, int SameDayCount)>();
        await using (var command = new NpgsqlCommand(
            "SELECT price_event_id, price_direction, discount_depth_pct, post_window_complete, overlaps_next_event, next_event_date, same_day_event_count FROM vw_vendor_sales_nivelacija ORDER BY price_event_id;",
            db.Connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var nextOrdinal = reader.GetOrdinal("next_event_date");
                semantics.Add(
                    reader.GetInt64(0),
                    (
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                        reader.GetBoolean(3),
                        reader.GetBoolean(4),
                        reader.IsDBNull(nextOrdinal) ? null : DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(nextOrdinal)),
                        reader.GetInt32(6)));
            }
        }

        Assert.Equal(("markdown", 20m, true, true, db.Anchor.AddDays(-30), 1), semantics[1]);
        Assert.Equal(("markup", -12.5m, true, true, db.Anchor.AddDays(-5), 1), semantics[2]);
        Assert.Equal(("flat", 0m, false, false, null, 1), semantics[3]);
        Assert.Equal(("markdown", 20m, false, true, db.Anchor.AddDays(-5), 2), semantics[4]);
        Assert.Equal(("markup", -25m, false, false, null, 2), semantics[5]);
    }

    [Fact]
    public async Task StartupView_ReappliesOverLegacyPlainNumericPostRevenue()
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_legacy_type");
        if (db is null)
        {
            return;
        }

        await ExecuteAsync(
            db.Connection,
            """
            DROP VIEW vw_vendor_sales_nivelacija;
            DROP VIEW vw_sales_post_nivelacija;
            CREATE VIEW vw_sales_post_nivelacija AS
            SELECT price_event_id, event_date, vendor_id, vendor_name, article_id, sku,
                   article_name, category, old_price, new_price,
                   NULL::numeric AS post_qty,
                   NULL::numeric AS post_revenue,
                   NULL::numeric AS coverage_post30,
                   0::bigint AS valid_days_post30
              FROM vw_sales_pre_nivelacija;
            """);

        await ExecuteAsync(db.Connection, ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql"));

        Assert.Equal(
            "numeric(18,2)",
            await ScalarAsync<string>(
                db.Connection,
                """
                SELECT format_type(a.atttypid, a.atttypmod)
                  FROM pg_attribute a
                 WHERE a.attrelid = 'vw_sales_post_nivelacija'::regclass
                   AND a.attname = 'post_revenue';
                """));

        var fixture = GoldenFixture.Build(db.Anchor);
        await SeedAsync(db.Connection, fixture);
        AssertRowsEqual(
            "vw_vendor_sales_nivelacija(after legacy type)",
            AssortmentNivelacijaOracle.SourceRows(fixture, AssortmentSourceOptions.View(db.Anchor)),
            await ReadViewRowsAsync(db.Connection));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task ScopedSource_MatchesOracleRowForRow(int? storeId)
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_scoped");
        if (db is null)
        {
            return;
        }

        var fixture = GoldenFixture.Build(db.Anchor);
        await SeedAsync(db.Connection, fixture);

        AssertRowsEqual(
            $"scoped_vendor_sales_nivelacija(storeId={storeId?.ToString(CultureInfo.InvariantCulture) ?? "null"})",
            AssortmentNivelacijaOracle.SourceRows(fixture, AssortmentSourceOptions.Scoped(db.Anchor, storeId)),
            await ReadScopedRowsAsync(db.Connection, storeId));
    }

    [Fact]
    public async Task ScopedSource_KeepsNullStoreDistinctFromRealNegativeOneStore()
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_negative_store_sentinel");
        if (db is null)
        {
            return;
        }

        const int articleId = 9001;
        var fixture = new AssortmentFixture();
        fixture.Vendors.Add(new AssortmentVendor(-1, "Dobavljač -1"));
        fixture.Articles.Add(new AssortmentArticle(articleId, -1, "Patike", "SKU-9001"));
        fixture.Events.Add(new AssortmentEvent(1, articleId, db.Anchor.AddDays(-50), 100m, 80m, -1));
        fixture.Events.Add(new AssortmentEvent(2, articleId, db.Anchor.AddDays(-50), 100m, 80m, null));
        fixture.Events.Add(new AssortmentEvent(3, articleId, db.Anchor.AddDays(-50), 100m, 80m, -2));
        fixture.Sales.Add(new AssortmentSale(1, "R-PRE", db.Anchor.AddDays(-60), articleId, 2, 100m, -1));
        fixture.Sales.Add(new AssortmentSale(2, "R-POST", db.Anchor.AddDays(-40), articleId, 3, 80m, -1));
        await SeedAsync(db.Connection, fixture);

        var expected = AssortmentNivelacijaOracle.SourceRows(
            fixture,
            AssortmentSourceOptions.Scoped(db.Anchor, storeId: null));
        var actual = await ReadScopedRowsAsync(db.Connection, storeId: null);

        Assert.Equal(new long[] { 1, 2, 3 }, expected.Select(row => row.PriceEventId).Order());
        Assert.Equal(new long[] { 1, 2, 3 }, actual.Select(row => row.PriceEventId).Order());
        AssertRowsEqual("scoped source with null and real -1 store", expected, actual);
        Assert.Contains(actual, row => row.VendorId == -1);

        var negativeStoreOnly = await ReadScopedRowsAsync(db.Connection, storeId: -1);
        Assert.Equal(new long[] { 1, 2 }, negativeStoreOnly.Select(row => row.PriceEventId).Order());

        var negativeVendor = actual.First(row => row.VendorId == -1);
        var missingVendor = negativeVendor with { PriceEventId = 3, VendorId = null, VendorName = null };
        Assert.Equal(2, EndpointDedup([negativeVendor, missingVendor]).Count);
    }

    [Fact]
    public async Task BoundedScopedSource_IsValueEquivalentToUnboundedViewAfterEndpointDedup()
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_bounded");
        if (db is null)
        {
            return;
        }

        var fixture = GoldenFixture.Build(db.Anchor);
        await SeedAsync(db.Connection, fixture);

        var view = await ReadViewRowsAsync(db.Connection);
        var scoped = await ReadScopedRowsAsync(db.Connection, storeId: null);

        // The bounded source keeps one event per store (E13 and E14); the endpoint dedup
        // collapses them exactly like the view, so every consumed value is identical.
        Assert.Contains(scoped, r => r.PriceEventId == 13);
        Assert.DoesNotContain(view, r => r.PriceEventId == 13);
        AssertRowsEqual("endpoint-dedup(view vs bounded)", EndpointDedup(view), EndpointDedup(scoped));

        var fromView = AssortmentNivelacijaOracle.Aggregate(view, db.Anchor);
        var fromScoped = AssortmentNivelacijaOracle.Aggregate(scoped, db.Anchor);
        var fromOracle = Aggregate(fixture, AssortmentSourceOptions.View(db.Anchor));
        Assert.Equal(Serialize(fromOracle, db.Anchor), Serialize(fromView, db.Anchor));
        Assert.Equal(Serialize(fromOracle, db.Anchor), Serialize(fromScoped, db.Anchor));
    }

    [Fact]
    public async Task LegacyPercentColumns_DivergeOnlyForImmatureRowsWithoutPostSales()
    {
        await using var db = await TryCreateDatabaseAsync("tp_assortment_legacy_pct");
        if (db is null)
        {
            return;
        }

        await SeedAsync(db.Connection, GoldenFixture.Build(db.Anchor));
        var view = await ReadLegacyPercentAsync(db.Connection, ViewSql("price_event_id, change_percent_revenue"));
        var scoped = await ReadLegacyPercentAsync(db.Connection, ScopedSql("price_event_id, change_percent_revenue"));

        // Not consumed by the Assortment endpoint (it reads the *_semantic columns); pinned so
        // a consumer cannot start reading them without seeing the difference.
        var differing = view.Keys.Intersect(scoped.Keys).Where(id => view[id] != scoped[id]).ToArray();
        Assert.Equal(new[] { 17L }, differing);
        Assert.Equal(-100.00m, view[17]);
        Assert.Null(scoped[17]);
    }

    [Fact]
    public async Task DidControlAndOptionalMappers_ArePinnedAgainstRealPostgresFixture()
    {
        await using var db = await TryCreateDatabaseAsync("tp_nivelacija_did_oracle");
        if (db is null)
        {
            return;
        }

        var fixture = new AssortmentFixture();
        fixture.Vendors.Add(new AssortmentVendor(301, "Oracle vendor"));
        fixture.Articles.Add(new AssortmentArticle(3011, 301, "Patike", "SHARED-PLU"));
        fixture.Articles.Add(new AssortmentArticle(3012, 301, "Patike", "SHARED-PLU"));
        fixture.Articles.Add(new AssortmentArticle(3021, 301, "Patike", "CONTROL-NO-STOCK", Stock: 0m));
        fixture.Articles.Add(new AssortmentArticle(3022, 301, "Patike", "CONTROL-STOCK"));
        fixture.Articles.Add(new AssortmentArticle(3031, 301, "Patike", "POST-BACKFILL-MARKDOWN"));
        fixture.Events.Add(new AssortmentEvent(301, 3011, db.Anchor.AddDays(-40), 100m, 80m, 1));
        fixture.Events.Add(new AssortmentEvent(302, 3011, db.Anchor.AddDays(-20), 80m, 90m, 1)); // markup, immature
        fixture.Events.Add(new AssortmentEvent(303, 3031, db.Anchor.AddDays(-15), 120m, 90m, 1)); // persisted after the backfill
        fixture.Sales.Add(new AssortmentSale(301, "T-PRE", db.Anchor.AddDays(-50), 3011, 10, 100m, 1));
        fixture.Sales.Add(new AssortmentSale(302, "T-POST-1", db.Anchor.AddDays(-35), 3011, 4, 80m, 1));
        fixture.Sales.Add(new AssortmentSale(303, "T-PRE-2", db.Anchor.AddDays(-25), 3011, 4, 80m, 1));
        fixture.Sales.Add(new AssortmentSale(304, "T-POST-2", db.Anchor.AddDays(-10), 3011, 2, 90m, 1));
        fixture.Sales.Add(new AssortmentSale(305, "CONTROL-PRE", db.Anchor.AddDays(-50), 3022, 10, 95m, 1));
        fixture.Sales.Add(new AssortmentSale(306, "CONTROL-POST", db.Anchor.AddDays(-35), 3022, 7, 95m, 1));
        fixture.Sales.Add(new AssortmentSale(307, "SECOND-SIZE", db.Anchor.AddDays(-35), 3012, 20, 80m, 1));
        fixture.Sales.Add(new AssortmentSale(308, "POST-BACKFILL-CONTROL-TRAP", db.Anchor.AddDays(-50), 3031, 10, 100m, 1));
        await SeedAsync(db.Connection, fixture);
        await SeedDidMapperViewsAsync(db.Connection);
        await ExecuteAsync(db.Connection, ReadRepoFile("Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql"));
        await ExecuteAsync(db.Connection, "CREATE VIEW supplier_did_dependency AS SELECT price_event_id, did_revenue FROM vw_nivelacija_did;");
        await ExecuteAsync(db.Connection, ReadRepoFile("Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql"));

        var controls = await ReadStringSetAsync(db.Connection, "SELECT article_id::text FROM vw_nivelacija_kontrolna_grupa ORDER BY article_id;");
        Assert.Equal(new[] { "3011", "3012", "3022", "3031" }, controls);

        var did = await ReadDidRowsAsync(db.Connection);
        Assert.Equal(new long[] { 301, 302, 303 }, did.Select(x => x.EventId).Order());
        Assert.True(await ScalarAsync<bool>(db.Connection, "SELECT to_regclass('supplier_did_dependency') IS NOT NULL;"));
        Assert.All(did, row => Assert.NotEqual(row.ArticleId, row.ControlArticleId));
        Assert.Equal(3022, did.Single(row => row.EventId == 301).ControlArticleId);
        var firstEvent = did.Single(row => row.EventId == 301);
        Assert.Equal(10m, firstEvent.PreQty);
        Assert.Equal(1000m, firstEvent.PreRevenue);
        Assert.Equal(8m, firstEvent.PostQty);
        Assert.Equal(640m, firstEvent.PostRevenue);
        Assert.Equal(10m, firstEvent.ControlPreQty);
        Assert.Equal(950m, firstEvent.ControlPreRevenue);
        Assert.Equal(7m, firstEvent.ControlPostQty);
        Assert.Equal(665m, firstEvent.ControlPostRevenue);
        Assert.Equal(-75m, did.Single(row => row.EventId == 301).DidRevenue);
        Assert.Equal(1m, did.Single(row => row.EventId == 301).DidQty);
        var secondEvent = did.Single(row => row.EventId == 302);
        Assert.Equal(3022, secondEvent.ControlArticleId);
        Assert.Equal(18m, secondEvent.PreQty);
        Assert.Equal(1640m, secondEvent.PreRevenue);
        Assert.Equal(2m, secondEvent.PostQty);
        Assert.Equal(180m, secondEvent.PostRevenue);
        Assert.Equal(17m, secondEvent.ControlPreQty);
        Assert.Equal(1615m, secondEvent.ControlPreRevenue);
        Assert.Equal(0m, secondEvent.ControlPostQty);
        Assert.Equal(0m, secondEvent.ControlPostRevenue);
        Assert.Null(secondEvent.DidRevenue);
        Assert.Null(secondEvent.DidQty);
        var postBackfillEvent = did.Single(row => row.EventId == 303);
        Assert.Equal(3022, postBackfillEvent.ControlArticleId);
        Assert.Null(postBackfillEvent.DidRevenue);
        Assert.Null(postBackfillEvent.DidQty);

        var explain = await ReadExplainAsync(db.Connection);
        var planEvidence = explain.Split(Environment.NewLine)
            .Where(line => line.Contains("Index Scan using idx_mv_daily_sales_facts_pk", StringComparison.Ordinal)
                || line.Contains("Planning Time:", StringComparison.Ordinal)
                || line.Contains("Execution Time:", StringComparison.Ordinal));
        _output.WriteLine("DiD bounded query plan evidence: {0}", string.Join(" | ", planEvidence));
        Assert.Contains("Execution Time:", explain, StringComparison.Ordinal);

        await SeedOptionalMapperViewsAsync(db.Connection);

        var articles = new List<VendorSalesNivelacijaArticleStatDto>
        {
            new() { ArticleId = 3011, PriceEventId = 301, EventDate = db.Anchor.AddDays(-40).ToDateTime(TimeOnly.MinValue), Sku = "SHARED-PLU", OldPrice = 100m, NewPrice = 80m, PriceChangePercent = -20m, PreQty = 10, PostQty = 4, PostRevenue = 320m, HasComparableSalesWindow = true, IsPostWindowMature = true, HasPostSalesEvidence = true },
            new() { ArticleId = 3012, PriceEventId = 0, Sku = "SHARED-PLU", PriceChangePercent = 0m, PreQty = 20, PostQty = 20, PostRevenue = 1600m },
            new() { ArticleId = 3011, PriceEventId = 302, EventDate = db.Anchor.AddDays(-20).ToDateTime(TimeOnly.MinValue), Sku = "SHARED-PLU", OldPrice = 80m, NewPrice = 90m, PriceChangePercent = 12.5m, PreQty = 4, PostQty = 2, PostRevenue = 180m, HasComparableSalesWindow = true, IsPostWindowMature = false }
        };

        var rollingWarning = await InvokeOptionalMapperAsync("MapRollingAndMomentumToNivelacijaArticlesAsync", articles, db.Connection, db.Anchor.AddDays(-40).ToDateTime(TimeOnly.MinValue));
        var didWarning = await InvokeOptionalMapperAsync("MapOosAndDidToNivelacijaArticlesAsync", articles, db.Connection);
        InvokeElasticityMapper(articles);

        Assert.Null(rollingWarning);
        Assert.Contains("OOS unavailable: observed snapshot history is not certified for event windows", didWarning, StringComparison.Ordinal);
        Assert.Equal(100m, articles[0].Rolling7dPreRevenue);
        Assert.Equal(200m, articles[0].Rolling7dPostRevenue);
        Assert.NotEqual(articles[0].Rolling7dPreRevenue, articles[1].Rolling7dPreRevenue);
        Assert.Null(articles[0].MomentumRevenue); // no complete event-relative 7-day post sample
        Assert.Null(articles[0].OOSRate);
        Assert.Null(articles[1].OOSRate);
        Assert.Equal(-75m, articles[0].DidRevenue);
        Assert.Null(articles[1].DidRevenue);
        Assert.Null(articles[0].LostSalesOOS);
        Assert.Equal(3m, articles[0].PriceElasticity);
        Assert.Null(articles[1].PriceElasticity);
        Assert.Null(articles[2].PriceElasticity);

        var scopedMetricRow = new VendorSalesNivelacijaArticleStatDto
        {
            ArticleId = 3011,
            PriceEventId = 301,
            EventDate = db.Anchor.AddDays(-40).ToDateTime(TimeOnly.MinValue),
            Sku = "SHARED-PLU"
        };
        var scopedRollingWarning = await InvokeOptionalMapperAsync(
            "MapRollingAndMomentumToNivelacijaArticlesAsync",
            [scopedMetricRow],
            db.Connection,
            db.Anchor.AddDays(-40).ToDateTime(TimeOnly.MinValue),
            metricsCanUseUnscopedSources: false);
        var scopedDidWarning = await InvokeOptionalMapperAsync(
            "MapOosAndDidToNivelacijaArticlesAsync",
            [scopedMetricRow],
            db.Connection,
            metricsCanUseUnscopedSources: false);
        Assert.Equal("scope_not_applied: rolling_and_momentum", scopedRollingWarning);
        Assert.Equal("scope_not_applied: did_and_observed_oos", scopedDidWarning);
        Assert.Null(scopedMetricRow.Rolling7dPreRevenue);
        Assert.Null(scopedMetricRow.DidRevenue);
        Assert.Null(scopedMetricRow.OOSRate);
    }

    [Fact]
    public void EndpointVendorEffectReceivesComparableIncludingImmatureArticles()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("ComparableArticleCount: row.ComparableCount,", source);
        Assert.DoesNotContain("ComparableArticleCount: row.Vendor.ComparableArticleCount,", source);
        Assert.Contains("var totalChangeRevenue = totalPostRevenue - totalPreRevenue;", source);
        Assert.Contains("var changeRevenue = postRev - preRev;", source);
    }

    [Fact]
    public void EndpointChangePercentIsUnknownWithoutMatureComparableEvidence()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("ComputeCohortChangePercent(\n                        matureComparableRows.Count, totalPreRevenue, totalPostRevenue)", source);
        Assert.Contains("ComputeCohortChangePercent(\n                            matureComparable.Count, preRev, postRev)", source);
        Assert.Contains("ChangePercent = semanticChangePercent,", source);
        Assert.DoesNotContain("ChangePercent = semanticChangePercent ?? 0m", source);
        Assert.DoesNotContain("SemanticChangePercent(totalPreRevenue, totalPostRevenue) ?? 0m", source);
    }

    // ------------------------------------------------------------------
    // Golden fixture (day offsets relative to the anchor)
    // ------------------------------------------------------------------

    private static class GoldenFixture
    {
        public static AssortmentFixture Build(DateOnly anchor)
        {
            var f = new AssortmentFixture();
            var receipt = 0;

            void Sale(int article, int day, int qty, decimal price, int? store = 1, string? receiptNumber = null)
            {
                receipt++;
                f.Sales.Add(new AssortmentSale(receipt, receiptNumber ?? $"R-{receipt}", anchor.AddDays(day), article, qty, price, store));
            }

            void Event(long id, int article, int day, decimal oldPrice, decimal newPrice, int? store = 1, string type = "Nivelacija") =>
                f.Events.Add(new AssortmentEvent(id, article, anchor.AddDays(day), oldPrice, newPrice, store, type));

            f.Vendors.Add(new AssortmentVendor(201, "Alfa"));
            f.Vendors.Add(new AssortmentVendor(202, "Beta"));
            f.Vendors.Add(new AssortmentVendor(203, "Gama"));

            foreach (var id in new[] { 2011, 2012, 2013, 2014, 2015, 2016, 2017, 2018, 2019 })
            {
                f.Articles.Add(new AssortmentArticle(id, 201, id <= 2012 || id is 2015 or 2016 ? "Patike" : "Sandale", $"SKU-{id}"));
            }

            f.Articles.Add(new AssortmentArticle(2021, 202, "Cizme", "SKU-2021"));
            f.Articles.Add(new AssortmentArticle(2022, 202, "Cizme", "SKU-2022"));
            f.Articles.Add(new AssortmentArticle(2031, 203, "Patike", "SKU-2031"));
            f.Articles.Add(new AssortmentArticle(2032, 203, "Patike", "SKU-2032"));
            f.Articles.Add(new AssortmentArticle(2041, 999, "Patike", "SKU-2041"));
            f.Articles.Add(new AssortmentArticle(2042, null, "Patike", "SKU-2042"));

            // A2011: mature markdown; DUG/KOREKCIJA receipts and a far sale must not count.
            Event(1, 2011, -60, 100m, 80m);
            Event(2, 2011, -58, 100m, 100m, type: "Ulaz");
            Sale(2011, -200, 1, 100m);
            Sale(2011, -80, 2, 100m);
            Sale(2011, -70, 3, 100m);
            Sale(2011, -65, 5, 100m, receiptNumber: "DUG");
            Sale(2011, -55, 4, 80m);
            Sale(2011, -45, 2, 80m, receiptNumber: " korekcija ");
            Sale(2011, -40, 3, 80m);

            // A2012: mature, valid baseline, no post sales.
            Event(3, 2012, -50, 120m, 90m);
            Sale(2012, -75, 1, 120m);
            Sale(2012, -60, 2, 120m);

            // A2013: repeated events with overlapping windows; the latest (E5) is the cohort row.
            Event(4, 2013, -90, 150m, 130m);
            Event(5, 2013, -45, 130m, 110m);
            Sale(2013, -100, 2, 150m);
            Sale(2013, -80, 1, 130m);
            Sale(2013, -50, 2, 130m);
            Sale(2013, -40, 3, 110m);
            Sale(2013, -20, 1, 110m);

            // A2014: price increase stays in the latest-event cohort.
            Event(6, 2014, -40, 90m, 99m);
            Sale(2014, -60, 2, 90m);
            Sale(2014, -50, 1, 90m);
            Sale(2014, -30, 2, 99m);

            // A2015: unchanged price is not a price-change effect.
            Event(7, 2015, -45, 100m, 100m);
            Sale(2015, -50, 1, 100m);
            Sale(2015, -40, 1, 100m);

            // A2016: duplicate source rows for one event.
            Event(8, 2016, -55, 200m, 160m);
            Event(9, 2016, -55, 200m, 160m);
            Sale(2016, -60, 1, 200m);
            Sale(2016, -50, 2, 160m);

            // A2017: comparable but immature.
            Event(10, 2017, -10, 80m, 60m);
            Sale(2017, -20, 2, 80m);
            Sale(2017, -5, 1, 60m);

            // A2018: no pre window.
            Event(11, 2018, -50, 70m, 50m);
            Sale(2018, -40, 2, 50m);

            // A2019: sale and return net to a zero baseline.
            Event(12, 2019, -50, 60m, 45m);
            Sale(2019, -70, 1, 60m);
            Sale(2019, -60, -1, 60m);
            Sale(2019, -45, 2, 45m);

            // A2021: one markdown recorded for store 1 and store 2; sales in both stores.
            Event(13, 2021, -50, 100m, 70m, store: 1);
            Event(14, 2021, -50, 100m, 70m, store: 2);
            Sale(2021, -70, 1, 100m, store: 2);
            Sale(2021, -60, 2, 100m, store: 1);
            Sale(2021, -40, 4, 70m, store: 1);
            Sale(2021, -30, 2, 70m, store: 2);

            // A2022: chain-wide markdown (no store).
            Event(15, 2022, -50, 80m, 60m, store: null);
            Sale(2022, -55, 2, 80m);
            Sale(2022, -45, 3, 60m);

            // Gama: only immature evidence.
            Event(16, 2031, -15, 100m, 70m);
            Sale(2031, -30, 3, 100m);
            Sale(2031, -10, 2, 70m);
            Event(17, 2032, -5, 50m, 40m);
            Sale(2032, -20, 2, 50m);

            // Unresolved vendor id and missing vendor collapse into one unknown bucket.
            Event(18, 2041, -50, 100m, 80m);
            Sale(2041, -60, 1, 100m);
            Sale(2041, -40, 1, 80m);
            Event(19, 2042, -50, 50m, 40m);
            Sale(2042, -60, 2, 50m);
            Sale(2042, -40, 3, 40m);

            return f;
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static AssortmentAggregate Aggregate(AssortmentFixture fixture, AssortmentSourceOptions options) =>
        AssortmentNivelacijaOracle.Aggregate(AssortmentNivelacijaOracle.SourceRows(fixture, options), options.AsOf);

    private static object Project(AssortmentAggregate aggregate, DateOnly anchor) => new
    {
        aggregate.Totals,
        aggregate.Vendors,
        aggregate.CohortEventIds,
        Rows = aggregate.Rows.Select(r => new
        {
            r.PriceEventId,
            r.ArticleId,
            EventDayOffset = r.EventDate.DayNumber - anchor.DayNumber,
            r.VendorId,
            r.VendorName,
            r.PreQty,
            r.PreRevenue,
            r.PostQty,
            r.PostRevenue,
            r.IsPostWindowMature,
            r.PostWindowDaysElapsed,
            r.HasComparableSalesWindow,
            r.RevenueBaselineReason,
            r.SemanticChangePercentRevenue,
            r.PriceChangePercent
        })
    };

    private static string Serialize(AssortmentAggregate aggregate, DateOnly anchor) =>
        JsonSerializer.Serialize(Project(aggregate, anchor), ScaleInsensitiveJson);

    private static readonly JsonSerializerOptions ScaleInsensitiveJson = new() { Converters = { new NormalizedDecimalConverter() } };

    /// <summary>PostgreSQL numeric(18,2) keeps trailing zeros; compare values, not scale.</summary>
    private sealed class NormalizedDecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value / 1.0000000000000000000000000000m);
    }

    private static IReadOnlyList<AssortmentSourceRow> EndpointDedup(IEnumerable<AssortmentSourceRow> rows) =>
        rows.GroupBy(r => (r.EventDate, r.VendorId, r.ArticleId, r.OldPrice, r.NewPrice))
            .Select(g => g.MaxBy(r => r.PriceEventId)!)
            .OrderBy(r => r.PriceEventId)
            .ToList();

    private static void AssertRowsEqual(string source, IEnumerable<AssortmentSourceRow> expected, IEnumerable<AssortmentSourceRow> actual)
    {
        var expectedById = expected.Select(r => r.Normalized()).ToDictionary(r => r.PriceEventId);
        var actualById = actual.Select(r => r.Normalized()).ToDictionary(r => r.PriceEventId);
        var mismatches = new List<string>();

        foreach (var id in expectedById.Keys.Union(actualById.Keys).Order())
        {
            expectedById.TryGetValue(id, out var e);
            actualById.TryGetValue(id, out var a);
            if (e != a)
            {
                mismatches.Add($"{source} event {id}:{Environment.NewLine}  oracle: {e?.ToString() ?? "<missing>"}{Environment.NewLine}  sql:    {a?.ToString() ?? "<missing>"}");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    private sealed class AssortmentDatabase(NpgsqlConnection connection, DateOnly anchor) : IAsyncDisposable
    {
        public NpgsqlConnection Connection { get; } = connection;

        public DateOnly Anchor { get; } = anchor;

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }

    private sealed record DidRow(
        long EventId, int ArticleId, decimal? PreQty, decimal? PostQty, decimal? PreRevenue, decimal? PostRevenue,
        int ControlArticleId, decimal? ControlPreQty, decimal? ControlPostQty, decimal? ControlPreRevenue,
        decimal? ControlPostRevenue, decimal? DidRevenue, decimal? DidQty);

    private static Task SeedDidMapperViewsAsync(NpgsqlConnection connection)
    {
        return ExecuteAsync(
            connection,
            """
            CREATE TABLE price_history (
                id bigint,
                article_id integer,
                event_date date,
                old_price numeric,
                new_price numeric
            );
            INSERT INTO price_history
            SELECT "Id", "ArtikalId", "Datum"::date, "StaraProdajnaCena", "NovaProdajnaCena"
            FROM "DnevnikPromena"
            WHERE "TipPromene" IN ('Nivelacija', 'Nivelacija cena')
              AND "Id" <> 303;
            CREATE TABLE mv_daily_sales_facts (
                article_id integer NOT NULL,
                day date NOT NULL,
                units bigint NOT NULL,
                revenue numeric NOT NULL
            );
            CREATE UNIQUE INDEX idx_mv_daily_sales_facts_pk ON mv_daily_sales_facts (article_id, day);
            CREATE INDEX idx_mv_daily_sales_facts_day ON mv_daily_sales_facts (day);
            INSERT INTO mv_daily_sales_facts(article_id, day, units, revenue)
            SELECT ps.id_artikal, pz.datum_prodaje::date,
                   SUM(ps.kolicina)::bigint, SUM(ps.kolicina * ps.cena)::numeric
              FROM prodaja_stavke ps
              JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
             GROUP BY ps.id_artikal, pz.datum_prodaje::date;
            """);
    }

    private static Task SeedOptionalMapperViewsAsync(NpgsqlConnection connection) => ExecuteAsync(
        connection,
        """
        CREATE TABLE vw_sales_rolling_7d (article_id integer, day date, ma7_revenue numeric);
        INSERT INTO vw_sales_rolling_7d VALUES
            (3011, CURRENT_DATE - 50, 100),
            (3011, CURRENT_DATE - 35, 200),
            (3012, CURRENT_DATE - 50, 80),
            (3012, CURRENT_DATE - 35, 160);
        CREATE TABLE vw_sales_momentum (article_id integer, last_day date, momentum_revenue numeric);
        CREATE SCHEMA analytics_intel;
        CREATE TABLE analytics_intel.vw_inventory_daily_stock_v1 (
            article_id integer,
            store_id integer,
            date date,
            observed_qty numeric,
            provenance text
        );
        INSERT INTO analytics_intel.vw_inventory_daily_stock_v1 VALUES
            (3011, 0, CURRENT_DATE - 39, 0, 'observed'),
            (3011, 0, CURRENT_DATE - 35, 4, 'observed');
        """);

    private static async Task<List<DidRow>> ReadDidRowsAsync(NpgsqlConnection connection)
    {
        const string sql = """
            SELECT price_event_id, article_id, pre_qty, post_qty, pre_revenue, post_revenue,
                   control_article_id, control_pre_qty, control_post_qty, control_pre_revenue,
                   control_post_revenue, did_revenue, did_qty
              FROM vw_nivelacija_did
             ORDER BY price_event_id;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<DidRow>();
        while (await reader.ReadAsync())
        {
            decimal? Dec(int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
            rows.Add(new DidRow(
                reader.GetInt64(0), reader.GetInt32(1), Dec(2), Dec(3), Dec(4), Dec(5), reader.GetInt32(6),
                Dec(7), Dec(8), Dec(9), Dec(10), Dec(11), Dec(12)));
        }

        return rows;
    }

    private static async Task<string[]> ReadStringSetAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
        return rows.ToArray();
    }

    private static async Task<string> ReadExplainAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) SELECT * FROM vw_nivelacija_did WHERE event_date >= CURRENT_DATE - INTERVAL '45 days' AND event_date < CURRENT_DATE;",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync()) lines.Add(reader.GetString(0));
        return string.Join(Environment.NewLine, lines);
    }

    private static async Task<string?> InvokeOptionalMapperAsync(
        string methodName,
        List<VendorSalesNivelacijaArticleStatDto> articles,
        NpgsqlConnection connection,
        DateTime? eventDate = null,
        bool metricsCanUseUnscopedSources = true)
    {
        var method = typeof(Trendplus2.Endpoints.AllEndpoints).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Mapper {methodName} not found.");
        var arguments = methodName == "MapRollingAndMomentumToNivelacijaArticlesAsync"
            ? new object?[] { articles, connection, eventDate, metricsCanUseUnscopedSources, CancellationToken.None }
            : new object?[] { articles, connection, metricsCanUseUnscopedSources, CancellationToken.None };
        return await (Task<string?>)(method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"Mapper {methodName} returned no task."));
    }

    private static void InvokeElasticityMapper(List<VendorSalesNivelacijaArticleStatDto> articles)
    {
        var method = typeof(Trendplus2.Endpoints.AllEndpoints).GetMethod(
            "MapElasticityAndLostSalesToNivelacijaArticles", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Elasticity mapper not found.");
        method.Invoke(null, [articles]);
    }

    private async Task<AssortmentDatabase?> TryCreateDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable)
        {
            return null;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE "Dobavljaci" ("Id" integer PRIMARY KEY, "Naziv" text);
            CREATE TABLE "Artikli" (
                "Id" integer PRIMARY KEY,
                "PLU" text,
                "Naziv" text,
                "Kategorija" text,
                "IDDobavljac" integer,
                "Kolicina" numeric NOT NULL DEFAULT 10
            );
            CREATE TABLE "DnevnikPromena" (
                "Id" integer PRIMARY KEY,
                "ArtikalId" integer,
                "Datum" timestamp NOT NULL,
                "TipPromene" text NOT NULL,
                "StaraProdajnaCena" numeric(18,2),
                "NovaProdajnaCena" numeric(18,2),
                "DobavljacId" integer,
                "IDObjekat" integer,
                "DataOrigin" text,
                "BrojRacuna" text
            );
            CREATE TABLE prodaja_zaglavlje (
                id integer PRIMARY KEY,
                datum_prodaje timestamp NOT NULL,
                broj_racuna text,
                id_objekat integer,
                data_origin text
            );
            CREATE TABLE prodaja_stavke (
                id serial PRIMARY KEY,
                id_prodaja integer NOT NULL,
                id_artikal integer NOT NULL,
                kolicina integer NOT NULL,
                cena numeric(18,2) NOT NULL
            );
            """);
        await ExecuteAsync(connection, ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql"));

        var anchor = DateOnly.FromDateTime(await ScalarAsync<DateTime>(connection, "SELECT CURRENT_DATE::timestamp;"));
        return new AssortmentDatabase(connection, anchor);
    }

    private static async Task SeedAsync(NpgsqlConnection connection, AssortmentFixture fixture)
    {
        var sql = new StringBuilder();
        foreach (var v in fixture.Vendors)
        {
            sql.AppendLine($"INSERT INTO \"Dobavljaci\" VALUES ({v.Id}, {Text(v.Name)});");
        }

        foreach (var a in fixture.Articles)
        {
            sql.AppendLine($"INSERT INTO \"Artikli\" VALUES ({a.Id}, {Text(a.Sku)}, {Text($"Artikal {a.Id}")}, {Text(a.Category)}, {Num(a.VendorId)}, {Num(a.Stock)});");
        }

        foreach (var e in fixture.Events)
        {
            sql.AppendLine(
                "INSERT INTO \"DnevnikPromena\" (\"Id\", \"ArtikalId\", \"Datum\", \"TipPromene\", \"StaraProdajnaCena\", \"NovaProdajnaCena\", \"DobavljacId\", \"IDObjekat\") "
                + $"VALUES ({e.Id}, {e.ArticleId}, {Timestamp(e.Day)}, {Text(e.TipPromene)}, {Num(e.OldPrice)}, {Num(e.NewPrice)}, {Num(e.EventVendorId)}, {Num(e.StoreId)});");
        }

        foreach (var s in fixture.Sales)
        {
            sql.AppendLine($"INSERT INTO prodaja_zaglavlje VALUES ({s.ReceiptId}, {Timestamp(s.Day)}, {Text(s.ReceiptNumber)}, {Num(s.StoreId)}, NULL);");
            sql.AppendLine($"INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena) VALUES ({s.ReceiptId}, {s.ArticleId}, {s.Qty}, {Num(s.Price)});");
        }

        await ExecuteAsync(connection, sql.ToString());
    }

    private const string ConsumedColumns = """
        price_event_id, event_date, vendor_id, vendor_name, article_id, sku, category, old_price, new_price,
        pre_qty, pre_revenue, post_qty, post_revenue, coverage_pre30, coverage_post30, change_qty, change_revenue,
        has_qty_baseline, qty_baseline_reason, change_percent_qty_semantic,
        has_revenue_baseline, revenue_baseline_reason, change_percent_revenue_semantic
        """;

    private static string ViewSql(string columns) => $"SELECT {columns} FROM vw_vendor_sales_nivelacija;";

    private static string ScopedSql(string columns)
    {
        var builder = typeof(Trendplus2.Endpoints.AllEndpoints).GetMethod(
            "BuildVendorSalesNivelacijaScopedSourceSql",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Scoped vendor nivelacija SQL builder not found.");
        return $"{(string)builder.Invoke(null, null)!}{Environment.NewLine}SELECT {columns} FROM scoped_vendor_sales_nivelacija;";
    }

    private static Task<List<AssortmentSourceRow>> ReadViewRowsAsync(NpgsqlConnection connection) =>
        ReadRowsAsync(connection, ViewSql(ConsumedColumns), storeId: null, scoped: false);

    private static Task<List<AssortmentSourceRow>> ReadScopedRowsAsync(NpgsqlConnection connection, int? storeId) =>
        ReadRowsAsync(connection, ScopedSql(ConsumedColumns), storeId, scoped: true);

    private static async Task<List<AssortmentSourceRow>> ReadRowsAsync(NpgsqlConnection connection, string sql, int? storeId, bool scoped)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (scoped)
        {
            AddScopedParameters(command, storeId);
        }

        var rows = new List<AssortmentSourceRow>();
        await using var reader = await command.ExecuteReaderAsync();

        decimal? Dec(string column) =>
            reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToDecimal(reader[column], CultureInfo.InvariantCulture);
        int? Int(string column) =>
            reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToInt32(reader[column], CultureInfo.InvariantCulture);
        string? Str(string column) =>
            reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));

        while (await reader.ReadAsync())
        {
            rows.Add(new AssortmentSourceRow(
                PriceEventId: Convert.ToInt64(reader["price_event_id"], CultureInfo.InvariantCulture),
                EventDate: DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(reader.GetOrdinal("event_date"))),
                VendorId: Int("vendor_id"),
                VendorName: Str("vendor_name"),
                ArticleId: Convert.ToInt32(reader["article_id"], CultureInfo.InvariantCulture),
                Sku: Str("sku"),
                Category: Str("category"),
                OldPrice: Dec("old_price"),
                NewPrice: Dec("new_price"),
                PreQty: Dec("pre_qty"),
                PreRevenue: Dec("pre_revenue"),
                PostQty: Dec("post_qty"),
                PostRevenue: Dec("post_revenue"),
                CoveragePre30: Dec("coverage_pre30"),
                CoveragePost30: Dec("coverage_post30"),
                ChangeQty: Dec("change_qty"),
                ChangeRevenue: Dec("change_revenue"),
                HasQtyBaseline: reader.GetBoolean(reader.GetOrdinal("has_qty_baseline")),
                QtyBaselineReason: Str("qty_baseline_reason"),
                ChangePercentQtySemantic: Dec("change_percent_qty_semantic"),
                HasRevenueBaseline: reader.GetBoolean(reader.GetOrdinal("has_revenue_baseline")),
                RevenueBaselineReason: Str("revenue_baseline_reason"),
                ChangePercentRevenueSemantic: Dec("change_percent_revenue_semantic")));
        }

        return rows;
    }

    private static async Task<Dictionary<long, decimal?>> ReadLegacyPercentAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (sql.Contains("scoped_vendor_sales_nivelacija", StringComparison.Ordinal))
        {
            AddScopedParameters(command, storeId: null);
        }

        var values = new Dictionary<long, decimal?>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values[reader.GetInt64(0)] = reader.IsDBNull(1) ? null : reader.GetDecimal(1);
        }

        return values;
    }

    private static void AddScopedParameters(NpgsqlCommand command, int? storeId)
    {
        command.Parameters.Add(new NpgsqlParameter("storeId", NpgsqlDbType.Integer) { Value = (object?)storeId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("dataScope", NpgsqlDbType.Text) { Value = "all" });
        command.Parameters.Add(new NpgsqlParameter("toDate", NpgsqlDbType.Date) { Value = DBNull.Value });
    }

    private static string Text(string? value) =>
        value is null ? "NULL" : $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string Num(decimal? value) =>
        value is null ? "NULL" : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string Num(int? value) =>
        value is null ? "NULL" : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string Timestamp(DateOnly day) => $"TIMESTAMP '{day:yyyy-MM-dd} 12:00:00'";

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Expected scalar result for SQL: {sql}"));
    }

    private static Task<bool> HasSemanticRevenueColumnAsync(NpgsqlConnection connection) =>
        ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'vw_vendor_sales_nivelacija'
                  AND column_name = 'change_percent_revenue_semantic');
            """);

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath)).ReplaceLineEndings("\n");
}
