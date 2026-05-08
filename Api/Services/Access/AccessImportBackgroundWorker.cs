using Api.Config;
using Application.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;
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
    private readonly WorkerRuntimeControlService _controlService;
    private readonly WorkerRuntimePolicyService _runtimePolicyService;
    private readonly IFileStorage _fileStorage;
    private readonly AccessImportOptions _options;

    public AccessImportBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<AccessImportBackgroundWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        WorkerRuntimePolicyService runtimePolicyService,
        IFileStorage fileStorage,
        IOptions<AccessImportOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _runtimePolicyService = runtimePolicyService;
        _fileStorage = fileStorage;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Access import background worker started. WorkerEnabled: {WorkerEnabled}. GlobalWorkersEnabled: {GlobalWorkersEnabled}. PollingIntervalSeconds: {PollingIntervalSeconds}. MaxConcurrentJobs: {MaxConcurrentJobs}. PendingBatchStaleMinutes: {PendingBatchStaleMinutes}.",
            _options.WorkerEnabled,
            _controlService.IsEnabled,
            _options.PollingIntervalSeconds,
            _options.MaxConcurrentJobs,
            _options.PendingBatchStaleMinutes);
        _healthService.ReportRunning(WorkerName, "Starting up...");

        var pauseCheckInterval = TimeSpan.FromSeconds(5);
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
        var maxConcurrentJobs = Math.Max(1, _options.MaxConcurrentJobs);
        var pendingRecoveryInterval = TimeSpan.FromSeconds(Math.Max(15, _options.PendingBatchRecoveryIntervalSeconds));
        var nextPendingRecoveryUtc = DateTime.MinValue;
        var runningJobs = new List<Task>(capacity: maxConcurrentJobs);
        var paused = false;

        await RecoverStaleBatchesAtStartupAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled || !_options.WorkerEnabled)
            {
                if (!paused)
                {
                    var reason = !_controlService.IsEnabled
                        ? "Paused - global workers switch OFF."
                        : "Paused - AccessImport:WorkerEnabled is false.";

                    _logger.LogInformation(
                        "{WorkerName} paused. GlobalWorkersEnabled: {GlobalWorkersEnabled}. WorkerEnabled: {WorkerEnabled}.",
                        WorkerName,
                        _controlService.IsEnabled,
                        _options.WorkerEnabled);
                    if (!_options.WorkerEnabled)
                    {
                        _logger.LogWarning(
                            "Access import worker disabled by config. Set AccessImport:WorkerEnabled=true to process pending batches.");
                    }

                    _healthService.ReportStopped(WorkerName, reason);
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

            var policy = await _runtimePolicyService.GetPolicyAsync(WorkerName, stoppingToken);
            var manualRunRequested = false;
            if (!policy.CanRunNow)
            {
                if (!paused)
                {
                    _logger.LogInformation("{WorkerName} paused. Reason: {Reason}", WorkerName, policy.PauseReason ?? "Worker policy disabled execution.");
                    _healthService.ReportStopped(WorkerName, policy.PauseReason ?? "Pauziran - worker policy disabled execution.");
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

            if (!policy.IsScheduleEnabled && policy.ManualRunRequested && !string.IsNullOrWhiteSpace(policy.ManualRunToken))
            {
                manualRunRequested = await _runtimePolicyService.TryConsumeManualRunRequestAsync(
                    WorkerName,
                    policy.ManualRunToken,
                    stoppingToken);

                if (!manualRunRequested)
                {
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

                if (DateTime.UtcNow >= nextPendingRecoveryUtc)
                {
                    await RecoverStalePendingBatchesAsync(stoppingToken);
                    nextPendingRecoveryUtc = DateTime.UtcNow.Add(pendingRecoveryInterval);
                }

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
                    var delay = manualRunRequested ? pauseCheckInterval : pollInterval;
                    await Task.Delay(delay, stoppingToken);
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

    private async Task RecoverStaleBatchesAtStartupAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IAccessImportService>();
            await service.RefreshBatchStatusesAsync(batchId: null, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown path
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Access import stale-batch recovery at startup failed. Worker will continue.");
        }
    }

    private async Task RecoverStalePendingBatchesAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var queue = scope.ServiceProvider.GetRequiredService<IAccessImportJobQueue>();
            await queue.RecoverStalePendingAsync(
                TimeSpan.FromMinutes(Math.Max(1, _options.PendingBatchStaleMinutes)),
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown path
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Access import stale pending watchdog failed. Worker will continue.");
        }
    }

    private async Task ProcessJobAsync(AccessImportQueuedJob job, CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Access import started. BatchId: {BatchId}. SourceFileName: {SourceFileName}. StorageBacked: {StorageBacked}.",
                job.BatchId,
                job.SourceFileName,
                !string.IsNullOrWhiteSpace(job.SourceStorageKey));

            var workingFilePath = job.SourceFilePath;
            if (!string.IsNullOrWhiteSpace(job.SourceStorageKey))
            {
                workingFilePath = await StageSourceFromStorageAsync(job, stoppingToken);
            }

            if (string.IsNullOrWhiteSpace(workingFilePath) || !File.Exists(workingFilePath))
            {
                throw new FileNotFoundException(
                    "Access import source file for claimed job was not found.",
                    workingFilePath);
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IAccessImportService>();

            await service.RunExistingBatchAsync(
                batchId: job.BatchId,
                accessFilePath: workingFilePath,
                sourceFileName: job.SourceFileName,
                includeAnalytics: job.IncludeAnalytics,
                overwriteExisting: job.OverwriteExisting,
                includeTemporaryTables: job.IncludeTemporaryTables,
                deleteWorkingFileAfterCompletion: true,
                ct: stoppingToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Access import worker completed batch. BatchId: {BatchId}. SourceFileName: {SourceFileName}. ElapsedMs: {ElapsedMs}.",
                job.BatchId,
                job.SourceFileName,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            await MarkBatchInterruptedBestEffortAsync(job.BatchId);
            _logger.LogWarning(
                "Access import worker stopping while processing batch {BatchId}. ElapsedMs: {ElapsedMs}. The batch will be recovered by stale-recovery on next startup.",
                job.BatchId,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (await TryRequeueForRetryAsync(job, ex, stoppingToken))
            {
                return;
            }

            _logger.LogError(
                ex,
                "Access import background job failed unexpectedly. BatchId: {BatchId}. SourceFileName: {SourceFileName}. ElapsedMs: {ElapsedMs}.",
                job.BatchId,
                job.SourceFileName,
                stopwatch.ElapsedMilliseconds);

            await MarkBatchFailedBestEffortAsync(job.BatchId, ex);
        }
    }

    private async Task<string> StageSourceFromStorageAsync(AccessImportQueuedJob job, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(job.SourceStorageKey))
        {
            throw new InvalidOperationException(
                $"Batch {job.BatchId} is marked as storage-backed but SourceStorageKey is empty.");
        }

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Access import storage staging started. BatchId: {BatchId}. StorageProvider: {StorageProvider}. StorageKey: {StorageKey}.",
            job.BatchId,
            string.IsNullOrWhiteSpace(job.SourceStorageProvider) ? "unknown" : job.SourceStorageProvider,
            job.SourceStorageKey);

        var storageRoot = string.IsNullOrWhiteSpace(_options.StorageRoot)
            ? Path.Combine(Path.GetTempPath(), "trendplus_access_jobs")
            : _options.StorageRoot;

        var baseDirectory = Path.IsPathRooted(storageRoot)
            ? storageRoot
            : Path.Combine(Path.GetTempPath(), storageRoot);

        var stagingDirectory = Path.Combine(baseDirectory, "staged-from-storage");
        Directory.CreateDirectory(stagingDirectory);

        var extension = Path.GetExtension(job.SourceFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".accdb";
        }

        var baseName = Path.GetFileNameWithoutExtension(job.SourceFileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = $"access_batch_{job.BatchId}";
        }

        var localFileName = $"{baseName}_job_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var localPath = Path.Combine(stagingDirectory, localFileName);

        await using var sourceStream = await _fileStorage.OpenReadAsync(job.SourceStorageKey, ct);
        await using var destinationStream = new FileStream(
            localPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await sourceStream.CopyToAsync(destinationStream, ct);
        await destinationStream.FlushAsync(ct);

        stopwatch.Stop();
        _logger.LogInformation(
            "Access import source staged from storage. BatchId: {BatchId}. StorageProvider: {StorageProvider}. StorageKey: {StorageKey}. LocalPath: {LocalPath}. FileSizeBytes: {FileSizeBytes}. ElapsedMs: {ElapsedMs}.",
            job.BatchId,
            string.IsNullOrWhiteSpace(job.SourceStorageProvider) ? "unknown" : job.SourceStorageProvider,
            job.SourceStorageKey,
            localPath,
            new FileInfo(localPath).Length,
            stopwatch.ElapsedMilliseconds);

        return localPath;
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
                    "CurrentStep" = 'failed',
                    "ErrorMessage" = COALESCE(NULLIF("ErrorMessage", ''), @p1),
                    "ErrorDetailsJson" = COALESCE("ErrorDetailsJson", @p2)
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
            var service = scope.ServiceProvider.GetRequiredService<IAccessImportService>();
            await service.MarkBatchInterruptedAsync(batchId, CancellationToken.None);
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
