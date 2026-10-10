using System.Linq;
using Api.Services;
using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Migrations.AnalyticsDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Workers;
using Xunit;

namespace Api.Tests;

// SalesLineFacts is keyed by the source receipt line (SaleId, SourceTableKey, SourceLineId).
// A receipt can carry the same product on several prodaja_stavke lines; each must stay a
// separate fact row and re-imports must never double them.
public sealed class SalesLineFactSourceLineKeyTests : IClassFixture<PostgresContainerFixture>
{
    private const string MigrationBeforeLineKey = "20260526171949_AddAnalyticsActionOutcomeTracking";
    private readonly PostgresContainerFixture _fixture;

    public SalesLineFactSourceLineKeyTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Resolve_PrefersAccessLineageAndFallsBackToTrendplusNamespace()
    {
        Assert.Equal(("access.prodaja_stavke", 9001L), SalesLineSourceIdentity.Resolve(17, "access.prodaja_stavke", 9001));
        Assert.Equal((SalesLineSourceIdentity.TrendplusLineNamespace, 17L), SalesLineSourceIdentity.Resolve(17, null, null));
        Assert.Equal((SalesLineSourceIdentity.TrendplusLineNamespace, 17L), SalesLineSourceIdentity.Resolve(17, "  ", 9001));
        Assert.Equal(((string?)null, (long?)null), SalesLineSourceIdentity.Resolve(0, null, null));
    }

    [Fact]
    public void MapSalesLineFacts_KeepsRepeatedProductAsSeparateLines()
    {
        var facts = AccessImportService.MapSalesLineFacts(new[]
        {
            new AccessImportService.SaleLineSourceSyncRow(11, 500, 42, 1, 1000m, 600m, "access.prodaja_stavke", 7001),
            new AccessImportService.SaleLineSourceSyncRow(12, 500, 42, 2, 950m, 600m, "access.prodaja_stavke", 7002),
        });

        Assert.Equal(2, facts.Count);
        Assert.All(facts, f => Assert.Equal(42, f.ProductId));
        Assert.Equal(new long?[] { 7001, 7002 }, facts.Select(f => f.SourceLineId).ToArray());
        Assert.Equal(3, facts.Sum(f => f.Qty));
        Assert.Equal(2900m, facts.Sum(f => f.LineTotal));
    }

    [Fact]
    public void OutboxSourceLineQueue_GivesEachRepeatedProductLineItsOwnSourceLine()
    {
        var queues = OutboxProcessorWorker.BuildSourceLineQueues(new[]
        {
            new OutboxProcessorWorker.SaleSourceLine(21, 42, 600m, null, null),
            new OutboxProcessorWorker.SaleSourceLine(20, 42, 550m, null, null),
            new OutboxProcessorWorker.SaleSourceLine(22, 43, 300m, null, null),
        });

        var first = OutboxProcessorWorker.DequeueSourceLine(queues, 42);
        var second = OutboxProcessorWorker.DequeueSourceLine(queues, 42);
        var third = OutboxProcessorWorker.DequeueSourceLine(queues, 42);

        Assert.Equal(20, first!.Id);
        Assert.Equal(550m, first.NabavnaCena);
        Assert.Equal(21, second!.Id);
        Assert.Null(third);
        Assert.Equal(22, OutboxProcessorWorker.DequeueSourceLine(queues, 43)!.Id);
    }

    [Fact]
    public void KeyBySourceLineMigration_UsesOnlyIdempotentSql()
    {
        var migration = new KeySalesLineFactsBySourceLine { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
        var statements = SqlStatements(migration.UpOperations);

        Assert.NotEmpty(statements);
        Assert.All(statements, s => Assert.True(
            s.Contains("IF NOT EXISTS", StringComparison.Ordinal) || s.Contains("IF EXISTS", StringComparison.Ordinal),
            s));
        Assert.Contains(statements, s => s.Contains("ADD COLUMN IF NOT EXISTS \"SourceLineId\" bigint", StringComparison.Ordinal));
        Assert.Contains(statements, s => s.Contains("ADD COLUMN IF NOT EXISTS \"SourceTableKey\" character varying(128)", StringComparison.Ordinal));
        Assert.Contains(statements, s => s.StartsWith("CREATE UNIQUE INDEX IF NOT EXISTS \"UX_SalesLineFacts_SaleId_SourceLine\"", StringComparison.Ordinal)
                                         && s.Contains("WHERE \"SourceLineId\" IS NOT NULL", StringComparison.Ordinal));
        Assert.DoesNotContain(statements, s => s.Contains("UNIQUE", StringComparison.Ordinal) && s.Contains("IX_SalesLineFacts_SaleId_ProductId", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyIdempotencyMigration_NoLongerFailsOrEnforcesProductUniqueness()
    {
        var migration = new AddSalesLineFactIdempotency { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
        var statements = SqlStatements(migration.UpOperations);

        Assert.All(migration.UpOperations, op => Assert.IsType<SqlOperation>(op));
        Assert.DoesNotContain(statements, s => s.Contains("RAISE EXCEPTION", StringComparison.Ordinal));
        Assert.DoesNotContain(statements, s => s.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Migrations_ApplyOnRepeatedLines_AndReimportNeverDoublesRows()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_slf_line_key_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(connectionString).Options;
        await using (var db = new AnalyticsDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(MigrationBeforeLineKey);
        }

        // DataOrigin is added by startup script Database/Analytics/011_AddDataOriginColumns.sql.
        // Legitimate repeated (SaleId, ProductId) lines present before the key change,
        // exactly the state that used to abort 20260909190000.
        await ExecuteAsync(connectionString, """
            ALTER TABLE "SalesLineFacts" ADD COLUMN IF NOT EXISTS "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing';
            INSERT INTO "SalesLineFacts" ("SaleId","ProductId","Qty","UnitPrice","LineTotal","DataOrigin")
            VALUES (1, 42, 1, 1000, 1000, 'access'), (1, 42, 1, 1000, 1000, 'access');
            """);

        await using (var db = new AnalyticsDbContext(options))
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }

        Assert.Equal(2L, await ScalarAsync<long>(connectionString, """SELECT COUNT(*) FROM "SalesLineFacts" WHERE "SourceLineId" IS NULL"""));
        Assert.False(await ScalarAsync<bool>(connectionString,
            """SELECT indisunique FROM pg_index WHERE indexrelid = '"IX_SalesLineFacts_SaleId_ProductId"'::regclass"""));
        Assert.True(await ScalarAsync<bool>(connectionString,
            """SELECT indisunique FROM pg_index WHERE indexrelid = '"UX_SalesLineFacts_SaleId_SourceLine"'::regclass"""));

        var rows = AccessImportService.MapSalesLineFacts(new[]
        {
            new AccessImportService.SaleLineSourceSyncRow(11, 500, 42, 1, 1000m, 600m, "access.prodaja_stavke", 7001),
            new AccessImportService.SaleLineSourceSyncRow(12, 500, 42, 2, 950m, 600m, "access.prodaja_stavke", 7002),
        });

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await AccessImportService.ReplaceSalesLineFactsBulkAsync(
                connection, transaction, new[] { 500 }, rows, NullLogger.Instance, CancellationToken.None);
            await transaction.CommitAsync();
        }

        Assert.Equal(2L, await ScalarAsync<long>(connectionString, """SELECT COUNT(*) FROM "SalesLineFacts" WHERE "SaleId" = 500"""));
        Assert.Equal(3L, await ScalarAsync<long>(connectionString, """SELECT SUM("Qty")::bigint FROM "SalesLineFacts" WHERE "SaleId" = 500"""));
        Assert.Equal(2900m, await ScalarAsync<decimal>(connectionString, """SELECT SUM("LineTotal") FROM "SalesLineFacts" WHERE "SaleId" = 500"""));

        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connectionString, """
            INSERT INTO "SalesLineFacts" ("SaleId","ProductId","Qty","UnitPrice","LineTotal","DataOrigin","SourceTableKey","SourceLineId")
            VALUES (500, 42, 1, 1000, 1000, 'access', 'access.prodaja_stavke', 7001);
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);

        // Re-running the new migration's SQL on an already-migrated schema is a no-op.
        var rerun = new KeySalesLineFactsBySourceLine { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
        foreach (var op in rerun.UpOperations.Cast<SqlOperation>())
            await ExecuteAsync(connectionString, op.Sql);
    }

    private static string[] SqlStatements(IEnumerable<MigrationOperation> operations)
        => operations
            .OfType<SqlOperation>()
            .SelectMany(op => op.Sql.Split(';'))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }
}
