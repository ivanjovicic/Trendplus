using Microsoft.Extensions.Configuration;
using Npgsql;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsIntelligenceSmokeTests
{
    [Fact]
    public async Task ProductDemandSignalsView_Exists_WithExpectedColumns_AndQueryExecutes()
    {
        if (!TryGetAnalyticsConnectionString(out var connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await BootstrapIntelligenceSqlAsync(connection,
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql");

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
            "days_since_last_sale",
            "launch_age_days",
            "store_coverage",
            "source_rows"
        };

        Assert.Equal(expectedColumns, actualColumns);

        while (await reader.ReadAsync())
        {
            _ = reader.GetValue(0);
        }
    }

    [Fact]
    public async Task InventorySnapshotFoundationView_Exists_WithExpectedColumns_AndQueryExecutes()
    {
        if (!TryGetAnalyticsConnectionString(out var connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await BootstrapIntelligenceSqlAsync(connection,
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql",
            "Database/Analytics/Intelligence/025_inventory_snapshot_foundation_v1.sql");

        await using (var existsCommand = new NpgsqlCommand(
                         "SELECT to_regclass('analytics_intel.vw_inventory_snapshot_foundation_v1')::text;",
                         connection))
        {
            var relationName = (string?)await existsCommand.ExecuteScalarAsync();
            Assert.Equal("analytics_intel.vw_inventory_snapshot_foundation_v1", relationName);
        }

        const string probeSql = """
            SELECT
                article_id,
                snapshot_date,
                sku,
                product_name,
                observed_at_utc,
                observed_stock_qty,
                reconstructed_stock_qty,
                stock_qty,
                snapshot_source_status,
                has_mixed_evidence,
                source_records
            FROM analytics_intel.vw_inventory_snapshot_foundation_v1
            LIMIT 0;
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
            "snapshot_date",
            "sku",
            "product_name",
            "observed_at_utc",
            "observed_stock_qty",
            "reconstructed_stock_qty",
            "stock_qty",
            "snapshot_source_status",
            "has_mixed_evidence",
            "source_records"
        };

        Assert.Equal(expectedColumns, actualColumns);

        while (await reader.ReadAsync())
        {
            _ = reader.GetValue(0);
        }
    }

    [Fact]
    public async Task IntelligenceEndpointQueries_ExecuteAgainstMaterializedCaches()
    {
        if (!TryGetAnalyticsConnectionString(out var connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await BootstrapIntelligenceSqlAsync(connection,
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql",
            "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql",
            "Database/Analytics/Intelligence/025_inventory_snapshot_foundation_v1.sql",
            "Database/Analytics/Intelligence/023_price_intelligence_v1.sql",
            "Database/Analytics/Intelligence/024_trend_momentum_v1.sql");

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

    private static bool TryGetAnalyticsConnectionString(out string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var resolved = configuration.GetConnectionString("AnalyticsConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (!IntegrationDbGuard.TryResolveConnectionString(resolved, out var validConnectionString))
        {
            connectionString = string.Empty;
            return false;
        }

        if (!IntegrationDbGuard.TryEnsureAvailable(("AnalyticsConnection", validConnectionString)))
        {
            connectionString = string.Empty;
            return false;
        }

        connectionString = validConnectionString;
        return true;
    }

    private static async Task BootstrapIntelligenceSqlAsync(NpgsqlConnection connection, params string[] relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var filePath = Path.Combine(
                AppContext.BaseDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(filePath), $"Expected SQL file was not copied to output: {relativePath}");

            var sql = await File.ReadAllTextAsync(filePath);
            await using var bootstrapCommand = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = 0
            };
            await bootstrapCommand.ExecuteNonQueryAsync();
        }
    }
}
