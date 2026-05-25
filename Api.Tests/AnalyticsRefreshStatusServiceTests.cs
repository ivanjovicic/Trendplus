using Api.Services;
using Domain.Model.Analytics;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsRefreshStatusServiceTests
{
    [Fact]
    public void AnalyticsRefreshRun_HasRequiredCompositeIndexes()
    {
        using var db = CreateAnalyticsDbContext();
        var entityType = db.Model.FindEntityType(typeof(AnalyticsRefreshRun));
        Assert.NotNull(entityType);

        var indexNames = entityType!
            .GetIndexes()
            .Select(index => index.GetDatabaseName())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("idx_analytics_refresh_runs_job_started", indexNames);
        Assert.Contains("idx_analytics_refresh_runs_status_started", indexNames);
        Assert.Contains("idx_analytics_refresh_runs_worker_started", indexNames);
    }

    [Fact]
    public async Task GetStatus_ReturnsUnknown_WhenNoRefreshHistoryExists()
    {
        await using var db = CreateAnalyticsDbContext();
        var service = CreateService(db);

        var status = await service.GetStatusAsync();

        Assert.Equal("unknown", status.DataFreshnessStatus);
        Assert.All(status.Jobs, job => Assert.Equal("unknown", job.DataFreshnessStatus));
    }

    [Fact]
    public async Task GetStatus_ReturnsFresh_WhenLastSuccessfulRefreshIsWithin24Hours()
    {
        await using var db = CreateAnalyticsDbContext();
        var nowUtc = DateTime.UtcNow;
        db.AnalyticsRefreshRuns.Add(new AnalyticsRefreshRun
        {
            JobKey = "nightly_analytics_refresh",
            JobName = "Nightly analytics refresh",
            Status = "succeeded",
            StartedAtUtc = nowUtc.AddHours(-2),
            FinishedAtUtc = nowUtc.AddHours(-2).AddMinutes(12),
            DurationSeconds = 720,
            RefreshedObjectsJson = "[\"sales_facts_mv\"]",
            TriggeredBy = "nightly",
            ProcessMode = "worker",
            WorkerName = "NightlyAnalyticsRefreshWorker",
            CreatedAtUtc = nowUtc.AddHours(-2)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetStatusAsync();

        Assert.Equal("fresh", status.DataFreshnessStatus);
        Assert.Contains(status.Jobs, job => job.Key == "sales_facts_refresh" && job.DataFreshnessStatus == "fresh");
    }

    [Fact]
    public async Task GetStatus_ReturnsStale_WhenLastSuccessfulRefreshIs30HoursOld()
    {
        await using var db = CreateAnalyticsDbContext();
        var nowUtc = DateTime.UtcNow;
        db.AnalyticsRefreshRuns.Add(new AnalyticsRefreshRun
        {
            JobKey = "nightly_analytics_refresh",
            JobName = "Nightly analytics refresh",
            Status = "succeeded",
            StartedAtUtc = nowUtc.AddHours(-30),
            FinishedAtUtc = nowUtc.AddHours(-30).AddMinutes(10),
            DurationSeconds = 600,
            TriggeredBy = "nightly",
            ProcessMode = "worker",
            WorkerName = "NightlyAnalyticsRefreshWorker",
            CreatedAtUtc = nowUtc.AddHours(-30)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetStatusAsync();

        Assert.Equal("stale", status.DataFreshnessStatus);
        Assert.Contains(status.Jobs, job => job.Key == "sales_facts_refresh" && job.DataFreshnessStatus == "stale");
    }

    [Fact]
    public async Task GetStatus_ReturnsCritical_WhenFailureIsNewerThanSuccess()
    {
        await using var db = CreateAnalyticsDbContext();
        var nowUtc = DateTime.UtcNow;
        db.AnalyticsRefreshRuns.AddRange(
            new AnalyticsRefreshRun
            {
                JobKey = "nightly_analytics_refresh",
                JobName = "Nightly analytics refresh",
                Status = "succeeded",
                StartedAtUtc = nowUtc.AddHours(-10),
                FinishedAtUtc = nowUtc.AddHours(-10).AddMinutes(9),
                DurationSeconds = 540,
                TriggeredBy = "nightly",
                ProcessMode = "worker",
                WorkerName = "NightlyAnalyticsRefreshWorker",
                CreatedAtUtc = nowUtc.AddHours(-10)
            },
            new AnalyticsRefreshRun
            {
                JobKey = "nightly_analytics_refresh",
                JobName = "Nightly analytics refresh",
                Status = "failed",
                StartedAtUtc = nowUtc.AddHours(-2),
                FinishedAtUtc = nowUtc.AddHours(-2).AddMinutes(2),
                DurationSeconds = 120,
                FailedObjectsJson = "[\"mv_product_decision_snapshot\"]",
                ErrorMessage = "timeout",
                TriggeredBy = "nightly",
                ProcessMode = "worker",
                WorkerName = "NightlyAnalyticsRefreshWorker",
                CreatedAtUtc = nowUtc.AddHours(-2)
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetStatusAsync();

        Assert.Equal("critical", status.DataFreshnessStatus);
        Assert.Contains(status.FailedObjects, value => value == "mv_product_decision_snapshot");
    }

    [Fact]
    public async Task GetStatus_ReturnsIsRunningTrue_WhenRunningJobExists()
    {
        await using var db = CreateAnalyticsDbContext();
        var nowUtc = DateTime.UtcNow;
        db.AnalyticsRefreshRuns.Add(new AnalyticsRefreshRun
        {
            JobKey = "data_quality_snapshot",
            JobName = "Data quality snapshot",
            Status = "running",
            StartedAtUtc = nowUtc.AddMinutes(-2),
            TriggeredBy = "system",
            ProcessMode = "worker",
            WorkerName = "AnalyticsDataQualityHealthWorker",
            CreatedAtUtc = nowUtc.AddMinutes(-2)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetStatusAsync();
        var dataQualityJob = Assert.Single(status.Jobs, job => job.Key == "data_quality_snapshot");

        Assert.True(status.IsRunning);
        Assert.True(dataQualityJob.IsRunning);
    }

    [Fact]
    public async Task GetStatus_ReturnsCritical_WhenRunningJobIsStuck()
    {
        await using var db = CreateAnalyticsDbContext();
        var nowUtc = DateTime.UtcNow;
        db.AnalyticsRefreshRuns.Add(new AnalyticsRefreshRun
        {
            JobKey = "data_quality_snapshot",
            JobName = "Data quality snapshot",
            Status = "running",
            StartedAtUtc = nowUtc.AddHours(-3),
            TriggeredBy = "system",
            ProcessMode = "worker",
            WorkerName = "AnalyticsDataQualityHealthWorker",
            CreatedAtUtc = nowUtc.AddHours(-3)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetStatusAsync();
        var dataQualityJob = Assert.Single(status.Jobs, job => job.Key == "data_quality_snapshot");

        Assert.Equal("critical", status.DataFreshnessStatus);
        Assert.Equal("critical", dataQualityJob.DataFreshnessStatus);
        Assert.Equal("Refresh je započet, ali nije završen u očekivanom vremenu.", dataQualityJob.StatusReason);
    }

    [Fact]
    public async Task GetStatus_UsesFailedObjects_FromDurableHistory()
    {
        await using var db = CreateAnalyticsDbContext();
        var nowUtc = DateTime.UtcNow;
        db.AnalyticsRefreshRuns.Add(new AnalyticsRefreshRun
        {
            JobKey = "nightly_analytics_refresh",
            JobName = "Nightly analytics refresh",
            Status = "failed",
            StartedAtUtc = nowUtc.AddHours(-1),
            FinishedAtUtc = nowUtc.AddMinutes(-50),
            DurationSeconds = 600,
            FailedObjectsJson = "[\"mv_inventory_recommendations\",\"sales_facts_mv\"]",
            ErrorMessage = "refresh error",
            TriggeredBy = "manual",
            ProcessMode = "worker",
            WorkerName = "NightlyAnalyticsRefreshWorker",
            CreatedAtUtc = nowUtc.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var status = await service.GetStatusAsync();

        Assert.Contains("mv_inventory_recommendations", status.FailedObjects);
        Assert.Contains("sales_facts_mv", status.FailedObjects);
    }

    [Fact]
    public async Task GetStatus_SetsProductionInMemoryCacheWarning()
    {
        await using var db = CreateAnalyticsDbContext();
        var cache = new TestAnalyticsCacheService
        {
            IsRedisAvailable = false,
            IsRedisEnabled = false
        };
        var service = CreateService(db, cacheService: cache);

        var status = await service.GetStatusAsync();

        Assert.Equal("in-memory", status.CacheMode);
        Assert.False(status.IsDistributed);
        Assert.Equal("Analytics cache je in-memory. U multi-instance okruženju podaci mogu biti nekonzistentni između instanci.", status.CacheWarning);
    }

    private static AnalyticsDbContext CreateAnalyticsDbContext()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AnalyticsDbContext(options);
    }

    private static AnalyticsRefreshStatusService CreateService(
        AnalyticsDbContext analyticsDbContext,
        Dictionary<string, string?>? overrides = null,
        IAnalyticsCacheService? cacheService = null)
    {
        var values = new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = "worker",
                ["Workers:Enabled"] = "true"
            };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var effectiveCacheService = cacheService ?? new DisabledAnalyticsCacheService();
        return new AnalyticsRefreshStatusService(
            configuration,
            new TestHostEnvironment(),
            analyticsDbContext,
            new WorkerHealthService(),
            new WorkerRuntimeControlService(
                initialEnabled: true,
                runtimeToggleAllowed: true,
                initialSource: "test"),
            new AnalyticsCacheAdminService(
                effectiveCacheService,
                distributedCache: null,
                NullLogger<AnalyticsCacheAdminService>.Instance),
            NullLogger<AnalyticsRefreshStatusService>.Instance);
    }

    private sealed class TestAnalyticsCacheService : IAnalyticsCacheService
    {
        public bool IsRedisAvailable { get; set; }

        public bool IsRedisEnabled { get; set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => factory();

        public void SetRedisEnabled(bool enabled)
            => IsRedisEnabled = enabled;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
