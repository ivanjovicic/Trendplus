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
