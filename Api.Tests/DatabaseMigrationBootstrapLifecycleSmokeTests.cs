using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.Database;
using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class DatabaseMigrationBootstrapLifecycleSmokeTests
{
    private readonly IntelligencePostgresFixture _fixture;

    public DatabaseMigrationBootstrapLifecycleSmokeTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FreshBootstrapRepeatAndRestartRemainIdempotent()
    {
        var database = await _fixture.CreateDatabaseAsync("bootstrap_lifecycle");
        var connectionString = database.ConnectionString;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:AnalyticsConnection"] = connectionString,
                ["DatabaseInitialization:FailFast"] = "true",
                ["DatabaseInitialization:RunFullSupplierDecisionHubOnStartup"] = "false",
            })
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger("DatabaseMigrationBootstrapLifecycleSmoke");

        await _fixture.RunFreshBootstrapAsync(
            connectionString,
            _ => RunBootstrapAsync(connectionString, configuration, logger));

        await AssertProvenanceColumnContractAsync(connectionString);
        var persistedSale = await InsertSaleLineWithEfAsync(
            connectionString,
            receiptNumber: $"RQ628-{Guid.NewGuid():N}",
            articleId: null,
            headerId: null,
            purchaseCost: 45.67m,
            provenance: "source_sale_line");

        // Reproduce a database whose EF migration history is intact while the
        // physical column was removed or lost by an out-of-band restore.
        await using (var driftConnection = await _fixture.OpenConnectionAsync(database))
        {
            await using var dropColumn = new NpgsqlCommand(
                "ALTER TABLE public.prodaja_stavke DROP COLUMN purchase_cost_provenance;",
                driftConnection);
            await dropColumn.ExecuteNonQueryAsync();
            Assert.False(await ColumnExistsAsync(driftConnection, "prodaja_stavke", "purchase_cost_provenance"));
            Assert.True(await MigrationExistsAsync(driftConnection, "20261010160000_AddPurchaseCostProvenance"));

            var missingColumn = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var query = new NpgsqlCommand(
                    "SELECT purchase_cost_provenance FROM public.prodaja_stavke WHERE id = @id;",
                    driftConnection);
                query.Parameters.AddWithValue("id", persistedSale.LineId);
                await query.ExecuteScalarAsync();
            });
            Assert.Equal("42703", missingColumn.SqlState);
        }

        // Simulate two independent processes starting against the same drifted database.
        // The initializer's PostgreSQL advisory lock serializes their full startup path.
        await Task.WhenAll(
            _fixture.RunOwnedBootstrapRepeatAsync(
                database,
                _ => RunBootstrapAsync(connectionString, configuration, logger)),
            _fixture.RunOwnedBootstrapRepeatAsync(
                database,
                _ => RunBootstrapAsync(connectionString, configuration, logger)));

        await using (var repairedConnection = await _fixture.OpenConnectionAsync(database))
        {
            Assert.True(await ColumnExistsAsync(repairedConnection, "prodaja_stavke", "purchase_cost_provenance"));
            Assert.Equal(persistedSale.PurchaseCost, await ScalarAsync<decimal>(
                repairedConnection,
                "SELECT nabavna_cena FROM public.prodaja_stavke WHERE id = @id;",
                ("id", persistedSale.LineId)));
        }

        await AssertProvenanceColumnContractAsync(connectionString);
        var preservedLegacyLine = await ReadSaleLineWithEfAsync(connectionString, persistedSale.LineId);
        Assert.Equal(45.67m, preservedLegacyLine.PurchaseCost);
        Assert.Null(preservedLegacyLine.Provenance);

        var newSale = await InsertSaleLineWithEfAsync(
            connectionString,
            receiptNumber: string.Empty,
            articleId: persistedSale.ArticleId,
            headerId: persistedSale.HeaderId,
            purchaseCost: 72.10m,
            provenance: "source_sale_line");
        var analyticsProjection = await ReadSaleLineWithEfAsync(connectionString, newSale.LineId);
        Assert.Equal(72.10m, analyticsProjection.PurchaseCost);
        Assert.Equal("source_sale_line", analyticsProjection.Provenance);

        await using (var wrongType = await _fixture.OpenConnectionAsync(database))
        await using (var command = new NpgsqlCommand(
                         "ALTER TABLE public.prodaja_stavke ALTER COLUMN purchase_cost_provenance TYPE text;",
                         wrongType))
        {
            await command.ExecuteNonQueryAsync();
        }
        var typeMismatch = await PurchaseCostProvenanceSchemaContract.ValidateAsync(connectionString);
        Assert.NotNull(typeMismatch);
        Assert.Contains("expected nullable character varying(64)", typeMismatch, StringComparison.Ordinal);
        var typeFailure = await Assert.ThrowsAsync<DatabaseInitializationFailureException>(
            () => RunBootstrapAsync(connectionString, configuration, logger));
        Assert.Contains("incompatible schema", typeFailure.ToString(), StringComparison.OrdinalIgnoreCase);

        await using (var restoreType = await _fixture.OpenConnectionAsync(database))
        await using (var command = new NpgsqlCommand(
                         "ALTER TABLE public.prodaja_stavke ALTER COLUMN purchase_cost_provenance TYPE character varying(64) USING purchase_cost_provenance::character varying(64);",
                         restoreType))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var wrongNullability = await _fixture.OpenConnectionAsync(database))
        {
            await using var command = new NpgsqlCommand(
                "UPDATE public.prodaja_stavke SET purchase_cost_provenance = 'unknown_provenance' WHERE purchase_cost_provenance IS NULL; ALTER TABLE public.prodaja_stavke ALTER COLUMN purchase_cost_provenance SET NOT NULL;",
                wrongNullability);
            await command.ExecuteNonQueryAsync();
        }
        var nullabilityMismatch = await PurchaseCostProvenanceSchemaContract.ValidateAsync(connectionString);
        Assert.NotNull(nullabilityMismatch);
        Assert.Contains("nullable=False", nullabilityMismatch, StringComparison.Ordinal);
        var nullabilityFailure = await Assert.ThrowsAsync<DatabaseInitializationFailureException>(
            () => RunBootstrapAsync(connectionString, configuration, logger));
        Assert.Contains("incompatible schema", nullabilityFailure.ToString(), StringComparison.OrdinalIgnoreCase);

        await using (var restoreNullability = await _fixture.OpenConnectionAsync(database))
        {
            await using var command = new NpgsqlCommand(
                "ALTER TABLE public.prodaja_stavke ALTER COLUMN purchase_cost_provenance DROP NOT NULL; UPDATE public.prodaja_stavke SET purchase_cost_provenance = NULL WHERE id = @id;",
                restoreNullability);
            command.Parameters.AddWithValue("id", persistedSale.LineId);
            await command.ExecuteNonQueryAsync();
        }

        await _fixture.RunOwnedBootstrapRepeatAsync(
            database,
            _ => RunBootstrapAsync(connectionString, configuration, logger));

        await using var connection = await _fixture.OpenConnectionAsync(database);

        Assert.True(await TableExistsAsync(connection, "__EFMigrationsHistory"));
        Assert.True(await TableExistsAsync(connection, "Artikli"));
        Assert.True(await TableExistsAsync(connection, "SalesFacts"));
        Assert.True(await TableExistsAsync(connection, "analytics_data_quality_history"));
        Assert.True(await TableExistsAsync(connection, "vw_vendor_sales_nivelacija"));
        Assert.True(await TableExistsAsync(connection, "vw_nivelacija_kontrolna_grupa"));
        Assert.True(await TableExistsAsync(connection, "vw_nivelacija_did"));
        Assert.True(await ColumnExistsAsync(connection, "vw_vendor_sales_nivelacija", "change_percent_revenue_semantic"));
        Assert.True(await ColumnExistsAsync(connection, "vw_nivelacija_did", "did_revenue"));
        Assert.True(await ColumnExistsAsync(connection, "vw_nivelacija_did", "did_qty"));

        Assert.True(await MigrationExistsAsync(connection, "20251224163406_InitialPostgreSQL"));
        Assert.True(await MigrationExistsAsync(connection, "20251229125031_AddProductsDimTimestamp"));
        Assert.Equal(0L, await DuplicateIndexCountAsync(connection));

        // Full bootstrap executes vector-backed Open Product Training SQL, so
        // the supported pgvector fixture is an explicit lifecycle dependency.
        Assert.True(await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_extension
                WHERE extname = 'vector'
            );
            """));

        await using (var ddlUnavailable = await _fixture.OpenConnectionAsync(database))
        await using (var drop = new NpgsqlCommand(
                         "ALTER TABLE public.prodaja_stavke DROP COLUMN purchase_cost_provenance;",
                         ddlUnavailable))
        {
            await drop.ExecuteNonQueryAsync();
        }
        var readOnlyConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Options = "-c default_transaction_read_only=on"
        }.ConnectionString;
        var ddlFailure = await Assert.ThrowsAsync<PostgresException>(
            () => PurchaseCostProvenanceSchemaContract.EnsureColumnAsync(readOnlyConnectionString));
        Assert.Equal("25006", ddlFailure.SqlState);
        Assert.False(await PurchaseCostProvenanceColumnExistsAsync(connectionString));
    }

    private static async Task AssertProvenanceColumnContractAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT format_type(attribute.atttypid, attribute.atttypmod), NOT attribute.attnotnull
            FROM pg_attribute AS attribute
            JOIN pg_class AS relation ON relation.oid = attribute.attrelid
            JOIN pg_namespace AS schema ON schema.oid = relation.relnamespace
            WHERE schema.nspname = 'public'
              AND relation.relname = 'prodaja_stavke'
              AND attribute.attname = 'purchase_cost_provenance'
              AND attribute.attnum > 0
              AND NOT attribute.attisdropped;
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "PostgreSQL catalog must expose purchase_cost_provenance.");
        Assert.Equal("character varying(64)", reader.GetString(0));
        Assert.True(reader.GetBoolean(1));
    }

    private static async Task<(int ArticleId, int HeaderId, int LineId, decimal PurchaseCost)> InsertSaleLineWithEfAsync(
        string connectionString,
        string receiptNumber,
        int? articleId,
        int? headerId,
        decimal purchaseCost,
        string? provenance)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new TrendplusDbContext(options);

        Artikli article;
        if (articleId is null)
        {
            article = new Artikli { Naziv = "RQ628 schema contract", UpdatedAt = DateTime.UtcNow };
            context.Artikli.Add(article);
        }
        else
        {
            article = await context.Artikli.SingleAsync(x => x.Id == articleId.Value);
        }

        ProdajaZaglavlje header;
        if (headerId is null)
        {
            header = new ProdajaZaglavlje
            {
                BrojRacuna = receiptNumber,
                DatumProdaje = DateTime.UtcNow,
                DataOrigin = "existing"
            };
            context.ProdajaZaglavlja.Add(header);
        }
        else
        {
            header = await context.ProdajaZaglavlja.SingleAsync(x => x.Id == headerId.Value);
        }

        await context.SaveChangesAsync();
        var line = new ProdajaStavka
        {
            IdProdaja = header.Id,
            IdArtikal = article.Id,
            Kolicina = 2,
            Cena = 100m,
            NabavnaCena = purchaseCost,
            PurchaseCostProvenance = provenance
        };
        context.ProdajaStavke.Add(line);
        await context.SaveChangesAsync();
        return (article.Id, header.Id, line.Id, purchaseCost);
    }

    private static async Task<(decimal? PurchaseCost, string? Provenance)> ReadSaleLineWithEfAsync(
        string connectionString,
        int lineId)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new TrendplusDbContext(options);
        var projection = await context.ProdajaStavke
            .AsNoTracking()
            .Where(line => line.Id == lineId)
            .Select(line => new { line.NabavnaCena, line.PurchaseCostProvenance })
            .SingleAsync();
        return (projection.NabavnaCena, projection.PurchaseCostProvenance);
    }

    private static async Task<bool> PurchaseCostProvenanceColumnExistsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relname = 'prodaja_stavke' AND a.attname = 'purchase_cost_provenance' AND a.attnum > 0 AND NOT a.attisdropped);",
            connection);
        return (bool)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Could not inspect PostgreSQL catalog."));
    }

    private static async Task RunBootstrapAsync(
        string connectionString,
        IConfiguration configuration,
        ILogger logger)
    {
        using var services = new ServiceCollection()
            .AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(connectionString))
            .AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(connectionString))
            .BuildServiceProvider();

        var outcome = await DatabaseInitializer.InitializeDatabasesAsync(
            services,
            configuration,
            logger);
        Assert.False(outcome.CompletedWithErrors);
        Assert.Null(outcome.FailureCategory);
        Assert.Null(outcome.FailureStage);
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string tableName)
    {
        return await ScalarAsync<bool>(
            connection,
            """
            SELECT to_regclass(format('public.%I', @tableName)) IS NOT NULL;
            """,
            ("tableName", tableName));
    }

    private static async Task<bool> MigrationExistsAsync(NpgsqlConnection connection, string migrationId)
    {
        return await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = @migrationId
            );
            """,
            ("migrationId", migrationId));
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
                WHERE table_schema = ANY (current_schemas(FALSE))
                  AND table_name = @relationName
                  AND column_name = @columnName
            );
            """,
            ("relationName", relationName),
            ("columnName", columnName));
    }

    private static async Task<long> DuplicateIndexCountAsync(NpgsqlConnection connection)
    {
        return await ScalarAsync<long>(
            connection,
            """
            SELECT COUNT(*)
            FROM (
                SELECT indexname
                FROM pg_indexes
                WHERE schemaname = 'public'
                GROUP BY indexname
                HAVING COUNT(*) > 1
            ) duplicate_indexes;
            """);
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
}
