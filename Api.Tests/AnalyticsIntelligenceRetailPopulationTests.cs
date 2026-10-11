using Npgsql;
using NpgsqlTypes;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class AnalyticsIntelligenceRetailPopulationTests
{
    private static readonly string[] IntelligenceScripts =
    [
        "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql",
        "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql",
        "Database/Analytics/Intelligence/024_trend_momentum_v1.sql",
    ];

    private readonly IntelligencePostgresFixture _fixture;

    public AnalyticsIntelligenceRetailPopulationTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IntelligenceViewsAndCaches_MatchUtcRetailPopulationOracle()
    {
        var database = await _fixture.CreateDatabaseAsync("retail_population");
        var utcToday = DateTime.UtcNow.Date;
        var headers = CreateHeaders(utcToday);
        var lines = CreateLines();
        await CreateAndSeedSourceTablesAsync(database, headers, lines);
        await BootstrapViewsAsync(database);

        // The independent oracle exercises the certified origin scopes without calling production
        // aggregation code. The intelligence views themselves are global/all-origin surfaces.
        Assert.Equal(4, BuildOraclePopulation(headers, lines, "imported")
            .Where(row => row.ArticleId == 101).Sum(row => row.Quantity));
        Assert.Equal(4, BuildOraclePopulation(headers, lines, "existing")
            .Where(row => row.ArticleId == 101).Sum(row => row.Quantity));
        Assert.Equal(9, BuildOraclePopulation(headers, lines, "all")
            .Where(row => row.ArticleId == 101).Sum(row => row.Quantity));
        Assert.DoesNotContain(BuildOraclePopulation(headers, lines, "all"), row => row.ReceiptNumber is "DUG" or "KOREKCIJA");
        Assert.DoesNotContain(BuildOraclePopulation(headers, lines, "all"), row => row.Quantity == 99);

        var allPopulation = BuildOraclePopulation(headers, lines, "all");
        foreach (var offset in new[] { -2, -1, 0 })
        {
            var signalDate = DateOnly.FromDateTime(utcToday.AddDays(offset));
            var expected = ExpectedDemand(allPopulation, articleId: 101, storeId: 7, signalDate);
            var actual = await ReadDemandSignalAsync(database, articleId: 101, storeId: 7, signalDate);
            Assert.Equal(expected.SalesVelocity, actual.SalesVelocity);
            Assert.Equal(expected.SourceRows, actual.SourceRows);
        }

        // The same SKU at a second store remains a separate demand series.
        var secondStore = await ReadDemandSignalAsync(
            database,
            articleId: 101,
            storeId: 8,
            DateOnly.FromDateTime(utcToday));
        Assert.Equal(3m, secondStore.SalesVelocity);

        var currentInventory = await ReadInventorySignalAsync(
            database,
            articleId: 101,
            DateOnly.FromDateTime(utcToday));
        Assert.Equal(0m, currentInventory.StockQty); // known, observed zero baseline
        Assert.Equal(0.3m, currentInventory.AverageDailySales30d); // 9 signed retail units / 30 days
        Assert.Equal(0m, (await ReadInventorySignalAsync(
            database,
            articleId: 505,
            DateOnly.FromDateTime(utcToday))).StockQty); // a separate known-zero control

        var missingDimensionInventory = await ReadInventorySignalAsync(
            database,
            articleId: 303,
            DateOnly.FromDateTime(utcToday));
        Assert.Null(missingDimensionInventory.StockQty);
        Assert.Null(missingDimensionInventory.DaysOfCover);
        Assert.Null(missingDimensionInventory.StockTurn);
        Assert.Null(missingDimensionInventory.StockoutDays);
        Assert.Null(missingDimensionInventory.LowStockDays);
        Assert.Null(missingDimensionInventory.DeadStockRisk);
        Assert.Equal(0.033333m, missingDimensionInventory.AverageDailySales30d);

        // A real ProductsDim row with NULL quantity must not become a measured zero.
        var nullQuantityInventory = await ReadInventorySignalAsync(
            database,
            articleId: 606,
            DateOnly.FromDateTime(utcToday));
        Assert.Null(nullQuantityInventory.StockQty);
        Assert.Null(nullQuantityInventory.DaysOfCover);
        Assert.Null(nullQuantityInventory.StockTurn);
        Assert.Null(nullQuantityInventory.StockoutDays);
        Assert.Null(nullQuantityInventory.LowStockDays);
        Assert.Null(nullQuantityInventory.DeadStockRisk);

        var apiMissingDimensionInventory = await AnalyticsIntelligenceEndpoints.QueryInventoryRiskSignalsAsync(
            database.ConnectionString,
            new InventoryRiskSignalsRequest(
                Date: utcToday,
                HistoryDays: 1,
                ArticleId: 303,
                SupplierId: null,
                Category: null,
                MinDeadStockRisk: null,
                OnlyAtRisk: false,
                Page: 1,
                PageSize: 5,
                SortBy: "deadStockRisk",
                SortDir: "desc"),
            CancellationToken.None);
        var apiMissingDimensionItem = Assert.Single(apiMissingDimensionInventory.Items);
        Assert.Null(apiMissingDimensionItem.StockQty);
        Assert.Null(apiMissingDimensionItem.DaysOfCover);
        Assert.Null(apiMissingDimensionItem.StockTurn);
        Assert.Null(apiMissingDimensionItem.StockoutDays);
        Assert.Null(apiMissingDimensionItem.LowStockDays);
        Assert.Null(apiMissingDimensionItem.DeadStockRisk);
        Assert.Equal(0.033333m, apiMissingDimensionItem.AvgDailySales30d);

        var apiKnownZeroInventory = await AnalyticsIntelligenceEndpoints.QueryInventoryRiskSignalsAsync(
            database.ConnectionString,
            new InventoryRiskSignalsRequest(
                Date: utcToday,
                HistoryDays: 1,
                ArticleId: 505,
                SupplierId: null,
                Category: null,
                MinDeadStockRisk: null,
                OnlyAtRisk: false,
                Page: 1,
                PageSize: 5,
                SortBy: "deadStockRisk",
                SortDir: "desc"),
            CancellationToken.None);
        Assert.Equal(0m, Assert.Single(apiKnownZeroInventory.Items).StockQty);

        var apiNullQuantityInventory = await AnalyticsIntelligenceEndpoints.QueryInventoryRiskSignalsAsync(
            database.ConnectionString,
            new InventoryRiskSignalsRequest(
                Date: utcToday,
                HistoryDays: 1,
                ArticleId: 606,
                SupplierId: null,
                Category: null,
                MinDeadStockRisk: null,
                OnlyAtRisk: false,
                Page: 1,
                PageSize: 5,
                SortBy: "deadStockRisk",
                SortDir: "desc"),
            CancellationToken.None);
        var apiNullQuantityItem = Assert.Single(apiNullQuantityInventory.Items);
        Assert.Null(apiNullQuantityItem.StockQty);
        Assert.Null(apiNullQuantityItem.DaysOfCover);
        Assert.Null(apiNullQuantityItem.StockTurn);
        Assert.Null(apiNullQuantityItem.StockoutDays);
        Assert.Null(apiNullQuantityItem.LowStockDays);
        Assert.Null(apiNullQuantityItem.DeadStockRisk);

        // Sales for a product without a product-dimension row remain visible as sales evidence,
        // while their inventory baseline remains unknown. A DEMO-origin row is included by the
        // global all-origin view, so callers must not describe this view as Access-only history.
        Assert.True(await ExistsAsync(database,
            $"SELECT EXISTS (SELECT 1 FROM analytics_intel.vw_product_demand_signals_v1 WHERE article_id = 303 AND date = DATE '{utcToday:yyyy-MM-dd}')"));
        Assert.True(await ExistsAsync(database,
            $"SELECT EXISTS (SELECT 1 FROM analytics_intel.vw_trend_momentum_v1 WHERE article_id = 404)"));

        var expectedTrend = ExpectedTrend(allPopulation, articleId: 101, DateOnly.FromDateTime(utcToday));
        var trend = await ReadTrendSignalAsync(database, articleId: 101);
        Assert.Equal(expectedTrend.LocalSalesAcceleration, trend.LocalSalesAcceleration, 5);
        Assert.Equal(expectedTrend.TrendEntropy, trend.TrendEntropy);

        // A signed net denominator is not a probability base: with daily +10, +2 and -9,
        // the current SQL produces a negative normalized Shannon entropy.
        var refundDominatedTrend = await ReadTrendSignalAsync(database, articleId: 707);
        Assert.InRange(refundDominatedTrend.TrendEntropy!.Value, 0m, 1m);
        var refundDominatedOracle = ExpectedTrend(
            allPopulation,
            articleId: 707,
            DateOnly.FromDateTime(utcToday));
        Assert.Equal(refundDominatedOracle.LocalSalesAcceleration, refundDominatedTrend.LocalSalesAcceleration);
        Assert.Equal(0m, (await ReadTrendSignalAsync(database, articleId: 708)).TrendEntropy); // 10 / -9: one gross-sales day
        Assert.Equal(0m, (await ReadTrendSignalAsync(database, articleId: 709)).TrendEntropy); // 10 / -10: one gross-sales day
        Assert.Null((await ReadTrendSignalAsync(database, articleId: 710)).TrendEntropy); // zero-only rows: no probability mass
        Assert.Null((await ReadTrendSignalAsync(database, articleId: 711)).TrendEntropy); // pure returns: no gross-sales evidence
        Assert.Equal(0.970951m, (await ReadTrendSignalAsync(database, articleId: 712)).TrendEntropy); // gross units 2 / 3
        Assert.Equal(0m, (await ReadTrendSignalAsync(database, articleId: 713)).TrendEntropy); // positive sale and refund across UTC midnight
        Assert.Null((await ReadTrendSignalAsync(database, articleId: 714)).TrendEntropy); // external trend only, no local sales

        var apiTrend = await AnalyticsIntelligenceEndpoints.QueryTrendMomentumAsync(
            database.ConnectionString,
            new TrendMomentumRequest(710, null, null, null, null, 1, 5, null, null),
            CancellationToken.None);
        var apiMissingEntropy = Assert.Single(apiTrend.Items);
        Assert.Null(apiMissingEntropy.TrendEntropy);
        using (var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(
            apiMissingEntropy,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))))
        {
            Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("trendEntropy").ValueKind);
        }
        var apiMeasuredZero = await AnalyticsIntelligenceEndpoints.QueryTrendMomentumAsync(
            database.ConnectionString,
            new TrendMomentumRequest(708, null, null, null, null, 1, 5, null, null),
            CancellationToken.None);
        Assert.Equal(0m, Assert.Single(apiMeasuredZero.Items).TrendEntropy);

        foreach (var viewAndCache in new[]
        {
            ("vw_product_demand_signals_v1", "mv_product_demand_signals_v1_cache"),
            ("vw_inventory_risk_signals_v1", "mv_inventory_risk_signals_v1_cache"),
            ("vw_trend_momentum_v1", "mv_trend_momentum_v1_cache"),
        })
        {
            var differenceCount = await ReadIntAsync(database, $"""
                SELECT COUNT(*)::integer
                FROM (
                    (SELECT * FROM analytics_intel.{viewAndCache.Item1}
                     EXCEPT ALL
                     SELECT * FROM analytics_intel.{viewAndCache.Item2})
                    UNION ALL
                    (SELECT * FROM analytics_intel.{viewAndCache.Item2}
                     EXCEPT ALL
                     SELECT * FROM analytics_intel.{viewAndCache.Item1})
                ) differences
                """);
            Assert.Equal(0, differenceCount);
        }
    }

    private static IReadOnlyList<SaleHeader> CreateHeaders(DateTime utcToday)
    {
        return
        [
            // The source SaleId 5001 deliberately collides with another header's surrogate Id.
            new(7000, 5001, 7, AtUtc(utcToday, -2, 23, 50), "R-EXISTING", "existing"),
            new(5001, 6001, 9, AtUtc(utcToday, -10, 12, 0), "R-ID-COLLISION", "existing"),
            new(7001, 5002, 7, AtUtc(utcToday, -1, 0, 5), "R-RETURN", "existing"),
            new(7002, 5003, 7, AtUtc(utcToday, 0, 0, 5), "R-IMPORTED", "access"),
            new(7003, 5004, 8, AtUtc(utcToday, 0, 2, 0), "R-OTHER-STORE", "existing"),
            new(7004, 5005, 7, AtUtc(utcToday, 0, 3, 0), "  dUg  ", "existing"),
            new(7005, 5006, 7, AtUtc(utcToday, -1, 4, 0), " KoReKcIjA ", "access"),
            new(7006, 5007, 7, AtUtc(utcToday, 0, 5, 0), "R-DEMO", "demo"),
            new(7007, 5008, 7, AtUtc(utcToday, 0, 6, 0), "R-MISSING-DIM", "existing"),
            new(7008, 5009, 8, AtUtc(utcToday, 0, 7, 0), "R-DEMO-ONLY", "demo"),
            new(7010, 5010, 7, AtUtc(utcToday, -5, 12, 0), "R-707-10", "existing"),
            new(7011, 5011, 7, AtUtc(utcToday, -4, 12, 0), "R-707-2", "existing"),
            new(7012, 5012, 7, AtUtc(utcToday, -3, 12, 0), "R-707-return", "existing"),
            new(7013, 5013, 7, AtUtc(utcToday, -5, 12, 0), "R-708-10", "existing"),
            new(7014, 5014, 7, AtUtc(utcToday, -4, 12, 0), "R-708-return", "existing"),
            new(7015, 5015, 7, AtUtc(utcToday, -5, 12, 0), "R-709-10", "existing"),
            new(7016, 5016, 7, AtUtc(utcToday, -4, 12, 0), "R-709-return", "existing"),
            new(7017, 5017, 7, AtUtc(utcToday, -5, 12, 0), "R-710-zero", "existing"),
            new(7018, 5018, 7, AtUtc(utcToday, -5, 12, 0), "R-711-return", "existing"),
            new(7019, 5019, 7, AtUtc(utcToday, -5, 12, 0), "R-712-2", "existing"),
            new(7020, 5020, 7, AtUtc(utcToday, -4, 12, 0), "R-712-3", "existing"),
            new(7021, 5021, 7, AtUtc(utcToday, -1, 23, 59), "R-713-sale", "existing"),
            new(7022, 5022, 7, AtUtc(utcToday, 0, 0, 1), "R-713-return", "existing"),
        ];
    }

    private static IReadOnlyList<SaleLine> CreateLines()
    {
        return
        [
            new(5001, 101, 2),
            new(5002, 101, -1), // signed customer return
            new(5003, 101, 4),
            new(5004, 101, 3),
            new(5005, 101, 10),
            new(5006, 101, 9),
            new(5007, 101, 1), // all includes other origins; no Access-only claim
            new(5008, 303, 1), // no ProductsDim row
            new(5009, 404, 5),
            new(9999, 101, 99), // orphan line has no matching sale header
            new(5010, 707, 10),
            new(5011, 707, 2),
            new(5012, 707, -9),
            new(5013, 708, 10),
            new(5014, 708, -9),
            new(5015, 709, 10),
            new(5016, 709, -10),
            new(5017, 710, 0),
            new(5018, 711, -2),
            new(5019, 712, 2),
            new(5020, 712, 3),
            new(5021, 713, 5),
            new(5022, 713, -4),
        ];
    }

    private async Task CreateAndSeedSourceTablesAsync(
        IntelligenceTestDatabase database,
        IReadOnlyList<SaleHeader> headers,
        IReadOnlyList<SaleLine> lines)
    {
        await using var connection = await OpenUtcIndependentSessionAsync(database);
        await using (var schema = new NpgsqlCommand("""
            CREATE TABLE "ProductsDim" (
                "ProductId" integer NOT NULL,
                "Timestamp" timestamp with time zone NOT NULL,
                "PLU" text NULL,
                "ProductName" text NULL,
                "Kolicina" integer NULL,
                "MinimalnaKolicina" integer NULL,
                "PurchasePrice" numeric(18,2) NULL,
                "PurchasePriceRsd" numeric(18,2) NULL,
                "Brand" text NULL,
                "Category" text NULL,
                "FirstSalePrice" numeric(18,2) NULL,
                "SalePrice" numeric(18,2) NULL,
                "SupplierId" integer NULL,
                "DataOrigin" text NULL
            );
            CREATE TABLE "SuppliersDim" (
                "SupplierId" integer NOT NULL,
                "Naziv" text NULL,
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT clock_timestamp()
            );
            CREATE TABLE "SalesFacts" (
                "Id" bigint PRIMARY KEY,
                "SaleId" integer NOT NULL UNIQUE,
                "BrojRacuna" text NULL,
                "SaleTimestampUtc" timestamp with time zone NOT NULL,
                "StoreId" integer NOT NULL,
                "DataOrigin" text NULL
            );
            CREATE TABLE "SalesLineFacts" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SaleId" integer NOT NULL,
                "ProductId" integer NOT NULL,
                "Qty" integer NOT NULL
            );
            CREATE TABLE "InventoryMovementFacts" (
                "ArtikalId" integer NULL,
                "Datum" timestamp with time zone NOT NULL,
                "TipPromene" character varying(100) NOT NULL,
                "Kolicina" integer NULL
            );
            CREATE TABLE "GlobalTrendScores" (
                "LocalProductId" integer NOT NULL,
                "FinalGlobalScore" numeric(18,4) NOT NULL
            );
            CREATE TABLE "TrendHistory" (
                "LocalProductId" integer NOT NULL,
                "FinalGlobalScore" numeric(18,4) NOT NULL,
                "Date" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            INSERT INTO "ProductsDim" ("ProductId", "Timestamp", "Kolicina") VALUES
                (101, clock_timestamp(), 0),
                (505, clock_timestamp(), 0),
                (606, clock_timestamp(), NULL);
            INSERT INTO "GlobalTrendScores" ("LocalProductId", "FinalGlobalScore") VALUES (714, 12);
            """, connection))
            await schema.ExecuteNonQueryAsync();

        foreach (var header in headers)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO "SalesFacts" ("Id", "SaleId", "StoreId", "SaleTimestampUtc", "BrojRacuna", "DataOrigin")
                VALUES (@id, @saleId, @storeId, @soldAt, @receiptNumber, @dataOrigin)
                """, connection);
            insert.Parameters.AddWithValue("id", header.Id);
            insert.Parameters.AddWithValue("saleId", header.SaleId);
            insert.Parameters.AddWithValue("storeId", header.StoreId);
            insert.Parameters.AddWithValue("soldAt", NpgsqlDbType.TimestampTz, header.SoldAtUtc);
            insert.Parameters.AddWithValue("receiptNumber", header.ReceiptNumber);
            insert.Parameters.AddWithValue("dataOrigin", (object?)header.DataOrigin ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync();
        }

        foreach (var line in lines)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO "SalesLineFacts" ("SaleId", "ProductId", "Qty")
                VALUES (@saleId, @articleId, @quantity)
                """, connection);
            insert.Parameters.AddWithValue("saleId", line.SaleId);
            insert.Parameters.AddWithValue("articleId", line.ArticleId);
            insert.Parameters.AddWithValue("quantity", line.Quantity);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private async Task BootstrapViewsAsync(IntelligenceTestDatabase database)
    {
        await _fixture.RunFreshBootstrapAsync(database.ConnectionString, async connectionString =>
        {
            await using var connection = await _fixture.OpenConnectionAsync(database);
            await SetTestTimeZoneAsync(connection);
            foreach (var relativePath in new[] { "Database/Analytics/Intelligence/020_create_intelligence_schema.sql" }.Concat(IntelligenceScripts))
            {
                await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(RepoPath(relativePath)), connection)
                {
                    CommandTimeout = 0,
                };
                await command.ExecuteNonQueryAsync();
            }
        });
    }

    private async Task<DemandSignal> ReadDemandSignalAsync(
        IntelligenceTestDatabase database,
        int articleId,
        int storeId,
        DateOnly date)
    {
        await using var connection = await OpenUtcIndependentSessionAsync(database);
        await using var command = new NpgsqlCommand($"""
            SELECT sales_velocity, source_rows
            FROM analytics_intel.vw_product_demand_signals_v1
            WHERE article_id = {articleId}
              AND store_id = {storeId}
              AND date = DATE '{date:yyyy-MM-dd}'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Missing demand signal for article {articleId}, store {storeId}, {date}.");
        return new DemandSignal(reader.GetDecimal(0), reader.GetInt32(1));
    }

    private async Task<InventorySignal> ReadInventorySignalAsync(
        IntelligenceTestDatabase database,
        int articleId,
        DateOnly date)
    {
        await using var connection = await OpenUtcIndependentSessionAsync(database);
        await using var command = new NpgsqlCommand($"""
            SELECT stock_qty, avg_daily_sales_30d, days_of_cover, stock_turn,
                   stockout_days, low_stock_days, dead_stock_risk
            FROM analytics_intel.vw_inventory_risk_signals_v1
            WHERE article_id = {articleId}
              AND date = DATE '{date:yyyy-MM-dd}'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Missing inventory signal for article {articleId}, {date}.");
        return new InventorySignal(
            ReadNullableDecimal(reader, 0),
            reader.GetDecimal(1),
            ReadNullableDecimal(reader, 2),
            ReadNullableDecimal(reader, 3),
            ReadNullableInt(reader, 4),
            ReadNullableInt(reader, 5),
            ReadNullableDecimal(reader, 6));
    }

    private async Task<TrendSignal> ReadTrendSignalAsync(IntelligenceTestDatabase database, int articleId)
    {
        await using var connection = await OpenUtcIndependentSessionAsync(database);
        await using var command = new NpgsqlCommand($"""
            SELECT local_sales_acceleration, trend_entropy
            FROM analytics_intel.vw_trend_momentum_v1
            WHERE article_id = {articleId}
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Missing trend signal for article {articleId}.");
        return new TrendSignal(reader.GetDecimal(0), ReadNullableDecimal(reader, 1));
    }

    private async Task<bool> ExistsAsync(IntelligenceTestDatabase database, string sql)
    {
        await using var connection = await OpenUtcIndependentSessionAsync(database);
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private async Task<int> ReadIntAsync(IntelligenceTestDatabase database, string sql)
    {
        await using var connection = await OpenUtcIndependentSessionAsync(database);
        await using var command = new NpgsqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a query result."));
    }

    private async Task<NpgsqlConnection> OpenUtcIndependentSessionAsync(IntelligenceTestDatabase database)
    {
        var connection = await _fixture.OpenConnectionAsync(database);
        await SetTestTimeZoneAsync(connection);
        return connection;
    }

    private static Task SetTestTimeZoneAsync(NpgsqlConnection connection)
    {
        return new NpgsqlCommand("SET TIME ZONE 'Pacific/Kiritimati';", connection).ExecuteNonQueryAsync();
    }

    private static decimal? ReadNullableDecimal(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    private static int? ReadNullableInt(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static IReadOnlyList<OracleSale> BuildOraclePopulation(
        IReadOnlyList<SaleHeader> headers,
        IReadOnlyList<SaleLine> lines,
        string rawScope)
    {
        var scope = rawScope.Trim().ToLowerInvariant();
        var bySaleId = headers.ToDictionary(header => header.SaleId);
        var rows = new List<OracleSale>();
        foreach (var line in lines)
        {
            if (!bySaleId.TryGetValue(line.SaleId, out var header))
                continue;

            var normalizedReceipt = header.ReceiptNumber.Trim();
            if (string.Equals(normalizedReceipt, "DUG", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedReceipt, "KOREKCIJA", StringComparison.OrdinalIgnoreCase))
                continue;

            var inScope = scope switch
            {
                "imported" => string.Equals(header.DataOrigin, "access", StringComparison.Ordinal),
                "existing" => string.IsNullOrEmpty(header.DataOrigin)
                    || string.Equals(header.DataOrigin, "existing", StringComparison.Ordinal),
                _ => true,
            };
            if (!inScope)
                continue;

            rows.Add(new OracleSale(
                header.SaleId,
                line.ArticleId,
                header.StoreId,
                DateOnly.FromDateTime(header.SoldAtUtc),
                header.ReceiptNumber,
                header.DataOrigin,
                line.Quantity));
        }

        return rows;
    }

    private static DemandSignal ExpectedDemand(
        IReadOnlyList<OracleSale> population,
        int articleId,
        int storeId,
        DateOnly signalDate)
    {
        var daily = population
            .Where(row => row.ArticleId == articleId && row.StoreId == storeId)
            .GroupBy(row => row.SaleDate)
            .ToDictionary(
                group => group.Key,
                group => (Units: group.Sum(row => row.Quantity), Rows: group.Count()));

        var velocity = 0;
        var sourceRows = 0;
        for (var offset = 0; offset < 14; offset++)
        {
            var date = signalDate.AddDays(-offset);
            if (!daily.TryGetValue(date, out var value))
                continue;
            sourceRows += value.Rows;
            if (offset < 7)
                velocity += value.Units;
        }

        return new DemandSignal(velocity, sourceRows);
    }

    private static TrendSignal ExpectedTrend(
        IReadOnlyList<OracleSale> population,
        int articleId,
        DateOnly signalDate)
    {
        var daily = population
            .Where(row => row.ArticleId == articleId
                && row.SaleDate >= signalDate.AddDays(-28)
                && row.SaleDate <= signalDate)
            .GroupBy(row => row.SaleDate)
            .ToDictionary(group => group.Key, group => new
            {
                NetUnits = group.Sum(row => row.Quantity),
                GrossUnits = group.Sum(row => Math.Max(row.Quantity, 0))
            });

        var values = Enumerable.Range(0, 29)
            .Select(day =>
            {
                var item = daily.GetValueOrDefault(signalDate.AddDays(day - 28));
                return (X: (double)day, Net: (double)(item?.NetUnits ?? 0), Gross: (double)(item?.GrossUnits ?? 0));
            })
            .ToArray();
        var meanX = values.Average(value => value.X);
        var meanY = values.Average(value => value.Net);
        var slope = values.Sum(value => (value.X - meanX) * (value.Net - meanY))
            / values.Sum(value => Math.Pow(value.X - meanX, 2));

        var grossSales = values.Where(value => value.Gross > 0).Select(value => value.Gross).ToArray();
        decimal? entropy = null;
        if (grossSales.Length == 1)
        {
            entropy = 0m;
        }
        else if (grossSales.Length > 1)
        {
            var totalGross = grossSales.Sum();
            var entropySum = grossSales
                .Select(units => units / totalGross)
                .Sum(share => share * Math.Log(share));
            entropy = decimal.Round((decimal)(-entropySum / Math.Log(grossSales.Length)), 6);
        }

        return new TrendSignal(
            decimal.Round((decimal)slope, 6),
            entropy);
    }

    private static DateTime AtUtc(DateTime utcToday, int dayOffset, int hour, int minute) =>
        DateTime.SpecifyKind(utcToday.AddDays(dayOffset).AddHours(hour).AddMinutes(minute), DateTimeKind.Utc);

    private static string RepoPath(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    private sealed record SaleHeader(
        long Id,
        int SaleId,
        int StoreId,
        DateTime SoldAtUtc,
        string ReceiptNumber,
        string? DataOrigin);

    private sealed record SaleLine(int SaleId, int ArticleId, int Quantity);

    private sealed record OracleSale(
        int SaleId,
        int ArticleId,
        int StoreId,
        DateOnly SaleDate,
        string ReceiptNumber,
        string? DataOrigin,
        int Quantity);

    private sealed record DemandSignal(decimal SalesVelocity, int SourceRows);

    private sealed record InventorySignal(
        decimal? StockQty,
        decimal AverageDailySales30d,
        decimal? DaysOfCover,
        decimal? StockTurn,
        int? StockoutDays,
        int? LowStockDays,
        decimal? DeadStockRisk);

    private sealed record TrendSignal(decimal LocalSalesAcceleration, decimal? TrendEntropy);
}
