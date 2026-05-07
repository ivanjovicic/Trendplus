using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
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

public sealed record AccessImportEnqueueDiagnostics(
    long BatchId,
    bool Exists,
    string? CurrentStatus,
    bool HasSourceFilePath,
    bool HasSourceStorageKey,
    bool CancellationRequested,
    DateTime? CompletedAtUtc,
    bool Enqueueable,
    string Reason);

public sealed record AccessImportPendingRecoveryResult(
    int StalePendingCount,
    int RecoveredCount,
    int MissingSourceCount);

public interface IAccessImportJobQueue
{
    Task EnqueueAsync(long batchId, CancellationToken ct = default);
    Task<AccessImportQueuedJob?> ClaimNextAsync(CancellationToken ct = default);
    Task<AccessImportEnqueueDiagnostics> GetEnqueueDiagnosticsAsync(long batchId, CancellationToken ct = default);
    Task<AccessImportPendingRecoveryResult> RecoverStalePendingAsync(TimeSpan staleAfter, CancellationToken ct = default);
}

public sealed class AccessImportJobQueue : IAccessImportJobQueue
{
    private static readonly object NoEligibleLogLock = new();
    private static DateTime _lastNoEligibleWarningUtc = DateTime.MinValue;

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

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Access import enqueue started. BatchId: {BatchId}.", batchId);

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

        stopwatch.Stop();
        _logger.LogInformation("Access import enqueue completed. BatchId: {BatchId}. ElapsedMs: {ElapsedMs}.", batchId, stopwatch.ElapsedMilliseconds);
    }

    public async Task<AccessImportEnqueueDiagnostics> GetEnqueueDiagnosticsAsync(long batchId, CancellationToken ct = default)
    {
        if (batchId <= 0)
        {
            return new AccessImportEnqueueDiagnostics(
                batchId,
                Exists: false,
                CurrentStatus: null,
                HasSourceFilePath: false,
                HasSourceStorageKey: false,
                CancellationRequested: false,
                CompletedAtUtc: null,
                Enqueueable: false,
                Reason: "invalid_batch_id");
        }

        var batch = await _db.DataImportBatches
            .AsNoTracking()
            .Where(x => x.Id == batchId)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.SourceFilePath,
                x.SourceStorageKey,
                x.CancellationRequested,
                x.CompletedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (batch is null)
        {
            return new AccessImportEnqueueDiagnostics(
                batchId,
                Exists: false,
                CurrentStatus: null,
                HasSourceFilePath: false,
                HasSourceStorageKey: false,
                CancellationRequested: false,
                CompletedAtUtc: null,
                Enqueueable: false,
                Reason: "not_found");
        }

        return BuildEnqueueDiagnostics(
            batch.Id,
            batch.Status,
            batch.SourceFilePath,
            batch.SourceStorageKey,
            batch.CancellationRequested,
            batch.CompletedAtUtc);
    }

    public async Task<AccessImportPendingRecoveryResult> RecoverStalePendingAsync(TimeSpan staleAfter, CancellationToken ct = default)
    {
        var safeStaleAfter = staleAfter < TimeSpan.FromMinutes(1)
            ? TimeSpan.FromMinutes(1)
            : staleAfter;
        var cutoffUtc = DateTime.UtcNow.Subtract(safeStaleAfter);

        try
        {
            var candidates = await _db.DataImportBatches
                .AsNoTracking()
                .Where(x =>
                    x.Status == "pending" &&
                    x.CompletedAtUtc == null &&
                    !x.CancellationRequested &&
                    (x.LastHeartbeatUtc ?? x.QueuedAtUtc) <= cutoffUtc)
                .OrderBy(x => x.QueuedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.SourceFileName,
                    x.SourceFilePath,
                    x.SourceStorageKey,
                    x.QueuedAtUtc,
                    x.LastHeartbeatUtc
                })
                .Take(200)
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                return new AccessImportPendingRecoveryResult(0, 0, 0);
            }

            var recovered = 0;
            var missingSource = 0;
            foreach (var candidate in candidates)
            {
                var hasSource =
                    !string.IsNullOrWhiteSpace(candidate.SourceFilePath) ||
                    !string.IsNullOrWhiteSpace(candidate.SourceStorageKey);

                _logger.LogWarning(
                    "Access import pending batch stale. BatchId: {BatchId}. SourceFileName: {SourceFileName}. QueuedAtUtc: {QueuedAtUtc}. LastHeartbeatUtc: {LastHeartbeatUtc}. StaleAfterMinutes: {StaleAfterMinutes}. HasSource: {HasSource}.",
                    candidate.Id,
                    candidate.SourceFileName,
                    candidate.QueuedAtUtc,
                    candidate.LastHeartbeatUtc,
                    safeStaleAfter.TotalMinutes,
                    hasSource);

                if (!hasSource)
                {
                    missingSource++;
                    continue;
                }

                const string recoverSql = """
                    UPDATE "DataImportBatches"
                    SET "QueuedAtUtc" = COALESCE("QueuedAtUtc", NOW()),
                        "LastHeartbeatUtc" = NOW(),
                        "CurrentStep" = CASE
                            WHEN COALESCE(NULLIF("CurrentStep", ''), '') = '' OR "CurrentStep" = 'queued' THEN 'queued-stale-recovered'
                            ELSE "CurrentStep"
                        END,
                        "CurrentTable" = COALESCE(NULLIF("CurrentTable", ''), 'all')
                    WHERE "Id" = @p0
                      AND "Status" = 'pending'
                      AND "CompletedAtUtc" IS NULL
                      AND COALESCE("CancellationRequested", FALSE) = FALSE
                      AND (
                            COALESCE(NULLIF("SourceFilePath", ''), '') <> ''
                         OR COALESCE(NULLIF("SourceStorageKey", ''), '') <> ''
                      );
                    """;

                var affected = await _db.Database.ExecuteSqlRawAsync(recoverSql, new object[] { candidate.Id }, ct);
                if (affected > 0)
                {
                    recovered++;
                }
            }

            if (recovered > 0 || missingSource > 0)
            {
                _logger.LogInformation(
                    "Access import stale pending recovery completed. StalePendingCount: {StalePendingCount}. RecoveredCount: {RecoveredCount}. MissingSourceCount: {MissingSourceCount}.",
                    candidates.Count,
                    recovered,
                    missingSource);
            }

            return new AccessImportPendingRecoveryResult(candidates.Count, recovered, missingSource);
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "Access import stale pending recovery skipped because DataImportBatches queue columns are not fully available yet.");
            return new AccessImportPendingRecoveryResult(0, 0, 0);
        }
    }

    public async Task<AccessImportQueuedJob?> ClaimNextAsync(CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Access import claim-next started.");

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
            {
                await LogNoEligiblePendingBatchAsync(connection, ct);
                stopwatch.Stop();
                _logger.LogDebug("Access import claim-next completed with no eligible job. ElapsedMs: {ElapsedMs}.", stopwatch.ElapsedMilliseconds);
                return null;
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Access import claim-next completed. BatchId: {BatchId}. SourceFileName: {SourceFileName}. StorageBacked: {StorageBacked}. ElapsedMs: {ElapsedMs}.",
                job.BatchId,
                job.SourceFileName,
                !string.IsNullOrWhiteSpace(job.SourceStorageKey),
                stopwatch.ElapsedMilliseconds);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Access import queue claim SQL failed.");
            throw;
        }
    }

    public static AccessImportEnqueueDiagnostics BuildEnqueueDiagnostics(
        long batchId,
        string? status,
        string? sourceFilePath,
        string? sourceStorageKey,
        bool cancellationRequested,
        DateTime? completedAtUtc)
    {
        var currentStatus = string.IsNullOrWhiteSpace(status)
            ? "unknown"
            : status.Trim();
        var hasSourceFilePath = !string.IsNullOrWhiteSpace(sourceFilePath);
        var hasSourceStorageKey = !string.IsNullOrWhiteSpace(sourceStorageKey);
        var hasSource = hasSourceFilePath || hasSourceStorageKey;
        var enqueueableStatus =
            string.Equals(currentStatus, "pending", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentStatus, "failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentStatus, "interrupted", StringComparison.OrdinalIgnoreCase);

        string reason;
        var enqueueable = false;
        if (completedAtUtc.HasValue)
        {
            reason = "completed";
        }
        else if (cancellationRequested)
        {
            reason = "cancellation_requested";
        }
        else if (!hasSource)
        {
            reason = "missing_source";
        }
        else if (!enqueueableStatus)
        {
            reason = "status_not_enqueueable";
        }
        else
        {
            enqueueable = true;
            reason = "enqueueable";
        }

        return new AccessImportEnqueueDiagnostics(
            batchId,
            Exists: true,
            CurrentStatus: currentStatus,
            HasSourceFilePath: hasSourceFilePath,
            HasSourceStorageKey: hasSourceStorageKey,
            CancellationRequested: cancellationRequested,
            CompletedAtUtc: completedAtUtc,
            Enqueueable: enqueueable,
            Reason: reason);
    }

    private async Task LogNoEligiblePendingBatchAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        const string summarySql = """
            SELECT
                COUNT(*) FILTER (
                    WHERE "Status" = 'pending'
                      AND "CompletedAtUtc" IS NULL
                ) AS pending_total,
                COUNT(*) FILTER (
                    WHERE "Status" = 'pending'
                      AND "CompletedAtUtc" IS NULL
                      AND COALESCE("CancellationRequested", FALSE) = FALSE
                      AND (
                            COALESCE(NULLIF("SourceFilePath", ''), '') <> ''
                         OR COALESCE(NULLIF("SourceStorageKey", ''), '') <> ''
                      )
                ) AS eligible_total,
                COUNT(*) FILTER (
                    WHERE "Status" = 'pending'
                      AND "CompletedAtUtc" IS NULL
                      AND COALESCE("CancellationRequested", FALSE) = FALSE
                      AND COALESCE(NULLIF("SourceFilePath", ''), '') = ''
                      AND COALESCE(NULLIF("SourceStorageKey", ''), '') = ''
                ) AS missing_source_total,
                COUNT(*) FILTER (
                    WHERE "Status" = 'pending'
                      AND "CompletedAtUtc" IS NULL
                      AND COALESCE("CancellationRequested", FALSE) = TRUE
                ) AS cancelled_pending_total;
            """;

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = summarySql;
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            if (!await reader.ReadAsync(ct))
            {
                _logger.LogDebug("Access import queue claim found no eligible pending batch.");
                return;
            }

            var pendingTotal = reader.GetInt64(0);
            var eligibleTotal = reader.GetInt64(1);
            var missingSourceTotal = reader.GetInt64(2);
            var cancelledPendingTotal = reader.GetInt64(3);

            if (pendingTotal > 0 && eligibleTotal == 0 && ShouldLogNoEligiblePendingWarning())
            {
                _logger.LogWarning(
                    "Access import queue found pending batches but none are eligible for claim. PendingTotal: {PendingTotal}. MissingSourceTotal: {MissingSourceTotal}. CancelledPendingTotal: {CancelledPendingTotal}.",
                    pendingTotal,
                    missingSourceTotal,
                    cancelledPendingTotal);
            }
            else
            {
                _logger.LogDebug(
                    "Access import queue claim found no eligible pending batch. PendingTotal: {PendingTotal}. EligibleTotal: {EligibleTotal}.",
                    pendingTotal,
                    eligibleTotal);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogDebug(
                ex,
                "Access import queue no-eligible diagnostics skipped because storage queue columns are not fully available yet.");
        }
    }

    private static bool ShouldLogNoEligiblePendingWarning()
    {
        lock (NoEligibleLogLock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastNoEligibleWarningUtc < TimeSpan.FromMinutes(5))
            {
                return false;
            }

            _lastNoEligibleWarningUtc = now;
            return true;
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
