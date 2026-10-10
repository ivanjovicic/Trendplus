using System.Diagnostics;
using System.Reflection;
using Api.Config;
using Api.Models;
using Api.Services;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportHeartbeatPostgresIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AccessImportHeartbeatPostgresIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HeartbeatAdvancesDuringLongPostgresImportTransaction_WithoutBatchRowLock()
    {
        var connectionString = await CreateDatabaseAsync("tp_access_heartbeat");
        if (connectionString is null)
            return;

        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync();
        await setup.ExecuteNonQueryAsync("""
            CREATE TABLE "DataImportBatches" (
                "Id" bigint PRIMARY KEY,
                "StartedAtUtc" timestamp with time zone NOT NULL,
                "Status" character varying(32) NOT NULL,
                "CompletedAtUtc" timestamp with time zone,
                "LastHeartbeatUtc" timestamp with time zone,
                "CurrentStep" character varying(64),
                "CurrentTable" character varying(300),
                "DurationSeconds" integer,
                "RowsRead" integer NOT NULL DEFAULT 0,
                "RowsAccepted" integer NOT NULL DEFAULT 0,
                "RowsWritten" integer NOT NULL DEFAULT 0,
                "RowsSkippedStale" integer NOT NULL DEFAULT 0,
                "ProgressPercent" integer NOT NULL DEFAULT 0,
                "TotalImported" integer NOT NULL DEFAULT 0,
                "TotalUpdated" integer NOT NULL DEFAULT 0,
                "TotalErrors" integer NOT NULL DEFAULT 0,
                "SummaryJson" text
            );
            CREATE TABLE "ImportProbe" ("Id" integer PRIMARY KEY, "Value" integer NOT NULL);
            INSERT INTO "DataImportBatches" ("Id", "StartedAtUtc", "Status", "LastHeartbeatUtc", "CurrentStep", "CurrentTable")
            VALUES (22, NOW(), 'running', NOW() - INTERVAL '1 minute', 'starting', 'all');
            INSERT INTO "ImportProbe" ("Id", "Value") VALUES (1, 0);
            """);

        await using var importDb = CreateDbContext(connectionString);
        var service = CreateService(importDb);
        SetProgressContext(service, batchId: 22, step: "tblDobavljaci", table: "tblDobavljaci");

        var longImport = RetriableDbContextTransaction.ExecuteAsync(importDb, async ct =>
        {
            await importDb.Database.ExecuteSqlRawAsync("UPDATE \"ImportProbe\" SET \"Value\" = 1 WHERE \"Id\" = 1;", ct);
            await Task.Delay(TimeSpan.FromMilliseconds(1200), ct);
        }, CancellationToken.None);

        await Task.Delay(150);
        var stopwatch = Stopwatch.StartNew();
        await InvokePersistProgressAsync(service, "integration-heartbeat", force: true);
        stopwatch.Stop();
        await longImport;

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Heartbeat took {stopwatch.Elapsed}.");

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();
        var heartbeat = await verify.ExecuteScalarAsync<DateTime>("SELECT \"LastHeartbeatUtc\" FROM \"DataImportBatches\" WHERE \"Id\" = 22;");
        var probeValue = await verify.ExecuteScalarAsync<int>("SELECT \"Value\" FROM \"ImportProbe\" WHERE \"Id\" = 1;");
        Assert.True(heartbeat > DateTime.UtcNow.AddSeconds(-10));
        Assert.Equal(1, probeValue);
    }

    [Fact]
    public async Task HeartbeatSkipsContendedBatchRowWithinBoundedTimeout()
    {
        var connectionString = await CreateDatabaseAsync("tp_access_heartbeat_lock");
        if (connectionString is null)
            return;

        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync();
        await setup.ExecuteNonQueryAsync("""
            CREATE TABLE "DataImportBatches" (
                "Id" bigint PRIMARY KEY,
                "StartedAtUtc" timestamp with time zone NOT NULL,
                "Status" character varying(32) NOT NULL,
                "CompletedAtUtc" timestamp with time zone,
                "LastHeartbeatUtc" timestamp with time zone,
                "CurrentStep" character varying(64),
                "CurrentTable" character varying(300),
                "DurationSeconds" integer,
                "RowsRead" integer NOT NULL DEFAULT 0,
                "RowsAccepted" integer NOT NULL DEFAULT 0,
                "RowsWritten" integer NOT NULL DEFAULT 0,
                "RowsSkippedStale" integer NOT NULL DEFAULT 0,
                "ProgressPercent" integer NOT NULL DEFAULT 0,
                "TotalImported" integer NOT NULL DEFAULT 0,
                "TotalUpdated" integer NOT NULL DEFAULT 0,
                "TotalErrors" integer NOT NULL DEFAULT 0,
                "SummaryJson" text
            );
            INSERT INTO "DataImportBatches" ("Id", "StartedAtUtc", "Status", "LastHeartbeatUtc", "CurrentStep", "CurrentTable")
            VALUES (22, NOW(), 'running', NOW() - INTERVAL '1 minute', 'starting', 'all');
            """);

        await using var importDb = CreateDbContext(connectionString);
        var service = CreateService(importDb);
        SetProgressContext(service, batchId: 22, step: "tblDobavljaci", table: "tblDobavljaci");

        await using var locker = new NpgsqlConnection(connectionString);
        await locker.OpenAsync();
        await using var lockTransaction = await locker.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand(
                         "SELECT \"Id\" FROM \"DataImportBatches\" WHERE \"Id\" = 22 FOR UPDATE;",
                         locker,
                         lockTransaction))
        {
            await lockCommand.ExecuteScalarAsync();
        }

        var stopwatch = Stopwatch.StartNew();
        await InvokePersistProgressAsync(service, "intentional-lock-contention", force: true);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Heartbeat waited too long: {stopwatch.Elapsed}.");
        await lockTransaction.RollbackAsync();
    }

    [Fact]
    public async Task RetriableImportTransactionRollsBackOnCancellation()
    {
        var connectionString = await CreateDatabaseAsync("tp_access_heartbeat_cancel");
        if (connectionString is null)
            return;

        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync();
        await setup.ExecuteNonQueryAsync("CREATE TABLE \"ImportProbe\" (\"Id\" integer PRIMARY KEY, \"Value\" integer NOT NULL);");

        await using var importDb = CreateDbContext(connectionString);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RetriableDbContextTransaction.ExecuteAsync(
            importDb,
            async ct =>
            {
                await importDb.Database.ExecuteSqlRawAsync("INSERT INTO \"ImportProbe\" (\"Id\", \"Value\") VALUES (1, 1);", ct);
                cancellation.Cancel();
                ct.ThrowIfCancellationRequested();
            },
            cancellation.Token));

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();
        var count = await verify.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM \"ImportProbe\";");
        Assert.Equal(0, count);
    }

    private async Task<string?> CreateDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable)
            return null;

        return await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
    }

    private static TrendplusDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TrendplusDbContext(options);
    }

    private static AccessImportService CreateService(TrendplusDbContext db)
        => new(
            trendDb: db,
            analyticsDb: null!,
            logger: NullLogger<AccessImportService>.Instance,
            options: Options.Create(new AccessImportOptions
            {
                HeartbeatIntervalSeconds = 1,
                StatusUpdateThrottleSeconds = 1
            }));

    private static void SetProgressContext(AccessImportService service, long batchId, string step, string table)
    {
        SetPrivateField(service, "_activeBatchId", batchId);
        SetPrivateField(service, "_activeBatchResult", new AccessImportRunResponse { BatchId = batchId, Status = "running" });
        SetPrivateField(service, "_activeBatchStep", step);
        SetPrivateField(service, "_activeBatchTable", table);
    }

    private static async Task InvokePersistProgressAsync(AccessImportService service, string reason, bool force)
    {
        var method = typeof(AccessImportService).GetMethod(
            "PersistBatchProgressAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task?)method!.Invoke(service, new object[] { reason, force, CancellationToken.None });
        Assert.NotNull(task);
        await task!;
    }

    private static void SetPrivateField<T>(AccessImportService service, string name, T value)
        => typeof(AccessImportService)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, value);
}

internal static class NpgsqlTestCommandExtensions
{
    public static async Task<int> ExecuteNonQueryAsync(this NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync();
    }

    public static async Task<T> ExecuteScalarAsync<T>(this NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        if (value is null || value is DBNull)
            throw new InvalidOperationException("The PostgreSQL test scalar was null.");

        return (T)Convert.ChangeType(value, typeof(T));
    }
}
