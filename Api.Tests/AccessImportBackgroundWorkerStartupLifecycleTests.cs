using Api.Config;
using Api.Models;
using Api.Services;
using Api.Services.Access;
using Api.Services.Startup;
using Application.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportBackgroundWorkerStartupLifecycleTests
{
    [Fact]
    public void StartupGate_EnabledOnlyForWebProcess()
    {
        Assert.True(AccessImportWorkerStartupGate.ShouldGateQueueOperationsOnStartupReadiness(ProcessType.Web));
        Assert.False(AccessImportWorkerStartupGate.ShouldGateQueueOperationsOnStartupReadiness(ProcessType.Worker));
    }

    [Fact]
    public void StartupGate_BlocksQueueWorkUntilReadinessInWebProcess()
    {
        var readiness = new StartupReadinessState();
        readiness.RequireDatabaseInitialization();
        readiness.ReportProbe(
            new StartupReadinessState.DatabaseProbeState { Ok = true },
            new StartupReadinessState.DatabaseProbeState { Ok = true });

        Assert.False(AccessImportWorkerStartupGate.CanPerformQueueOperations(true, readiness));

        readiness.MarkDatabaseInitializationCompleted();

        Assert.True(AccessImportWorkerStartupGate.CanPerformQueueOperations(true, readiness));
    }

    [Fact]
    public async Task WebWorker_DoesNotClaimOrRecoverBeforeStartupReadiness()
    {
        var readiness = new StartupReadinessState();
        readiness.RequireDatabaseInitialization();
        readiness.ReportProbe(
            new StartupReadinessState.DatabaseProbeState { Ok = true },
            new StartupReadinessState.DatabaseProbeState { Ok = true });

        var queue = new RecordingAccessImportJobQueue();
        var importService = new RecordingAccessImportService();
        await using var workerHost = CreateWorkerHost(
            readiness,
            queue,
            importService,
            processType: "web",
            pollingIntervalSeconds: 1);

        await workerHost.Worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(400));
        await workerHost.Worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, queue.ClaimCount);
        Assert.Equal(0, importService.RefreshBatchStatusesCallCount);
    }

    [Fact]
    public async Task WebWorker_ResumesQueueWorkAfterStartupReadiness()
    {
        var readiness = new StartupReadinessState();
        readiness.RequireDatabaseInitialization();
        readiness.ReportProbe(
            new StartupReadinessState.DatabaseProbeState { Ok = true },
            new StartupReadinessState.DatabaseProbeState { Ok = true });

        var queue = new RecordingAccessImportJobQueue();
        var importService = new RecordingAccessImportService();
        await using var workerHost = CreateWorkerHost(
            readiness,
            queue,
            importService,
            processType: "web",
            pollingIntervalSeconds: 1);

        await workerHost.Worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(250));

        readiness.MarkDatabaseInitializationCompleted();

        await Task.Delay(TimeSpan.FromSeconds(2));
        await workerHost.Worker.StopAsync(CancellationToken.None);

        Assert.True(importService.RefreshBatchStatusesCallCount >= 1);
        Assert.True(queue.ClaimCount >= 1);
    }

    [Fact]
    public async Task WorkerProcess_DoesNotWaitForWebStartupReadiness()
    {
        var readiness = new StartupReadinessState();
        readiness.RequireDatabaseInitialization();

        var queue = new RecordingAccessImportJobQueue();
        var importService = new RecordingAccessImportService();
        await using var workerHost = CreateWorkerHost(
            readiness,
            queue,
            importService,
            processType: "worker",
            pollingIntervalSeconds: 1);

        await workerHost.Worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await workerHost.Worker.StopAsync(CancellationToken.None);

        Assert.True(importService.RefreshBatchStatusesCallCount >= 1);
        Assert.True(queue.ClaimCount >= 1);
    }

    private static WorkerTestHost CreateWorkerHost(
        StartupReadinessState readiness,
        RecordingAccessImportJobQueue queue,
        RecordingAccessImportService importService,
        string processType,
        int pollingIntervalSeconds)
    {
        var services = new ServiceCollection();
        services.AddSingleton(readiness);
        services.AddSingleton(queue);
        services.AddSingleton<IAccessImportJobQueue>(queue);
        services.AddSingleton<IAccessImportService>(importService);
        services.AddSingleton<WorkerHealthService>();
        services.AddSingleton(new WorkerRuntimeControlService(true, true, "test"));
        services.AddSingleton<WorkerRuntimePolicyService>();
        services.AddSingleton<IFileStorage, NoOpFileStorage>();
        services.AddSingleton(Options.Create(new AccessImportOptions
        {
            WorkerEnabled = true,
            PollingIntervalSeconds = pollingIntervalSeconds,
            MaxConcurrentJobs = 1,
        }));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = processType,
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddDbContext<TrendplusDbContext>(options =>
            options.UseInMemoryDatabase($"access-import-worker-{Guid.NewGuid():N}"));
        services.AddLogging();
        services.AddSingleton<AccessImportBackgroundWorker>();

        var provider = services.BuildServiceProvider();
        return new WorkerTestHost(provider, provider.GetRequiredService<AccessImportBackgroundWorker>());
    }

    private sealed class WorkerTestHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        public WorkerTestHost(ServiceProvider provider, AccessImportBackgroundWorker worker)
        {
            _provider = provider;
            Worker = worker;
        }

        public AccessImportBackgroundWorker Worker { get; }

        public ValueTask DisposeAsync()
        {
            _provider.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingAccessImportJobQueue : IAccessImportJobQueue
    {
        public int ClaimCount { get; private set; }

        public Task EnqueueAsync(long batchId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<AccessImportQueuedJob?> ClaimNextAsync(CancellationToken ct = default)
        {
            ClaimCount++;
            return Task.FromResult<AccessImportQueuedJob?>(null);
        }

        public Task<AccessImportEnqueueDiagnostics> GetEnqueueDiagnosticsAsync(long batchId, CancellationToken ct = default)
            => Task.FromResult(new AccessImportEnqueueDiagnostics(
                batchId,
                Exists: false,
                CurrentStatus: null,
                HasSourceFilePath: false,
                HasSourceStorageKey: false,
                CancellationRequested: false,
                CompletedAtUtc: null,
                Enqueueable: false,
                Reason: "test"));

        public Task<AccessImportPendingRecoveryResult> RecoverStalePendingAsync(TimeSpan staleAfter, CancellationToken ct = default)
            => Task.FromResult(new AccessImportPendingRecoveryResult(0, 0, 0));
    }

    private sealed class RecordingAccessImportService : IAccessImportService
    {
        public int RefreshBatchStatusesCallCount { get; private set; }

        public Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<AccessImportPreviewResponse>(null!);

        public Task<AccessImportRunResponse> ImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<AccessImportRunResponse>(null!);

        public Task<AccessImportRunResponse> StartImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<AccessImportRunResponse>(null!);

        public Task<AccessImportRunResponse> RunExistingBatchAsync(long batchId, string accessFilePath, string sourceFileName, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, bool deleteWorkingFileAfterCompletion = false, CancellationToken ct = default)
            => Task.FromResult<AccessImportRunResponse>(null!);

        public Task RefreshBatchStatusesAsync(long? batchId = null, CancellationToken ct = default)
        {
            RefreshBatchStatusesCallCount++;
            return Task.CompletedTask;
        }

        public Task<List<AccessImportBatchDto>> GetRecentBatchStatusesAsync(int take = 20, CancellationToken ct = default)
            => Task.FromResult(new List<AccessImportBatchDto>());

        public Task<List<AccessImportBatchDto>> GetRecentBatchesAsync(int take = 20, CancellationToken ct = default)
            => Task.FromResult(new List<AccessImportBatchDto>());

        public Task<AccessImportBatchDto?> GetBatchAsync(long batchId, CancellationToken ct = default)
            => Task.FromResult<AccessImportBatchDto?>(null);

        public Task<bool> RequestCancellationAsync(long batchId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task MarkBatchInterruptedAsync(long batchId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<DeleteBatchResult> DeleteBatchAsync(long batchId, bool includeAnalytics = true, CancellationToken ct = default)
            => Task.FromResult<DeleteBatchResult>(null!);

        public Task<NegativeIdDimensionRepairResult> RepairNegativeIdDimensionsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class NoOpFileStorage : IFileStorage
    {
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);

        public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
            => Task.FromResult(string.Empty);

        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task UploadAsync(string key, Stream content, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
