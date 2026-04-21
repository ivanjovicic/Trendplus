using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Api.Services.Access;

public sealed class AccessImportQueuedJob
{
    public long BatchId { get; init; }
    public string SourceFilePath { get; init; } = string.Empty;
    public string SourceStorageKey { get; init; } = string.Empty;
    public string SourceStorageProvider { get; init; } = string.Empty;
    public string SourceFileName { get; init; } = string.Empty;
    public bool IncludeAnalytics { get; init; }
    public bool OverwriteExisting { get; init; }
    public bool IncludeTemporaryTables { get; init; }
}

public interface IAccessImportJobQueue
{
    Task EnqueueAsync(long batchId, CancellationToken ct = default);
    Task<AccessImportQueuedJob?> ClaimNextAsync(CancellationToken ct = default);
}

public sealed class AccessImportJobQueue : IAccessImportJobQueue
{
    private readonly TrendplusDbContext _db;
    private readonly ILogger<AccessImportJobQueue> _logger;

    public AccessImportJobQueue(TrendplusDbContext db, ILogger<AccessImportJobQueue> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task EnqueueAsync(long batchId, CancellationToken ct = default)
    {
        if (batchId <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchId));

        const string sql = """
            UPDATE "DataImportBatches"
            SET "Status" = 'pending',
                "QueuedAtUtc" = NOW(),
                "CurrentStep" = 'queued',
                "CurrentTable" = 'all',
                "LastHeartbeatUtc" = NOW()
            WHERE "Id" = @p0
              AND "CompletedAtUtc" IS NULL
              AND COALESCE("CancellationRequested", FALSE) = FALSE
              AND "Status" IN ('pending', 'failed', 'interrupted');
            """;

        var affected = await _db.Database.ExecuteSqlRawAsync(sql, new object[] { batchId }, ct);
        if (affected <= 0)
            throw new InvalidOperationException($"Access import batch {batchId} is not enqueueable.");

        _logger.LogInformation("Access import job enqueued. BatchId: {BatchId}.", batchId);
    }

    public async Task<AccessImportQueuedJob?> ClaimNextAsync(CancellationToken ct = default)
    {
        const string claimSqlStorageAware = """
            WITH next_job AS (
                SELECT "Id"
                FROM "DataImportBatches"
                WHERE "Status" = 'pending'
                  AND "CompletedAtUtc" IS NULL
                  AND COALESCE("CancellationRequested", FALSE) = FALSE
                  AND (
                        COALESCE(NULLIF("SourceFilePath", ''), '') <> ''
                     OR COALESCE(NULLIF("SourceStorageKey", ''), '') <> ''
                  )
                ORDER BY "QueuedAtUtc" ASC NULLS FIRST, "Id" ASC
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE "DataImportBatches" b
            SET "Status" = 'running',
                "StartedAtUtc" = NOW(),
                "LastHeartbeatUtc" = NOW(),
                "CurrentStep" = 'starting',
                "CurrentTable" = 'all'
            FROM next_job
            WHERE b."Id" = next_job."Id"
            RETURNING
                b."Id",
                COALESCE(b."SourceFilePath", ''),
                COALESCE(b."SourceStorageKey", ''),
                COALESCE(b."SourceStorageProvider", ''),
                b."SourceFileName",
                COALESCE(b."IncludeAnalytics", TRUE),
                COALESCE(b."OverwriteExisting", TRUE),
                COALESCE(b."IncludeTemporaryTables", FALSE);
            """;

        const string claimSqlLegacy = """
            WITH next_job AS (
                SELECT "Id"
                FROM "DataImportBatches"
                WHERE "Status" = 'pending'
                  AND "CompletedAtUtc" IS NULL
                  AND COALESCE("CancellationRequested", FALSE) = FALSE
                  AND COALESCE(NULLIF("SourceFilePath", ''), '') <> ''
                ORDER BY "QueuedAtUtc" ASC NULLS FIRST, "Id" ASC
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE "DataImportBatches" b
            SET "Status" = 'running',
                "StartedAtUtc" = NOW(),
                "LastHeartbeatUtc" = NOW(),
                "CurrentStep" = 'starting',
                "CurrentTable" = 'all'
            FROM next_job
            WHERE b."Id" = next_job."Id"
            RETURNING
                b."Id",
                COALESCE(b."SourceFilePath", ''),
                ''::text AS "SourceStorageKey",
                ''::text AS "SourceStorageProvider",
                b."SourceFileName",
                COALESCE(b."IncludeAnalytics", TRUE),
                COALESCE(b."OverwriteExisting", TRUE),
                COALESCE(b."IncludeTemporaryTables", FALSE);
            """;

        try
        {
            var connectionString = _db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Trendplus connection string is not configured for access import job claiming.");

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            AccessImportQueuedJob? job;
            try
            {
                job = await ClaimNextWithCommandAsync(connection, claimSqlStorageAware, ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _logger.LogWarning(
                    ex,
                    "Access import queue claim is using legacy schema without storage metadata columns. Falling back to SourceFilePath-only claim.");
                job = await ClaimNextWithCommandAsync(connection, claimSqlLegacy, ct);
            }

            if (job is null)
                return null;

            _logger.LogInformation(
                "Access import job claimed. BatchId: {BatchId}. SourceFileName: {SourceFileName}. StorageBacked: {StorageBacked}.",
                job.BatchId,
                job.SourceFileName,
                !string.IsNullOrWhiteSpace(job.SourceStorageKey));
            return job;
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "Access import job queue claim skipped because DataImportBatches queue columns are not fully available yet.");
            return null;
        }
    }

    private static async Task<AccessImportQueuedJob?> ClaimNextWithCommandAsync(
        NpgsqlConnection connection,
        string commandText,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = commandText;

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new AccessImportQueuedJob
        {
            BatchId = reader.GetInt64(0),
            SourceFilePath = reader.GetString(1),
            SourceStorageKey = reader.GetString(2),
            SourceStorageProvider = reader.GetString(3),
            SourceFileName = reader.GetString(4),
            IncludeAnalytics = reader.GetBoolean(5),
            OverwriteExisting = reader.GetBoolean(6),
            IncludeTemporaryTables = reader.GetBoolean(7)
        };
    }
}
