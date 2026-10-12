using Npgsql;
using NpgsqlTypes;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class AnalyticsIntelligenceNegativeStockProxyTests
{
    private const int ObservedZeroArticleId = 626001;
    private const int ObservedNegativeArticleId = 626002;
    private const int InferredNegativeArticleId = 626003;
    private const int MissingBaselineArticleId = 626004;
    private const int RestockTransferReturnArticleId = 626005;

    private readonly IntelligencePostgresFixture _fixture;

    public AnalyticsIntelligenceNegativeStockProxyTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NegativeReconstructedProxy_IsDistinctFromMeasuredZeroAndDoesNotFabricateStockouts()
    {
        var database = await _fixture.CreateDatabaseAsync("negative_stock_proxy");
        var utcToday = DateTime.UtcNow.Date;
        var yesterday = utcToday.AddDays(-1);
        var twoDaysAgo = utcToday.AddDays(-2);

        await CreateAndSeedSourceTablesAsync(database, utcToday, yesterday, twoDaysAgo);
        await _fixture.ExecuteIntelligenceScriptsAsync(
            database,
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql");

        var asOf = DateOnly.FromDateTime(utcToday);
        var prior = DateOnly.FromDateTime(yesterday);
        var older = DateOnly.FromDateTime(twoDaysAgo);

        // Independent signed arithmetic for the inferred-negative article:
        // current=0, as-of-day receipt +10, yesterday transfer-out 2 and sale 3.
        // Walking backward from as-of:
        //   as-of = 0
        //   yesterday = 0 - 10 = -10
        //   two-days-ago = -10 + 2 + 3 = -5
        Assert.Equal(0m, IndependentSignedStock(
            current: 0m,
            futureSales: 0m,
            futureTransferOut: 0m,
            futureReceipts: 0m,
            futureTransferIn: 0m,
            futureReturns: 0m));
        Assert.Equal(-10m, IndependentSignedStock(
            current: 0m,
            futureSales: 0m,
            futureTransferOut: 0m,
            futureReceipts: 10m,
            futureTransferIn: 0m,
            futureReturns: 0m));
        Assert.Equal(-5m, IndependentSignedStock(
            current: 0m,
            futureSales: 3m,
            futureTransferOut: 2m,
            futureReceipts: 10m,
            futureTransferIn: 0m,
            futureReturns: 0m));

        var observedZero = await ReadInventoryAsync(database, ObservedZeroArticleId, asOf);
        Assert.Equal(0m, observedZero.StockQty);
        // Constant measured-zero baseline fills the trailing 30-day stockout window.
        Assert.Equal(30, observedZero.StockoutDays);
        Assert.Null(observedZero.DaysOfCover);
        Assert.Equal("zero", AnalyticsIntelligenceEndpoints.ClassifyStockProxyEvidence(observedZero.StockQty));

        var observedNegative = await ReadInventoryAsync(database, ObservedNegativeArticleId, asOf);
        Assert.Equal(-3m, observedNegative.StockQty);
        Assert.Equal(0, observedNegative.StockoutDays);
        Assert.Null(observedNegative.DaysOfCover);
        Assert.Equal(0m, observedNegative.DeadStockRisk);
        Assert.Equal(
            "negative_proxy",
            AnalyticsIntelligenceEndpoints.ClassifyStockProxyEvidence(observedNegative.StockQty));

        var inferredAsOf = await ReadInventoryAsync(database, InferredNegativeArticleId, asOf);
        var inferredYesterday = await ReadInventoryAsync(database, InferredNegativeArticleId, prior);
        var inferredOlder = await ReadInventoryAsync(database, InferredNegativeArticleId, older);
        Assert.Equal(0m, inferredAsOf.StockQty);
        Assert.Equal(-10m, inferredYesterday.StockQty);
        Assert.Equal(-5m, inferredOlder.StockQty);
        Assert.Null(inferredYesterday.DaysOfCover);
        Assert.Null(inferredOlder.DaysOfCover);
        // Exact zero on as-of still counts; negative historical days must not add fabricated OOS.
        Assert.Equal(1, inferredAsOf.StockoutDays);
        Assert.Equal(
            "negative_proxy",
            AnalyticsIntelligenceEndpoints.ClassifyStockProxyEvidence(inferredYesterday.StockQty));

        var missing = await ReadInventoryAsync(database, MissingBaselineArticleId, asOf);
        Assert.Null(missing.StockQty);
        Assert.Null(missing.StockoutDays);
        Assert.Null(missing.DaysOfCover);
        Assert.Null(missing.DeadStockRisk);
        Assert.Equal("missing_baseline", AnalyticsIntelligenceEndpoints.ClassifyStockProxyEvidence(missing.StockQty));

        // Restock / transfer / return matrix on a positive baseline:
        // current=20; as-of day: sale 4, Ulaz 8, Prenos ulaz 3, Prenos izlaz 2, Povrat 1
        // as-of = 20
        // yesterday = 20 + 4 + 2 - 8 - 3 - 1 = 14
        var restockAsOf = await ReadInventoryAsync(database, RestockTransferReturnArticleId, asOf);
        var restockYesterday = await ReadInventoryAsync(database, RestockTransferReturnArticleId, prior);
        Assert.Equal(20m, restockAsOf.StockQty);
        Assert.Equal(14m, restockYesterday.StockQty);
        Assert.Equal(
            IndependentSignedStock(20m, 4m, 2m, 8m, 3m, 1m),
            restockYesterday.StockQty);
        Assert.Equal("positive", AnalyticsIntelligenceEndpoints.ClassifyStockProxyEvidence(restockAsOf.StockQty));

        var apiNegative = await AnalyticsIntelligenceEndpoints.QueryInventoryRiskSignalsAsync(
            database.ConnectionString,
            new InventoryRiskSignalsRequest(
                Date: utcToday,
                HistoryDays: 3,
                ArticleId: InferredNegativeArticleId,
                SupplierId: null,
                Category: null,
                MinDeadStockRisk: null,
                OnlyAtRisk: false,
                Page: 1,
                PageSize: 10,
                SortBy: "stockQty",
                SortDir: "asc"),
            CancellationToken.None);
        Assert.Contains(apiNegative.Items, item => item.StockQty == -10m && item.StockProxyEvidence == "negative_proxy");
        Assert.Contains(apiNegative.Items, item => item.StockQty == 0m && item.StockProxyEvidence == "zero");
        Assert.DoesNotContain(apiNegative.Items, item => item.StockQty < 0 && item.StockoutDays > 1);

        var apiOnlyAtRisk = await AnalyticsIntelligenceEndpoints.QueryInventoryRiskSignalsAsync(
            database.ConnectionString,
            new InventoryRiskSignalsRequest(
                Date: utcToday,
                HistoryDays: 3,
                ArticleId: InferredNegativeArticleId,
                SupplierId: null,
                Category: null,
                MinDeadStockRisk: null,
                OnlyAtRisk: true,
                Page: 1,
                PageSize: 10,
                SortBy: null,
                SortDir: null),
            CancellationToken.None);
        // onlyAtRisk must not promote a negative-proxy history into unjustified actionability
        // beyond the single exact-zero as-of day already present.
        Assert.All(apiOnlyAtRisk.Items, item => Assert.True(item.StockQty is null or >= 0 || item.StockoutDays == 0));

        var differenceCount = await ReadIntAsync(database, """
            SELECT COUNT(*)::integer
            FROM (
                (SELECT * FROM analytics_intel.vw_inventory_risk_signals_v1
                 EXCEPT ALL
                 SELECT * FROM analytics_intel.mv_inventory_risk_signals_v1_cache)
                UNION ALL
                (SELECT * FROM analytics_intel.mv_inventory_risk_signals_v1_cache
                 EXCEPT ALL
                 SELECT * FROM analytics_intel.vw_inventory_risk_signals_v1)
            ) differences
            """);
        Assert.Equal(0, differenceCount);
    }

    private static decimal IndependentSignedStock(
        decimal current,
        decimal futureSales,
        decimal futureTransferOut,
        decimal futureReceipts,
        decimal futureTransferIn,
        decimal futureReturns) =>
        current + futureSales + futureTransferOut - futureReceipts - futureTransferIn - futureReturns;

    private async Task CreateAndSeedSourceTablesAsync(
        IntelligenceTestDatabase database,
        DateTime utcToday,
        DateTime yesterday,
        DateTime twoDaysAgo)
    {
        await using var connection = await _fixture.OpenConnectionAsync(database);
        await using var schema = new NpgsqlCommand("""
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

            INSERT INTO "ProductsDim" (
                "ProductId", "Timestamp", "Kolicina", "MinimalnaKolicina", "PurchasePriceRsd",
                "PLU", "ProductName", "Category", "SupplierId")
            VALUES
                (@observedZero, @now, 0, 5, 100, 'Z-0', 'Observed zero', 'Obuca', 1),
                (@observedNegative, @now, -3, 5, 100, 'Z-NEG', 'Observed negative', 'Obuca', 1),
                (@inferredNegative, @now, 0, 5, 100, 'Z-INF', 'Inferred negative', 'Obuca', 1),
                (@restock, @now, 20, 5, 100, 'Z-RST', 'Restock matrix', 'Obuca', 1);

            INSERT INTO "SuppliersDim" ("SupplierId", "Naziv") VALUES (1, 'Dobavljac');

            INSERT INTO "SalesFacts" ("Id", "SaleId", "BrojRacuna", "SaleTimestampUtc", "StoreId", "DataOrigin") VALUES
                (626101, 626101, 'RQ626-INF-SALE', @yesterday, 1, 'existing'),
                (626102, 626102, 'RQ626-RST-SALE', @today, 1, 'existing'),
                (626103, 626103, 'RQ626-MISS-SALE', @yesterday, 1, 'existing');

            INSERT INTO "SalesLineFacts" ("SaleId", "ProductId", "Qty") VALUES
                (626101, @inferredNegative, 3),
                (626102, @restock, 4),
                (626103, @missingBaseline, 1);

            INSERT INTO "InventoryMovementFacts" ("ArtikalId", "Datum", "TipPromene", "Kolicina") VALUES
                (@inferredNegative, @today, 'Ulaz robe', 10),
                (@inferredNegative, @yesterday, 'Prenos izlaz', 2),
                (@restock, @today, 'Ulaz robe', 8),
                (@restock, @today, 'Prenos ulaz', 3),
                (@restock, @today, 'Prenos izlaz', 2),
                (@restock, @today, 'Povrat kupca', 1),
                (@missingBaseline, @yesterday, 'Ulaz robe', 4);
            """, connection);
        schema.Parameters.AddWithValue("observedZero", ObservedZeroArticleId);
        schema.Parameters.AddWithValue("observedNegative", ObservedNegativeArticleId);
        schema.Parameters.AddWithValue("inferredNegative", InferredNegativeArticleId);
        schema.Parameters.AddWithValue("missingBaseline", MissingBaselineArticleId);
        schema.Parameters.AddWithValue("restock", RestockTransferReturnArticleId);
        schema.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, DateTime.UtcNow);
        schema.Parameters.AddWithValue("today", NpgsqlDbType.TimestampTz, DateTime.SpecifyKind(utcToday.AddHours(12), DateTimeKind.Utc));
        schema.Parameters.AddWithValue("yesterday", NpgsqlDbType.TimestampTz, DateTime.SpecifyKind(yesterday.AddHours(12), DateTimeKind.Utc));
        schema.Parameters.AddWithValue("twoDaysAgo", NpgsqlDbType.TimestampTz, DateTime.SpecifyKind(twoDaysAgo.AddHours(12), DateTimeKind.Utc));
        await schema.ExecuteNonQueryAsync();
    }

    private async Task<InventoryRow> ReadInventoryAsync(
        IntelligenceTestDatabase database,
        int articleId,
        DateOnly date)
    {
        await using var connection = await _fixture.OpenConnectionAsync(database);
        await using var command = new NpgsqlCommand("""
            SELECT stock_qty, days_of_cover, stockout_days, low_stock_days, dead_stock_risk
            FROM analytics_intel.vw_inventory_risk_signals_v1
            WHERE article_id = @articleId
              AND date = @date
            """, connection);
        command.Parameters.AddWithValue("articleId", articleId);
        command.Parameters.AddWithValue("date", date);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Missing inventory signal for article {articleId} on {date}.");
        return new InventoryRow(
            reader.IsDBNull(0) ? null : reader.GetDecimal(0),
            reader.IsDBNull(1) ? null : reader.GetDecimal(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetDecimal(4));
    }

    private async Task<int> ReadIntAsync(IntelligenceTestDatabase database, string sql)
    {
        await using var connection = await _fixture.OpenConnectionAsync(database);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed record InventoryRow(
        decimal? StockQty,
        decimal? DaysOfCover,
        int? StockoutDays,
        int? LowStockDays,
        decimal? DeadStockRisk);
}
