using Npgsql;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class AnalyticsIntelligenceSmokeTests
{
    private readonly IntelligencePostgresFixture _fixture;

    public AnalyticsIntelligenceSmokeTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProductDemandSignalsView_Exists_WithExpectedColumns_AndQueryExecutes()
    {
        var database = await CreateBootstrappedDatabaseAsync(
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql");
        await using var connection = await _fixture.OpenConnectionAsync(database);

        await using (var existsCommand = new NpgsqlCommand(
                         "SELECT to_regclass('analytics_intel.vw_product_demand_signals_v1')::text;",
                         connection))
        {
            var relationName = (string?)await existsCommand.ExecuteScalarAsync();
            Assert.Equal("analytics_intel.vw_product_demand_signals_v1", relationName);
        }

        const string probeSql = """
            SELECT
                article_id,
                store_id,
                date,
                sales_velocity,
                demand_acceleration,
                demand_state,
                days_since_last_sale,
                launch_age_days,
                store_coverage,
                source_rows
            FROM analytics_intel.vw_product_demand_signals_v1
            LIMIT 5;
            """;

        await using var probeCommand = new NpgsqlCommand(probeSql, connection)
        {
            CommandTimeout = 0
        };

        await using var reader = await probeCommand.ExecuteReaderAsync();
        var actualColumns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        var expectedColumns = new[]
        {
            "article_id",
            "store_id",
            "date",
            "sales_velocity",
            "demand_acceleration",
            "demand_state",
            "days_since_last_sale",
            "launch_age_days",
            "store_coverage",
            "source_rows"
        };

        Assert.Equal(expectedColumns, actualColumns);

        Assert.True(await reader.ReadAsync(), "The isolated seed should produce product demand rows.");
        do
        {
            _ = reader.GetValue(0);
        }
        while (await reader.ReadAsync());
    }

    [Fact]
    public async Task ProductDemandSignalsView_DistinguishesNewDemandFromMeasuredAcceleration()
    {
        var database = await CreateBootstrappedDatabaseAsync(
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql");
        await using var connection = await _fixture.OpenConnectionAsync(database);

        await using var command = new NpgsqlCommand("""
            SELECT COUNT(*)
            FROM analytics_intel.vw_product_demand_signals_v1
            WHERE demand_state IN ('NEW_DEMAND', 'NO_BASELINE')
              AND demand_acceleration IS NOT NULL;
            """, connection)
        {
            CommandTimeout = 0
        };

        var invalidBaselineCount = Convert.ToInt64(await command.ExecuteScalarAsync());
        Assert.Equal(0L, invalidBaselineCount);
        Assert.True(await ScalarLongAsync(connection, "SELECT COUNT(*) FROM analytics_intel.vw_product_demand_signals_v1;") > 0);
    }

    [Fact]
    public async Task ObservedDailyStockView_Exists_WithProvenanceColumns()
    {
        var database = await CreateBootstrappedDatabaseAsync(
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql",
            "Database/Analytics/Intelligence/025_observed_inventory_daily_snapshot_v1.sql");
        await using var connection = await _fixture.OpenConnectionAsync(database);

        await using (var existsCommand = new NpgsqlCommand(
                         "SELECT to_regclass('analytics_intel.vw_inventory_daily_stock_v1')::text;",
                         connection))
        {
            var relationName = (string?)await existsCommand.ExecuteScalarAsync();
            Assert.Equal("analytics_intel.vw_inventory_daily_stock_v1", relationName);
        }

        const string probeSql = """
            SELECT
                article_id,
                store_id,
                date,
                observed_qty,
                reconstructed_qty,
                stock_qty,
                provenance,
                captured_at_utc,
                source_system
            FROM analytics_intel.vw_inventory_daily_stock_v1
            LIMIT 5;
            """;

        await using var probeCommand = new NpgsqlCommand(probeSql, connection)
        {
            CommandTimeout = 0
        };

        await using var reader = await probeCommand.ExecuteReaderAsync();
        var actualColumns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        Assert.Equal(
            [
                "article_id",
                "store_id",
                "date",
                "observed_qty",
                "reconstructed_qty",
                "stock_qty",
                "provenance",
                "captured_at_utc",
                "source_system"
            ],
            actualColumns);

        Assert.True(await reader.ReadAsync(), "The isolated seed should produce observed stock view rows.");
        do
        {
            _ = reader.GetValue(0);
        }
        while (await reader.ReadAsync());
    }

    [Fact]
    public async Task IntelligenceEndpointQueries_ExecuteAgainstMaterializedCaches()
    {
        var database = await CreateBootstrappedDatabaseAsync(
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql",
            "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql",
            "Database/Analytics/Intelligence/025_observed_inventory_daily_snapshot_v1.sql",
            "Database/Analytics/Intelligence/023_price_intelligence_v1.sql",
            "Database/Analytics/Intelligence/024_trend_momentum_v1.sql");
        var connectionString = database.ConnectionString;
        await using var connection = await _fixture.OpenConnectionAsync(database);

        Assert.True(await ScalarLongAsync(connection, "SELECT COUNT(*) FROM analytics_intel.mv_product_demand_signals_v1_cache;") > 0);
        Assert.True(await ScalarLongAsync(connection, "SELECT COUNT(*) FROM analytics_intel.mv_inventory_risk_signals_v1_cache;") > 0);
        Assert.True(await ScalarLongAsync(connection, "SELECT COUNT(*) FROM analytics_intel.mv_price_intelligence_v1_cache;") > 0);
        Assert.True(await ScalarLongAsync(connection, "SELECT COUNT(*) FROM analytics_intel.mv_trend_momentum_v1_cache;") > 0);

        var demand = await AnalyticsIntelligenceEndpoints.QueryDemandSignalsAsync(
            connectionString,
            new DemandSignalsRequest(
                Date: null,
                HistoryDays: 1,
                ArticleId: null,
                StoreId: null,
                SupplierId: null,
                Category: null,
                MinSalesVelocity: null,
                MinDemandAcceleration: null,
                Page: 1,
                PageSize: 5,
                SortBy: "salesVelocity",
                SortDir: "desc"),
            CancellationToken.None);

        var inventory = await AnalyticsIntelligenceEndpoints.QueryInventoryRiskSignalsAsync(
            connectionString,
            new InventoryRiskSignalsRequest(
                Date: null,
                HistoryDays: 1,
                ArticleId: null,
                SupplierId: null,
                Category: null,
                MinDeadStockRisk: null,
                OnlyAtRisk: false,
                Page: 1,
                PageSize: 5,
                SortBy: "deadStockRisk",
                SortDir: "desc"),
            CancellationToken.None);

        var price = await AnalyticsIntelligenceEndpoints.QueryPriceIntelligenceAsync(
            connectionString,
            new PriceIntelligenceRequest(
                ArticleId: null,
                SupplierId: null,
                Category: null,
                BrandKey: null,
                MinDiscountDepth: null,
                MinMarginPct: null,
                Page: 1,
                PageSize: 5,
                SortBy: "marginPct",
                SortDir: "desc"),
            CancellationToken.None);

        var trend = await AnalyticsIntelligenceEndpoints.QueryTrendMomentumAsync(
            connectionString,
            new TrendMomentumRequest(
                ArticleId: null,
                SupplierId: null,
                Category: null,
                MinExternalTrendScore: null,
                MinLocalSalesAcceleration: null,
                Page: 1,
                PageSize: 5,
                SortBy: "externalTrendScore",
                SortDir: "desc"),
            CancellationToken.None);

        Assert.Equal(1, demand.Page);
        Assert.Equal(5, demand.PageSize);
        Assert.True(demand.TotalCount >= demand.Items.Count);

        Assert.Equal(1, inventory.Page);
        Assert.Equal(5, inventory.PageSize);
        Assert.True(inventory.TotalCount >= inventory.Items.Count);

        Assert.Equal(1, price.Page);
        Assert.Equal(5, price.PageSize);
        Assert.True(price.TotalCount >= price.Items.Count);

        Assert.Equal(1, trend.Page);
        Assert.Equal(5, trend.PageSize);
        Assert.True(trend.TotalCount >= trend.Items.Count);
    }

    [Fact]
    public async Task PriceIntelligenceView_DoesNotExposeDiscountDepthForInvalidListPrice()
    {
        var database = await CreateBootstrappedDatabaseAsync(
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/023_price_intelligence_v1.sql");
        await using var connection = await _fixture.OpenConnectionAsync(database);

        await using var command = new NpgsqlCommand("""
            SELECT COUNT(*)
            FROM analytics_intel.vw_price_intelligence_v1
            WHERE list_price <= 0
              AND discount_depth IS NOT NULL;
            """, connection)
        {
            CommandTimeout = 0
        };

        var invalidDiscountCount = Convert.ToInt64(await command.ExecuteScalarAsync());
        Assert.Equal(0L, invalidDiscountCount);

        Assert.Equal(1L, await ScalarLongAsync(
            connection,
            "SELECT COUNT(*) FROM analytics_intel.vw_price_intelligence_v1 WHERE article_id = 43 AND list_price <= 0;"));
    }

    [Fact]
    public async Task PriceIntelligence_PreservesUnknownCostAcrossViewCacheAndApi()
    {
        var database = await _fixture.CreateDatabaseAsync("pricecost");
        await using (var connection = await _fixture.OpenConnectionAsync(database))
        {
            var seedPath = FindRepositoryFile("Api.Tests/Fixtures/analytics-intelligence-smoke-seed.sql");
            await using (var seed = new NpgsqlCommand(await File.ReadAllTextAsync(seedPath), connection)
                         { CommandTimeout = 0 })
            {
                await seed.ExecuteNonQueryAsync();
            }

            await using (var productDisplayColumns = new NpgsqlCommand("""
                             ALTER TABLE "ProductsDim"
                                 ADD COLUMN IF NOT EXISTS "PLU" text,
                                 ADD COLUMN IF NOT EXISTS "ProductName" text;
                             """, connection))
            {
                await productDisplayColumns.ExecuteNonQueryAsync();
            }

            await using var adversarial = new NpgsqlCommand("""
                INSERT INTO "ProductsDim" (
                    "ProductId", "Timestamp", "PurchasePrice", "PurchasePriceRsd", "Brand",
                    "Category", "FirstSalePrice", "SalePrice", "SupplierId", "DataOrigin")
                VALUES
                    (44, now(), NULL, NULL, 'RQ621', 'Cost oracle', 100, 100, 7, 'RQ621 fixture'),
                    (45, now(), 80, NULL, 'RQ621', 'Cost oracle', 100, 100, 7, 'RQ621 fixture'),
                    (46, now(), 80, 0, 'RQ621', 'Cost oracle', 100, 100, 7, 'RQ621 fixture'),
                    (47, now(), 70, 40, 'RQ621', 'Cost oracle', 100, 100, 7, 'RQ621 fixture'),
                    (48, now(), 70, -5, 'RQ621', 'Cost oracle', 100, 100, 7, 'RQ621 fixture'),
                    (49, now(), 100, 100, 'RQ621', 'Cost oracle', 100, 100, 7, 'RQ621 fixture');
                """, connection);
            await adversarial.ExecuteNonQueryAsync();

            await using var oldBehavior = new NpgsqlCommand("""
                SELECT
                    COALESCE("PurchasePriceRsd", "PurchasePrice", 0) AS cost,
                    ("SalePrice" - COALESCE("PurchasePriceRsd", "PurchasePrice", 0)) / "SalePrice" AS margin_pct
                FROM "ProductsDim"
                WHERE "ProductId" = 44;
                """, connection);
            await using var oldBehaviorReader = await oldBehavior.ExecuteReaderAsync();
            Assert.True(await oldBehaviorReader.ReadAsync());
            Assert.Equal(0m, oldBehaviorReader.GetDecimal(0));
            Assert.Equal(1m, oldBehaviorReader.GetDecimal(1));
        }

        await _fixture.RunFreshBootstrapAsync(database.ConnectionString, async connectionString =>
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using (var schema = new NpgsqlCommand(
                             await File.ReadAllTextAsync(FindRepositoryFile("Database/Analytics/Intelligence/020_create_intelligence_schema.sql")),
                             connection)
                         { CommandTimeout = 0 })
            {
                await schema.ExecuteNonQueryAsync();
            }

            await using (var legacyView = new NpgsqlCommand("""
                             CREATE VIEW analytics_intel.vw_price_intelligence_v1 AS
                             SELECT
                                 "ProductId" AS article_id,
                                 "Timestamp"::date AS price_date,
                                 COALESCE(NULLIF(BTRIM("Category"), ''), 'Uncategorized') AS category,
                                 COALESCE(NULLIF(BTRIM("Brand"), ''), 'unknown') AS brand_key,
                                 ROUND(COALESCE("SalePrice", 0)::numeric(18,4), 4) AS net_price,
                                 ROUND(COALESCE("FirstSalePrice", 0)::numeric(18,4), 4) AS list_price,
                                 ROUND(COALESCE("PurchasePriceRsd", "PurchasePrice", 0)::numeric(18,4), 4) AS cost,
                                 ROUND(1::numeric, 4) AS price_index_vs_category,
                                 ROUND(1::numeric, 4) AS price_index_vs_brand,
                                 ROUND(CASE WHEN "FirstSalePrice" > 0
                                            THEN ("FirstSalePrice" - "SalePrice") / "FirstSalePrice" END, 4) AS discount_depth,
                                 ROUND(CASE WHEN "SalePrice" > 0
                                            THEN ("SalePrice" - COALESCE("PurchasePriceRsd", "PurchasePrice", 0)) / "SalePrice" END, 4) AS margin_pct
                             FROM "ProductsDim"
                             WHERE "SalePrice" > 0;
                             CREATE MATERIALIZED VIEW analytics_intel.mv_price_intelligence_v1_cache AS
                             SELECT * FROM analytics_intel.vw_price_intelligence_v1;
                             """, connection))
            {
                await legacyView.ExecuteNonQueryAsync();
            }

            await using (var replaceView = new NpgsqlCommand(
                             await File.ReadAllTextAsync(FindRepositoryFile("Database/Analytics/Intelligence/023_price_intelligence_v1.sql")),
                             connection)
                         { CommandTimeout = 0 })
            {
                await replaceView.ExecuteNonQueryAsync();
            }

            await using (var staleCache = new NpgsqlCommand("""
                             SELECT cost, margin_pct
                             FROM analytics_intel.mv_price_intelligence_v1_cache
                             WHERE article_id = 44;
                             """, connection))
            await using (var staleReader = await staleCache.ExecuteReaderAsync())
            {
                Assert.True(await staleReader.ReadAsync());
                Assert.Equal(0m, staleReader.GetDecimal(0));
                Assert.Equal(1m, staleReader.GetDecimal(1));
            }

            await using var refresh = new NpgsqlCommand(
                "REFRESH MATERIALIZED VIEW analytics_intel.mv_price_intelligence_v1_cache;",
                connection);
            await refresh.ExecuteNonQueryAsync();
        });

        await using var queryConnection = await _fixture.OpenConnectionAsync(database);
        await using (var columns = new NpgsqlCommand("""
                         SELECT string_agg(column_name, ',' ORDER BY ordinal_position)
                         FROM information_schema.columns
                         WHERE table_schema = 'analytics_intel'
                           AND table_name = 'vw_price_intelligence_v1';
                         """, queryConnection))
        {
            Assert.Equal(
                "article_id,price_date,category,brand_key,net_price,list_price,cost,price_index_vs_category,price_index_vs_brand,discount_depth,margin_pct",
                await columns.ExecuteScalarAsync());
        }

        var viewRows = new Dictionary<int, (decimal? Cost, decimal? Margin)>();
        await using (var command = new NpgsqlCommand("""
            SELECT article_id, cost, margin_pct
            FROM analytics_intel.vw_price_intelligence_v1
            WHERE article_id BETWEEN 44 AND 49
            ORDER BY article_id;
            """, queryConnection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                viewRows.Add(
                    reader.GetInt32(0),
                    (reader.IsDBNull(1) ? null : reader.GetDecimal(1), reader.IsDBNull(2) ? null : reader.GetDecimal(2)));
            }
        }

        Assert.Equal(6, viewRows.Count);
        Assert.Equal((null, null), viewRows[44]);
        Assert.Equal((80m, 0.2m), viewRows[45]);
        Assert.Equal((null, null), viewRows[46]);
        Assert.Equal((40m, 0.6m), viewRows[47]);
        Assert.Equal((null, null), viewRows[48]);
        Assert.Equal((100m, 0m), viewRows[49]);

        Assert.Equal(0L, await ScalarLongAsync(queryConnection, """
            SELECT COUNT(*)
            FROM analytics_intel.vw_price_intelligence_v1 v
            FULL JOIN analytics_intel.mv_price_intelligence_v1_cache m
              USING (article_id, price_date)
            WHERE v.article_id BETWEEN 44 AND 49
              AND (v.article_id IS NULL OR m.article_id IS NULL OR v.cost IS DISTINCT FROM m.cost
                   OR v.margin_pct IS DISTINCT FROM m.margin_pct);
            """));

        var response = await AnalyticsIntelligenceEndpoints.QueryPriceIntelligenceAsync(
            database.ConnectionString,
            new PriceIntelligenceRequest(
                ArticleId: null,
                SupplierId: null,
                Category: "Cost oracle",
                BrandKey: "RQ621",
                MinDiscountDepth: null,
                MinMarginPct: null,
                Page: 1,
                PageSize: 20,
                SortBy: "marginPct",
                SortDir: "desc"),
            CancellationToken.None);

        Assert.Equal(6, response.TotalCount);
        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(47, response.Items[0].ArticleId);
        Assert.Equal(45, response.Items[1].ArticleId);
        Assert.All(response.Items.Skip(3), item => Assert.Null(item.MarginPct));
        Assert.Null(response.Items.Single(item => item.ArticleId == 44).Cost);
        Assert.Null(response.Items.Single(item => item.ArticleId == 44).MarginPct);
        Assert.Equal(0m, response.Items.Single(item => item.ArticleId == 49).MarginPct);

        var secondPage = await AnalyticsIntelligenceEndpoints.QueryPriceIntelligenceAsync(
            database.ConnectionString,
            new PriceIntelligenceRequest(
                ArticleId: null,
                SupplierId: null,
                Category: "Cost oracle",
                BrandKey: "RQ621",
                MinDiscountDepth: null,
                MinMarginPct: null,
                Page: 2,
                PageSize: 2,
                SortBy: "marginPct",
                SortDir: "desc"),
            CancellationToken.None);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(new[] { 49, 44 }, secondPage.Items.Select(item => item.ArticleId));

        var thresholdResponse = await AnalyticsIntelligenceEndpoints.QueryPriceIntelligenceAsync(
            database.ConnectionString,
            new PriceIntelligenceRequest(
                ArticleId: null,
                SupplierId: null,
                Category: "Cost oracle",
                BrandKey: "RQ621",
                MinDiscountDepth: null,
                MinMarginPct: 0.5m,
                Page: 1,
                PageSize: 20,
                SortBy: "marginPct",
                SortDir: "desc"),
            CancellationToken.None);
        Assert.Equal(new[] { 47 }, thresholdResponse.Items.Select(item => item.ArticleId));

        var json = System.Text.Json.JsonSerializer.Serialize(response.Items.Single(item => item.ArticleId == 44),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"cost\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"marginPct\":null", json, StringComparison.Ordinal);
    }

    private async Task<IntelligenceTestDatabase> CreateBootstrappedDatabaseAsync(params string[] scripts)
    {
        var database = await _fixture.CreateDatabaseAsync("smoke");
        await using (var connection = await _fixture.OpenConnectionAsync(database))
        {
            var seedPath = FindRepositoryFile("Api.Tests/Fixtures/analytics-intelligence-smoke-seed.sql");
            await using var seed = new NpgsqlCommand(await File.ReadAllTextAsync(seedPath), connection)
            {
                CommandTimeout = 0
            };
            await seed.ExecuteNonQueryAsync();
        }

        await _fixture.ExecuteIntelligenceScriptsAsync(database, scripts);
        return database;
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
