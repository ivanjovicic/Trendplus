using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using System.Data;

namespace Api.Services.Access;

public sealed class AccessImportQueuedJob
{
    public long BatchId { get; init; }
    public string SourceFilePath { get; init; } = string.Empty;
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
        try
        {
            await _db.Database.OpenConnectionAsync(ct);
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.Transaction = tx.GetDbTransaction();
            cmd.CommandText = """
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
                    b."SourceFileName",
                    COALESCE(b."IncludeAnalytics", TRUE),
                    COALESCE(b."OverwriteExisting", TRUE),
                    COALESCE(b."IncludeTemporaryTables", FALSE);
                """;

            AccessImportQueuedJob? job = null;
            await using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    job = new AccessImportQueuedJob
                    {
                        BatchId = reader.GetInt64(0),
                        SourceFilePath = reader.GetString(1),
                        SourceFileName = reader.GetString(2),
                        IncludeAnalytics = reader.GetBoolean(3),
                        OverwriteExisting = reader.GetBoolean(4),
                        IncludeTemporaryTables = reader.GetBoolean(5)
                    };
                }
            }

            await tx.CommitAsync(ct);
            if (job is null)
                return null;

            _logger.LogInformation(
                "Access import job claimed. BatchId: {BatchId}. SourceFileName: {SourceFileName}.",
                job.BatchId,
                job.SourceFileName);
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
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
