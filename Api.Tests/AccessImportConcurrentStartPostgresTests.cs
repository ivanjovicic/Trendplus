using System.Reflection;
using Api.Config;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Api.Tests;

/// <summary>
/// RQ673: Access import batch creation is serialized across instances with
/// pg_advisory_xact_lock(AccessImportBatchCreateAdvisoryLockKey); the active-batch check and insert share one transaction.
/// </summary>
public sealed class AccessImportConcurrentStartPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AccessImportConcurrentStartPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresRequiredFact]
    public async Task ParallelStarts_OnSeparateConnections_CreateExactlyOneBatch_AndShareItsId()
    {
        var connectionString = await CreateDatabaseAsync();
        if (connectionString is null)
            return;

        var sourceFile = CreateTempSourceFile();
        try
        {
            await using var dbA = CreateDbContext(connectionString);
            await using var dbB = CreateDbContext(connectionString);
            var serviceA = CreateService(dbA);
            var serviceB = CreateService(dbB);

            // Hold the dedicated key from a third session so both requests are provably blocked on it at the same time.
            await using var gate = new NpgsqlConnection(connectionString);
            await gate.OpenAsync();
            await ExecuteAsync(gate, $"SELECT pg_advisory_lock({LockKey})");

            var taskA = Task.Run(() => TryCreateBatchAsync(serviceA, sourceFile));
            var taskB = Task.Run(() => TryCreateBatchAsync(serviceB, sourceFile));

            await WaitForAdvisoryWaitersAsync(gate, expectedWaiters: 2);
            await ExecuteAsync(gate, $"SELECT pg_advisory_unlock({LockKey})");

            var results = await Task.WhenAll(taskA, taskB);

            Assert.Single(results, r => r.Created);
            Assert.Single(results, r => !r.Created);
            Assert.Equal(results[0].BatchId, results[1].BatchId);

            await using var verify = CreateDbContext(connectionString);
            var batches = await verify.DataImportBatches.AsNoTracking().ToListAsync();
            var batch = Assert.Single(batches);
            Assert.Equal(results[0].BatchId, batch.Id);
            Assert.Equal("pending", batch.Status);
        }
        finally
        {
            File.Delete(sourceFile);
        }
    }

    [PostgresRequiredFact]
    public async Task Start_WithStaleRunningBatch_RecoversItAndCreatesNewBatch()
    {
        var connectionString = await CreateDatabaseAsync();
        if (connectionString is null)
            return;

        var sourceFile = CreateTempSourceFile();
        try
        {
            long staleId;
            await using (var seed = new NpgsqlConnection(connectionString))
            {
                await seed.OpenAsync();
                await using var cmd = new NpgsqlCommand(
                    """
                    INSERT INTO "DataImportBatches" ("SourceFileName", "Status", "StartedAtUtc", "QueuedAtUtc", "LastHeartbeatUtc")
                    VALUES ('abandoned.accdb', 'running', NOW() - INTERVAL '3 hours', NOW() - INTERVAL '3 hours', NOW() - INTERVAL '2 hours')
                    RETURNING "Id";
                    """,
                    seed);
                staleId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            await using var db = CreateDbContext(connectionString);
            var result = await TryCreateBatchAsync(CreateService(db), sourceFile);

            Assert.True(result.Created);
            Assert.NotEqual(staleId, result.BatchId);

            await using var verify = CreateDbContext(connectionString);
            var stale = await verify.DataImportBatches.AsNoTracking().SingleAsync(x => x.Id == staleId);
            Assert.Equal("failed", stale.Status);
            Assert.NotNull(stale.CompletedAtUtc);
            var fresh = await verify.DataImportBatches.AsNoTracking().SingleAsync(x => x.Id == result.BatchId);
            Assert.Equal("pending", fresh.Status);
        }
        finally
        {
            File.Delete(sourceFile);
        }
    }

    [PostgresRequiredFact]
    public async Task Start_WithFreshActiveBatch_ReturnsExistingIdWithoutInsert()
    {
        var connectionString = await CreateDatabaseAsync();
        if (connectionString is null)
            return;

        var sourceFile = CreateTempSourceFile();
        try
        {
            await using var dbA = CreateDbContext(connectionString);
            var first = await TryCreateBatchAsync(CreateService(dbA), sourceFile);
            await using var dbB = CreateDbContext(connectionString);
            var second = await TryCreateBatchAsync(CreateService(dbB), sourceFile);

            Assert.True(first.Created);
            Assert.False(second.Created);
            Assert.Equal(first.BatchId, second.BatchId);

            await using var verify = CreateDbContext(connectionString);
            Assert.Equal(1, await verify.DataImportBatches.CountAsync());
        }
        finally
        {
            File.Delete(sourceFile);
        }
    }

    private static long LockKey => (long)typeof(AccessImportService)
        .GetField("AccessImportBatchCreateAdvisoryLockKey", BindingFlags.NonPublic | BindingFlags.Static)!
        .GetRawConstantValue()!;

    private static async Task<(bool Created, long BatchId)> TryCreateBatchAsync(AccessImportService service, string sourceFile)
    {
        var method = typeof(AccessImportService).GetMethod("CreateImportBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            var task = (Task)method.Invoke(service, new object?[]
            {
                sourceFile, null, null, Path.GetFileName(sourceFile), true, false, false, "synthetic-test-source", CancellationToken.None
            })!;
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            var response = (Api.Models.AccessImportRunResponse)result.GetType().GetField("Item2")!.GetValue(result)!;
            return (true, response.BatchId);
        }
        catch (InvalidOperationException ex) when (ex.GetType().Name == "AccessImportBatchAlreadyActiveException")
        {
            var activeId = (long)ex.GetType().GetProperty("ActiveBatchId")!.GetValue(ex)!;
            return (false, activeId);
        }
    }

    private static async Task WaitForAdvisoryWaitersAsync(NpgsqlConnection connection, int expectedWaiters)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())",
                connection);
            var waiters = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if (waiters >= expectedWaiters)
                return;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Expected {expectedWaiters} advisory lock waiters, saw {waiters}.");
            await Task.Delay(50);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string CreateTempSourceFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rq673_{Guid.NewGuid():N}.accdb");
        File.WriteAllBytes(path, new byte[] { 0 });
        return path;
    }

    private async Task<string?> CreateDatabaseAsync()
    {
        if (!_fixture.IsAvailable)
            return null;

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_access_start_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        await using var db = CreateDbContext(connectionString);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "DataImportBatches" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SourceSystem" character varying(64) NOT NULL DEFAULT 'access',
                "SourceNamespace" character varying(128),
                "SourceFileName" character varying(300) NOT NULL DEFAULT '',
                "SourceFilePath" character varying(800),
                "SourceStorageKey" character varying(1024),
                "SourceStorageProvider" character varying(32),
                "QueuedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "StartedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "CompletedAtUtc" timestamp with time zone,
                "LastHeartbeatUtc" timestamp with time zone,
                "Status" character varying(32) NOT NULL DEFAULT 'pending',
                "CurrentStep" character varying(64),
                "CurrentTable" character varying(300),
                "SummaryJson" text,
                "ErrorMessage" character varying(4000),
                "ErrorDetailsJson" text,
                "RequestedBy" character varying(200),
                "ImportMode" character varying(16) NOT NULL DEFAULT 'auto',
                "ImportStrategy" character varying(32) NOT NULL DEFAULT 'full',
                "IncludeAnalytics" boolean NOT NULL DEFAULT TRUE,
                "OverwriteExisting" boolean NOT NULL DEFAULT TRUE,
                "IncludeTemporaryTables" boolean NOT NULL DEFAULT FALSE,
                "SkipInvalidForeignKeys" boolean NOT NULL DEFAULT FALSE,
                "CancellationRequested" boolean NOT NULL DEFAULT FALSE,
                "CancellationRequestedAtUtc" timestamp with time zone,
                "RetryCount" integer NOT NULL DEFAULT 0,
                "ProgressPercent" integer NOT NULL DEFAULT 0,
                "RowsRead" integer NOT NULL DEFAULT 0,
                "RowsAccepted" integer NOT NULL DEFAULT 0,
                "RowsWritten" integer NOT NULL DEFAULT 0,
                "IsIncremental" boolean NOT NULL DEFAULT FALSE,
                "CursorSnapshot" jsonb,
                "CursorBeforeJson" jsonb,
                "CursorAfterJson" jsonb,
                "ProcessedRowCount" integer NOT NULL DEFAULT 0,
                "SkippedRowCount" integer NOT NULL DEFAULT 0,
                "RowsInserted" integer NOT NULL DEFAULT 0,
                "RowsUpdated" integer NOT NULL DEFAULT 0,
                "RowsUnchanged" integer NOT NULL DEFAULT 0,
                "RowsStaged" integer NOT NULL DEFAULT 0,
                "RowsSkippedStale" integer NOT NULL DEFAULT 0,
                "RowsRejected" integer NOT NULL DEFAULT 0,
                "ShadowMismatchCount" integer NOT NULL DEFAULT 0,
                "SourceFileHash" character varying(128),
                "DurationSeconds" integer,
                "TotalImported" integer NOT NULL DEFAULT 0,
                "TotalUpdated" integer NOT NULL DEFAULT 0,
                "TotalErrors" integer NOT NULL DEFAULT 0,
                "DataOrigin" character varying(32) NOT NULL DEFAULT 'access'
            );
            """);
        return connectionString;
    }

    private static TrendplusDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
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
                PreventConcurrentRuns = true,
                RunningBatchStaleMinutes = 30,
                EnableRuntimeBatchSchemaBootstrap = false
            }));
}
