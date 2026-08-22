using System.Net;
using System.Net.Http.Json;
using Api.Config;
using Api.Endpoints;
using Api.Models;
using Api.Services;
using Api.Services.Access;
using Api.Services.DataSources;
using Domain.Model;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DemoEnvironmentVerificationEndpointTests
{
    private const string AdminApiKey = "test-admin-key";

    [Fact]
    public async Task DemoVerification_ReturnsSafe_WhenEnvironmentNameContainsDemo()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Demo",
            configuration: new Dictionary<string, string?>(),
            withAdminKey: true);

        var response = await GetResponseAsync(host, includeAdminKey: true);

        Assert.True(response.DemoSafe);
        Assert.Contains("environment_name_contains_demo", response.Reasons);
    }

    [Fact]
    public async Task DemoVerification_ReturnsSafe_WhenExplicitFlagIsEnabled()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>
            {
                ["AnalyticsDemo:Enabled"] = "true"
            },
            withAdminKey: true);

        var response = await GetResponseAsync(host, includeAdminKey: true);

        Assert.True(response.DemoSafe);
        Assert.Contains("analytics_demo_flag_enabled", response.Reasons);
    }

    [Fact]
    public async Task DemoVerification_ReturnsSafe_WhenDemoDatabaseMarkerIsPresent()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>
            {
                ["ConnectionStrings:AnalyticsConnection"] = "Host=prod-db.local;Port=5432;Database=trendplus_demo;Application Name=trendplus-app;Username=trendplus;Password=secret;"
            },
            withAdminKey: true);

        var response = await GetResponseAsync(host, includeAdminKey: true);

        Assert.True(response.DemoSafe);
        Assert.Contains("analytics_connection_database_contains_demo", response.Reasons);
        Assert.Equal("Production", response.Environment);
        Assert.NotNull(response.Warnings);
    }

    [Fact]
    public async Task DemoVerification_ReturnsSafe_WhenConnectionHostOrApplicationNameContainsDemo()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>
            {
                ["ConnectionStrings:AnalyticsConnection"] = "Host=demo-db.local;Port=5432;Database=trendplus;Application Name=trendplus-demo-verifier;Username=trendplus;Password=secret;"
            },
            withAdminKey: true);

        var response = await GetResponseAsync(host, includeAdminKey: true);

        Assert.True(response.DemoSafe);
        Assert.Contains("analytics_connection_host_contains_demo", response.Reasons);
        Assert.Contains("analytics_connection_application_name_contains_demo", response.Reasons);
        Assert.Equal("Production", response.Environment);
    }

    [Fact]
    public async Task DemoVerification_ReturnsUnsafe_WhenNoProofInputsArePresent()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>(),
            withAdminKey: true);

        var response = await GetResponseAsync(host, includeAdminKey: true);

        Assert.False(response.DemoSafe);
        Assert.Empty(response.Reasons);
        Assert.Equal("Production", response.Environment);
        Assert.Contains("connection_string_unavailable_or_unreadable", response.Warnings);
    }

    [Fact]
    public async Task DemoVerification_ResponseDoesNotExposeRawConnectionString()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>
            {
                ["ConnectionStrings:AnalyticsConnection"] = "Host=demo-db.local;Port=5432;Database=trendplus_demo;Application Name=trendplus-demo-verifier;Username=trendplus;Password=super-secret;"
            },
            withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/demo-verification");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("super-secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trendplus-demo-verifier", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demo-db.local", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DemoVerification_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>());

        using var response = await host.Client.GetAsync("/api/admin/demo-verification");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DemoVerification_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>(),
            withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/demo-verification");
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RequeueBatch_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>(),
            withAdminKey: true);

        using var response = await host.Client.PostAsync("/api/admin/requeue-batch/1", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Queue.EnqueueCallCount);
    }

    [Fact]
    public async Task RequeueBatch_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>(),
            withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/requeue-batch/1");
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Queue.EnqueueCallCount);
    }

    [Fact]
    public async Task RequeueBatch_AllowsRequestWithAdminKey_AndInvokesQueue()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>(),
            withAdminKey: true);

        long batchId;
        using (var scope = host.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            var now = DateTime.UtcNow;
            var batch = new DataImportBatch
            {
                SourceSystem = "access",
                SourceFileName = "repair.accdb",
                SourceFilePath = "/tmp/repair.accdb",
                Status = "failed",
                QueuedAtUtc = now,
                StartedAtUtc = now,
                LastHeartbeatUtc = now
            };
            db.DataImportBatches.Add(batch);
            await db.SaveChangesAsync();
            batchId = batch.Id;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/requeue-batch/{batchId}");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<RequeueResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.Success, payload.Message);
        Assert.Equal(1, host.Queue.EnqueueCallCount);
    }

    [Fact]
    public async Task RunStaleRecovery_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>(),
            withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/run-stale-recovery");
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.ImportService.RefreshBatchStatusesCallCount);
    }

    [Fact]
    public async Task RunStaleRecovery_AllowsRequestWithAdminKey_AndInvokesImportService()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>(),
            withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/run-stale-recovery");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, host.ImportService.RefreshBatchStatusesCallCount);
    }

    private static async Task<DemoEnvironmentVerificationResponse> GetResponseAsync(
        TestHost host,
        bool includeAdminKey = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/demo-verification");
        if (includeAdminKey)
        {
            request.Headers.Add("X-Admin-Key", AdminApiKey);
        }

        using var response = await host.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DemoEnvironmentVerificationResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private static readonly InMemoryDatabaseRoot DatabaseRoot = new();

        private TestHost(WebApplication app, RecordingAccessImportJobQueue queue, RecordingAccessImportService importService)
        {
            App = app;
            Client = app.GetTestClient();
            Queue = queue;
            ImportService = importService;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public RecordingAccessImportJobQueue Queue { get; }
        public RecordingAccessImportService ImportService { get; }

        public static async Task<TestHost> CreateAsync(
            string environmentName,
            IDictionary<string, string?> configuration,
            bool withAdminKey = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environmentName
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            // Capture the database name once so every scoped DbContext shares the same InMemory store.
            // Evaluating Guid.NewGuid() inside the options lambda can create a fresh empty database per scope.
            var databaseName = $"demo-verification-{Guid.NewGuid():N}";
            builder.Services.AddDbContext<TrendplusDbContext>(options =>
                options.UseInMemoryDatabase(databaseName, DatabaseRoot));
            builder.Services.AddSingleton<WorkerHealthService>();
            builder.Services.AddSingleton(new WorkerRuntimeControlService(
                initialEnabled: true,
                runtimeToggleAllowed: false,
                initialSource: "test"));
            builder.Services.AddScoped<WorkerConfigurationService>();
            builder.Services.AddScoped<WorkerRegistryService>();
            var syncStore = new InMemorySourceSyncStore();
            builder.Services.AddSingleton<ISourceSyncStore>(syncStore);
            builder.Services.AddSingleton(syncStore);
            builder.Services.AddSingleton<SourceCheckpointSyncEngine>();
            builder.Services.AddScoped<SourceCheckpointSyncService>();
            var queue = new RecordingAccessImportJobQueue();
            var importService = new RecordingAccessImportService();
            builder.Services.AddSingleton<IAccessImportJobQueue>(queue);
            builder.Services.AddSingleton<IAccessImportService>(importService);
            builder.Services.Configure<AccessImportOptions>(_ => { });
            builder.Services.Configure<TrendIngestionOptions>(_ => { });
            builder.Services.Configure<NightlyAnalyticsRefreshOptions>(_ => { });
            builder.Services.Configure<OpenTrainingModelTrainingOptions>(_ => { });
            builder.Services.Configure<AnalyticsDataQualityHealthOptions>(_ => { });
            // Force empty connection strings unless the caller explicitly supplies them.
            // WebApplication.CreateBuilder still loads process env (e.g. GHA ConnectionStrings__*),
            // so an empty caller dictionary alone would leave CI connection strings readable.
            var effectiveConfiguration = new Dictionary<string, string?>(configuration, StringComparer.OrdinalIgnoreCase);
            if (!effectiveConfiguration.ContainsKey("ConnectionStrings:AnalyticsConnection"))
            {
                effectiveConfiguration["ConnectionStrings:AnalyticsConnection"] = string.Empty;
            }

            if (!effectiveConfiguration.ContainsKey("ConnectionStrings:DefaultConnection"))
            {
                effectiveConfiguration["ConnectionStrings:DefaultConnection"] = string.Empty;
            }

            if (withAdminKey)
            {
                builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            }
            builder.Configuration.AddInMemoryCollection(effectiveConfiguration);

            var app = builder.Build();
            app.UseRouting();
            app.MapAdminConfigEndpoints();
            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<TrendplusDbContext>().Database.EnsureCreated();
            }
            await app.StartAsync();

            return new TestHost(app, queue, importService);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class RecordingAccessImportJobQueue : IAccessImportJobQueue
    {
        public int EnqueueCallCount { get; private set; }

        public Task EnqueueAsync(long batchId, CancellationToken ct = default)
        {
            EnqueueCallCount++;
            return Task.CompletedTask;
        }

        public Task<AccessImportQueuedJob?> ClaimNextAsync(CancellationToken ct = default)
            => Task.FromResult<AccessImportQueuedJob?>(null);

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
    }
}
