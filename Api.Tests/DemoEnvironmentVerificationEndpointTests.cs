using System.Net.Http.Json;
using Api.Config;
using Api.Endpoints;
using Api.Models;
using Api.Services;
using Api.Services.Access;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DemoEnvironmentVerificationEndpointTests
{
    [Fact]
    public async Task DemoVerification_ReturnsSafe_WhenEnvironmentNameContainsDemo()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Demo",
            configuration: new Dictionary<string, string?>());

        var response = await GetResponseAsync(host);

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
            });

        var response = await GetResponseAsync(host);

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
                ["ConnectionStrings:AnalyticsConnection"] = "Host=demo-db.local;Port=5432;Database=trendplus_demo;Username=trendplus;Password=secret;"
            });

        var response = await GetResponseAsync(host);

        Assert.True(response.DemoSafe);
        Assert.Contains("analytics_connection_database_contains_demo", response.Reasons);
    }

    [Fact]
    public async Task DemoVerification_ReturnsUnsafe_WhenNoProofInputsArePresent()
    {
        await using var host = await TestHost.CreateAsync(
            environmentName: "Production",
            configuration: new Dictionary<string, string?>());

        var response = await GetResponseAsync(host);

        Assert.False(response.DemoSafe);
        Assert.Empty(response.Reasons);
    }

    private static async Task<DemoEnvironmentVerificationResponse> GetResponseAsync(TestHost host)
    {
        using var response = await host.Client.GetAsync("/api/admin/demo-verification");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DemoEnvironmentVerificationResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<TestHost> CreateAsync(string environmentName, IDictionary<string, string?> configuration)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environmentName
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddDbContext<TrendplusDbContext>(options =>
                options.UseInMemoryDatabase($"demo-verification-{Guid.NewGuid():N}"));
            builder.Services.AddSingleton<WorkerHealthService>();
            builder.Services.AddSingleton(new WorkerRuntimeControlService(
                initialEnabled: true,
                runtimeToggleAllowed: false,
                initialSource: "test"));
            builder.Services.AddScoped<WorkerConfigurationService>();
            builder.Services.AddScoped<WorkerRegistryService>();
            builder.Services.AddSingleton<IAccessImportJobQueue, NoOpAccessImportJobQueue>();
            builder.Services.AddSingleton<IAccessImportService, NoOpAccessImportService>();
            builder.Services.Configure<AccessImportOptions>(_ => { });
            builder.Services.Configure<TrendIngestionOptions>(_ => { });
            builder.Services.Configure<NightlyAnalyticsRefreshOptions>(_ => { });
            builder.Services.Configure<OpenTrainingModelTrainingOptions>(_ => { });
            builder.Services.Configure<AnalyticsDataQualityHealthOptions>(_ => { });
            builder.Configuration.AddInMemoryCollection(configuration);

            var app = builder.Build();
            app.UseRouting();
            app.MapAdminConfigEndpoints();
            await app.StartAsync();

            return new TestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class NoOpAccessImportJobQueue : IAccessImportJobQueue
    {
        public Task EnqueueAsync(long batchId, CancellationToken ct = default) => Task.CompletedTask;

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

    private sealed class NoOpAccessImportService : IAccessImportService
    {
        public Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<AccessImportPreviewResponse>(null!);

        public Task<AccessImportRunResponse> ImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<AccessImportRunResponse>(null!);

        public Task<AccessImportRunResponse> StartImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<AccessImportRunResponse>(null!);

        public Task<AccessImportRunResponse> RunExistingBatchAsync(long batchId, string accessFilePath, string sourceFileName, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, bool deleteWorkingFileAfterCompletion = false, CancellationToken ct = default)
            => Task.FromResult<AccessImportRunResponse>(null!);

        public Task RefreshBatchStatusesAsync(long? batchId = null, CancellationToken ct = default)
            => Task.CompletedTask;

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
