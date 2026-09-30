using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Api.Tests;

public sealed class SupplierDecisionSchemaReadinessIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private static readonly string[] SupplierDecisionMaterializedViews =
    [
        "mv_supplier_markdown_dependency_cache",
        "mv_supplier_decision_score_cache",
        "mv_supplier_recommendations_cache",
        "mv_supplier_decision_score_cache_90d",
        "mv_supplier_decision_score_cache_180d"
    ];

    private readonly PostgresContainerFixture _fixture;

    public SupplierDecisionSchemaReadinessIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SupplierDecisionRepair_SeedsPostgres_RepeatsIdempotently_AndPreservesRefreshPrerequisites()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_supplier_schema_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await SeedSupplierSchemaPrerequisitesAsync(connectionString);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Keep the analytics DB split from the default DB so the
                // compatibility layer is exercised rather than skipped.
                ["ConnectionStrings:DefaultConnection"] = _fixture.AdminConnectionString,
                ["ConnectionStrings:AnalyticsConnection"] = connectionString
            })
            .Build();

        using var services = new ServiceCollection()
            .AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(connectionString))
            .BuildServiceProvider();

        await DatabaseInitializer.EnsureAnalyticsSupplierDecisionSchemaAsync(
            services,
            configuration,
            NullLogger.Instance);
        await DatabaseInitializer.EnsureAnalyticsSupplierDecisionSchemaAsync(
            services,
            configuration,
            NullLogger.Instance);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Assert.True(await RelationExistsAsync(connection, "vw_vendor_sales_nivelacija"));
        Assert.True(await RelationExistsAsync(connection, "vw_nivelacija_kontrolna_grupa"));
        Assert.True(await RelationExistsAsync(connection, "vw_nivelacija_did"));
        Assert.True(await RelationExistsAsync(connection, "vw_supplier_fullprice_signals"));
        Assert.True(await RelationExistsAsync(connection, "vw_supplier_markdown_dependency"));
        Assert.True(await RelationExistsAsync(connection, "vw_supplier_decision_score"));
        Assert.True(await RelationExistsAsync(connection, "vw_supplier_recommendations"));

        foreach (var column in new[]
        {
            "change_percent_qty_semantic",
            "change_percent_revenue_semantic",
            "qty_baseline_reason",
            "revenue_baseline_reason"
        })
        {
            Assert.True(
                await ColumnExistsAsync(connection, "vw_vendor_sales_nivelacija", column),
                $"Missing semantic column {column} after repeated supplier repair.");
        }

        foreach (var column in new[] { "price_event_id", "did_revenue", "did_qty" })
        {
            Assert.True(
                await ColumnExistsAsync(connection, "vw_nivelacija_did", column),
                $"Missing DiD column {column} after repeated supplier repair.");
        }

        foreach (var materializedView in SupplierDecisionMaterializedViews)
        {
            Assert.True(await IsPopulatedMaterializedViewAsync(connection, materializedView));

            // A plain refresh is the worker-safe fallback for expression-indexed
            // caches and proves that every startup-created MV has valid sources.
            await ExecuteAsync(
                connection,
                $"REFRESH MATERIALIZED VIEW public.\"{materializedView}\";");
        }

        // The refresh worker may use CONCURRENTLY only when PostgreSQL exposes a
        // valid, non-partial, non-expression unique index. The markdown cache
        // intentionally uses an expression index, so it must use the blocking
        // fallback; the other caches can refresh concurrently.
        Assert.False(await CanRefreshConcurrentlyAsync(
            connection,
            "mv_supplier_markdown_dependency_cache"));
        Assert.True(await CanRefreshConcurrentlyAsync(
            connection,
            "mv_supplier_decision_score_cache"));
        Assert.True(await CanRefreshConcurrentlyAsync(
            connection,
            "mv_supplier_recommendations_cache"));
        Assert.True(await CanRefreshConcurrentlyAsync(
            connection,
            "mv_supplier_decision_score_cache_90d"));
        Assert.True(await CanRefreshConcurrentlyAsync(
            connection,
            "mv_supplier_decision_score_cache_180d"));
    }

    [Fact]
    public async Task SupplierMlRanking_IsolatedTransactionSafetyIsExplicit_AndStartupDoesNotExecuteIt()
    {
        var sql = ReadRepoFile("Database/Analytics/015_AddSupplierMlRanking.sql");
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");

        Assert.Contains("CREATE INDEX CONCURRENTLY", sql);
        Assert.Contains("CREATE MATERIALIZED VIEW CONCURRENTLY", sql);
        Assert.Contains(
            "The optional ML overlay from 015_AddSupplierMlRanking.sql remains separate.",
            initializer);
        Assert.DoesNotContain(
            "ExecuteSqlFileAsync(connectionString, \"Database/Analytics/015_AddSupplierMlRanking.sql\"",
            initializer);

        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_supplier_ml_isolated_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE supplier_ml_transaction_probe (id integer NOT NULL);");

        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            """
            CREATE UNIQUE INDEX CONCURRENTLY ux_supplier_ml_transaction_probe
                ON supplier_ml_transaction_probe (id);
            """,
            connection,
            transaction);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());
        Assert.Equal("25001", exception.SqlState);
        await transaction.RollbackAsync();
    }

    private static async Task SeedSupplierSchemaPrerequisitesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" varchar(150) PRIMARY KEY,
                "ProductVersion" varchar(32) NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "ProductsDim" (
                "ProductKey" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "ProductId" integer NOT NULL,
                "PLU" varchar(100),
                "ProductName" varchar(300) NOT NULL DEFAULT '',
                "Category" varchar(200),
                "SubCategory" varchar(200),
                "FootwearTypeId" integer,
                "SupplierId" integer,
                "SeasonId" integer,
                "PurchasePrice" numeric(18,2),
                "PurchasePriceRsd" numeric(18,2),
                "FirstSalePrice" numeric(18,2),
                "SalePrice" numeric(18,2),
                "Kolicina" integer,
                "MinimalnaKolicina" integer,
                "Velicina" varchar(100),
                "Boja" varchar(100),
                "Materijal" varchar(100),
                "Timestamp" timestamptz NOT NULL,
                "DataOrigin" varchar(32) NOT NULL DEFAULT 'existing'
            );

            CREATE TABLE IF NOT EXISTS "SuppliersDim" (
                "SupplierKey" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SupplierId" integer NOT NULL,
                "Naziv" varchar(300) NOT NULL DEFAULT '',
                "Adresa" varchar(500),
                "Telefon" varchar(50),
                "Napomena" varchar(500),
                "DataOrigin" varchar(32) NOT NULL DEFAULT 'existing',
                "UpdatedAt" timestamptz NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "SalesFacts" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SaleId" integer NOT NULL,
                "BrojRacuna" varchar(100) NOT NULL,
                "SaleTimestampUtc" timestamptz NOT NULL,
                "StoreId" integer NOT NULL,
                "PaymentType" varchar(100) NOT NULL,
                "TotalAmount" numeric(18,2) NOT NULL,
                "TotalUnits" integer NOT NULL,
                "TotalLines" integer NOT NULL,
                "DataOrigin" varchar(32) NOT NULL DEFAULT 'existing'
            );

            CREATE TABLE IF NOT EXISTS "SalesLineFacts" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SaleId" integer NOT NULL,
                "ProductId" integer NOT NULL,
                "Qty" integer NOT NULL,
                "UnitPrice" numeric(18,2) NOT NULL,
                "LineTotal" numeric(18,2) NOT NULL,
                "NabavnaCena" numeric(18,2),
                "DataOrigin" varchar(32) NOT NULL DEFAULT 'existing'
            );

            CREATE TABLE IF NOT EXISTS "InventoryMovementFacts" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SourceId" integer NOT NULL,
                "TipPromene" varchar(100) NOT NULL,
                "Datum" timestamptz NOT NULL,
                "ArtikalId" integer,
                "Kolicina" integer,
                "StaraProdajnaCena" numeric(18,2),
                "NovaProdajnaCena" numeric(18,2),
                "Iznos" numeric(18,2) NOT NULL DEFAULT 0,
                "StoreId" integer,
                "DobavljacId" integer,
                "BrojDokumenta" varchar(100),
                "KorisnikIme" varchar(200),
                "DataOrigin" varchar(32) NOT NULL DEFAULT 'existing'
            );

            INSERT INTO "ProductsDim" (
                "ProductId", "PLU", "ProductName", "Category", "SupplierId",
                "PurchasePrice", "PurchasePriceRsd", "SalePrice", "Kolicina",
                "MinimalnaKolicina", "Timestamp"
            )
            VALUES (1001, 'SKU-1001', 'Fixture shoe', 'Obuca', 501, 40, 40, 80, 12, 2, now())
            ON CONFLICT DO NOTHING;

            INSERT INTO "SuppliersDim" ("SupplierId", "Naziv", "UpdatedAt")
            VALUES (501, 'Fixture supplier', now())
            ON CONFLICT DO NOTHING;

            INSERT INTO "SalesFacts" (
                "Id", "SaleId", "BrojRacuna", "SaleTimestampUtc", "StoreId",
                "PaymentType", "TotalAmount", "TotalUnits", "TotalLines"
            )
            VALUES (1, 1, 'FIXTURE-1', '2026-09-01T12:00:00Z', 7, 'Cash', 80, 1, 1)
            ON CONFLICT DO NOTHING;

            INSERT INTO "SalesLineFacts" (
                "Id", "SaleId", "ProductId", "Qty", "UnitPrice", "LineTotal", "NabavnaCena"
            )
            VALUES (1, 1, 1001, 1, 80, 80, 40)
            ON CONFLICT DO NOTHING;

            INSERT INTO "InventoryMovementFacts" (
                "SourceId", "TipPromene", "Datum", "ArtikalId", "Kolicina",
                "StaraProdajnaCena", "NovaProdajnaCena", "Iznos", "DobavljacId"
            )
            VALUES (1, 'Nivelacija', '2026-09-10T12:00:00Z', 1001, 1, 80, 70, 70, 501)
            ON CONFLICT DO NOTHING;

            DROP MATERIALIZED VIEW IF EXISTS mv_daily_sales_facts;
            CREATE MATERIALIZED VIEW mv_daily_sales_facts AS
            SELECT
                DATE '2026-09-01' AS day,
                1001::integer AS article_id,
                1::numeric AS units,
                80::numeric AS revenue
            WHERE FALSE;
            """);
    }

    private static async Task<bool> RelationExistsAsync(
        NpgsqlConnection connection,
        string relationName)
    {
        return await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public'
                  AND c.relname = @relationName
                  AND c.relkind IN ('v', 'm')
            );
            """,
            ("relationName", relationName));
    }

    private static async Task<bool> ColumnExistsAsync(
        NpgsqlConnection connection,
        string relationName,
        string columnName)
    {
        return await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @relationName
                  AND column_name = @columnName
            );
            """,
            ("relationName", relationName),
            ("columnName", columnName));
    }

    private static async Task<bool> IsPopulatedMaterializedViewAsync(
        NpgsqlConnection connection,
        string relationName)
    {
        return await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_matviews
                WHERE schemaname = 'public'
                  AND matviewname = @relationName
                  AND ispopulated
            );
            """,
            ("relationName", relationName));
    }

    private static async Task<bool> CanRefreshConcurrentlyAsync(
        NpgsqlConnection connection,
        string relationName)
    {
        return await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_class mv
                JOIN pg_namespace ns ON ns.oid = mv.relnamespace
                JOIN pg_index idx ON idx.indrelid = mv.oid
                WHERE ns.nspname = 'public'
                  AND mv.relname = @relationName
                  AND mv.relkind = 'm'
                  AND idx.indisunique
                  AND idx.indisvalid
                  AND idx.indpred IS NULL
                  AND idx.indexprs IS NULL
            );
            """,
            ("relationName", relationName));
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Expected scalar result for SQL: {sql}"));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
