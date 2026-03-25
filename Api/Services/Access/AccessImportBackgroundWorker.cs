using Api.Config;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;

namespace Api.Services.Access;

public sealed class AccessImportBackgroundWorker : BackgroundService
{
    private const string WorkerName = "AccessImportBackgroundWorker";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AccessImportBackgroundWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly AccessImportOptions _options;

    public AccessImportBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<AccessImportBackgroundWorker> logger,
        WorkerHealthService healthService,
        IOptions<AccessImportOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthService = healthService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Access import background worker started.");
        _healthService.ReportRunning(WorkerName, "Starting up...");

        var pauseCheckInterval = TimeSpan.FromSeconds(5);
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
        var maxConcurrentJobs = Math.Max(1, _options.MaxConcurrentJobs);
        var runningJobs = new List<Task>(capacity: maxConcurrentJobs);
        var paused = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_options.WorkerEnabled)
            {
                if (!paused)
                {
                    _logger.LogInformation(
                        "{WorkerName} paused. WorkerEnabled: {WorkerEnabled}.",
                        WorkerName,
                        _options.WorkerEnabled);
                    _healthService.ReportStopped(WorkerName, "Paused by runtime settings.");
                    paused = true;
                }

                try
                {
                    await Task.Delay(pauseCheckInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            if (paused)
            {
                paused = false;
                _healthService.ReportRunning(WorkerName, "Resumed.");
                _logger.LogInformation("{WorkerName} resumed.", WorkerName);
            }

            try
            {
                runningJobs.RemoveAll(t => t.IsCompleted);

                while (runningJobs.Count < maxConcurrentJobs)
                {
                    AccessImportQueuedJob? job;
                    await using (var claimScope = _serviceProvider.CreateAsyncScope())
                    {
                        var queue = claimScope.ServiceProvider.GetRequiredService<IAccessImportJobQueue>();
                        job = await queue.ClaimNextAsync(stoppingToken);
                    }

                    if (job is null)
                        break;

                    runningJobs.Add(ProcessJobAsync(job, stoppingToken));
                }

                if (runningJobs.Count == 0)
                {
                    _healthService.ReportHealthy(WorkerName, "Idle - waiting for pending jobs.");
                    await Task.Delay(pollInterval, stoppingToken);
                }
                else
                {
                    _healthService.ReportRunning(WorkerName, $"Processing {runningJobs.Count} job(s).");
                    await Task.WhenAny(Task.WhenAll(runningJobs), Task.Delay(pollInterval, stoppingToken));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Access import background worker iteration failed.");
                _healthService.ReportError(WorkerName, ex);
                try
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        try
        {
            if (runningJobs.Count > 0)
                await Task.WhenAll(runningJobs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Access import background worker shut down with incomplete job tasks.");
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
        _logger.LogInformation("Access import background worker stopped.");
    }

    private async Task ProcessJobAsync(AccessImportQueuedJob job, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IAccessImportService>();

            await service.RunExistingBatchAsync(
                batchId: job.BatchId,
                accessFilePath: job.SourceFilePath,
                sourceFileName: job.SourceFileName,
                includeAnalytics: job.IncludeAnalytics,
                overwriteExisting: job.OverwriteExisting,
                includeTemporaryTables: job.IncludeTemporaryTables,
                deleteWorkingFileAfterCompletion: true,
                ct: stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await MarkBatchInterruptedBestEffortAsync(job.BatchId);
            _logger.LogWarning(
                "Access import worker stopping while processing batch {BatchId}. The batch will be recovered by stale-recovery on next startup.",
                job.BatchId);
        }
        catch (Exception ex)
        {
            if (await TryRequeueForRetryAsync(job, ex, stoppingToken))
            {
                return;
            }

            _logger.LogError(
                ex,
                "Access import background job failed unexpectedly. BatchId: {BatchId}. SourceFileName: {SourceFileName}.",
                job.BatchId,
                job.SourceFileName);

            await MarkBatchFailedBestEffortAsync(job.BatchId, ex);
        }
    }

    private async Task<bool> TryRequeueForRetryAsync(AccessImportQueuedJob job, Exception ex, CancellationToken stoppingToken)
    {
        if (!_options.EnableAutoRetryForTransientFailures)
            return false;

        var maxRetryCount = Math.Max(0, _options.MaxRetryCount);
        if (maxRetryCount <= 0)
            return false;

        if (!IsTransientFailure(ex))
            return false;

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

            const string sql = """
                UPDATE "DataImportBatches"
                SET "Status" = 'pending',
                    "CompletedAtUtc" = NULL,
                    "LastHeartbeatUtc" = NOW(),
                    "CurrentStep" = 'retry-queued',
                    "CurrentTable" = 'all',
                    "ErrorMessage" = NULL,
                    "ErrorDetailsJson" = NULL,
                    "SummaryJson" = NULL,
                    "ProgressPercent" = 0,
                    "RowsRead" = 0,
                    "RowsAccepted" = 0,
                    "RowsWritten" = 0
                WHERE "Id" = @p0
                  AND "Status" = 'failed'
                  AND COALESCE("CancellationRequested", FALSE) = FALSE
                  AND COALESCE("RetryCount", 0) <= @p1;
                """;

            var affected = await db.Database.ExecuteSqlRawAsync(
                sql,
                new object[] { job.BatchId, maxRetryCount },
                stoppingToken);

            if (affected <= 0)
                return false;

            _logger.LogWarning(
                ex,
                "Access import job scheduled for retry. BatchId: {BatchId}. SourceFileName: {SourceFileName}. MaxRetryCount: {MaxRetryCount}.",
                job.BatchId,
                job.SourceFileName,
                maxRetryCount);
            return true;
        }
        catch (Exception retryEx)
        {
            _logger.LogWarning(
                retryEx,
                "Access import job retry scheduling failed. BatchId: {BatchId}.",
                job.BatchId);
            return false;
        }
    }

    private static bool IsTransientFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case TimeoutException:
                case HttpRequestException:
                case SocketException:
                case IOException:
                    return true;
                case NpgsqlException npgsql when npgsql is not PostgresException:
                    return true;
                case PostgresException pg when IsTransientSqlState(pg.SqlState):
                    return true;
            }
        }

        return false;
    }

    private static bool IsTransientSqlState(string? sqlState)
    {
        if (string.IsNullOrWhiteSpace(sqlState))
            return false;

        return sqlState is
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected or
            PostgresErrorCodes.LockNotAvailable or
            PostgresErrorCodes.ConnectionException or
            PostgresErrorCodes.ConnectionDoesNotExist or
            PostgresErrorCodes.ConnectionFailure or
            PostgresErrorCodes.SqlClientUnableToEstablishSqlConnection or
            PostgresErrorCodes.TooManyConnections or
            PostgresErrorCodes.ConfigurationLimitExceeded;
    }

    private async Task MarkBatchFailedBestEffortAsync(long batchId, Exception ex)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

            const string sql = """
                UPDATE "DataImportBatches"
                SET "Status" = 'failed',
                    "CompletedAtUtc" = NOW(),
                    "LastHeartbeatUtc" = NOW(),
                    "CurrentStep" = COALESCE(NULLIF("CurrentStep", ''), 'failed'),
                    "ErrorMessage" = COALESCE(NULLIF("ErrorMessage", ''), @p1),
                    "ErrorDetailsJson" = COALESCE("ErrorDetailsJson", @p2),
                    "ProgressPercent" = 100
                WHERE "Id" = @p0
                  AND "CompletedAtUtc" IS NULL;
                """;

            await db.Database.ExecuteSqlRawAsync(
                sql,
                new object[]
                {
                    batchId,
                    ex.GetBaseException().Message,
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = ex.GetType().FullName,
                        message = ex.GetBaseException().Message
                    })
                },
                CancellationToken.None);
        }
        catch (Exception markEx)
        {
            _logger.LogWarning(
                markEx,
                "Failed to mark access import batch as failed after worker crash. BatchId: {BatchId}.",
                batchId);
        }
    }

    private async Task MarkBatchInterruptedBestEffortAsync(long batchId)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

            const string sql = """
                UPDATE "DataImportBatches"
                SET "Status" = 'interrupted',
                    "CompletedAtUtc" = NOW(),
                    "LastHeartbeatUtc" = NOW(),
                    "CurrentStep" = 'stopped',
                    "CurrentTable" = NULL,
                    "ProgressPercent" = 100,
                    "ErrorMessage" = COALESCE(NULLIF("ErrorMessage", ''), @p1),
                    "ErrorDetailsJson" = COALESCE("ErrorDetailsJson", @p2)
                WHERE "Id" = @p0
                  AND "CompletedAtUtc" IS NULL
                  AND "Status" = 'running';
                """;

            await db.Database.ExecuteSqlRawAsync(
                sql,
                new object[]
                {
                    batchId,
                    "Import interrupted during worker shutdown.",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = typeof(OperationCanceledException).FullName,
                        message = "Access import worker shutdown interrupted batch execution."
                    })
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to mark access import batch as interrupted during worker stop. BatchId: {BatchId}.",
                batchId);
        }
    }
}
