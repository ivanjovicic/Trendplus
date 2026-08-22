using System.Net;
using System.Net.Http.Json;
using Api.Config;
using Api.Endpoints;
using Api.Services;
using Api.Services.DataSources;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class AdminConfigOperationalReadsAuthorizationTests
{
    private const string AdminApiKey = "test-admin-key";

    public static TheoryData<string> OperationalReadRoutes => new()
    {
        "/api/admin/pending-batches",
        "/api/admin/health-check",
        "/api/admin/audit-log",
        "/api/admin/workers/list",
        "/api/admin/workers/nightly-analytics-refresh",
    };

    [Theory]
    [MemberData(nameof(OperationalReadRoutes))]
    public async Task OperationalRead_RejectsRequestWithoutAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(OperationalReadRoutes))]
    public async Task OperationalRead_RejectsRequestWithWrongAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(OperationalReadRoutes))]
    public async Task OperationalRead_AllowsRequestWithAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        // Authorized callers may get 404 only when the worker name is unknown after auth.
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected 200/404 for authorized '{route}', got {(int)response.StatusCode}.");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PendingBatches_WithAdminKey_PreservesEmptyPayloadShape()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/pending-batches?take=10");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PendingBatchesResponse>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload!.Total);
        Assert.NotNull(payload.Batches);
        Assert.Empty(payload.Batches);
    }

    [Fact]
    public async Task HealthCheck_WithAdminKey_ReturnsDiagnosticsPayload()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/health-check");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AdminHealthCheckResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.Timestamp > DateTime.MinValue);
    }

    [Fact]
    public async Task UnknownWorker_WithoutAdminKey_DoesNotRevealExistence()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.GetAsync("/api/admin/workers/does-not-exist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

        public static async Task<TestHost> CreateAsync(bool withAdminKey)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddDbContext<TrendplusDbContext>(options =>
                options.UseInMemoryDatabase($"admin-ops-reads-{Guid.NewGuid():N}"));
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
            builder.Services.Configure<AccessImportOptions>(_ => { });
            builder.Services.Configure<TrendIngestionOptions>(_ => { });
            builder.Services.Configure<NightlyAnalyticsRefreshOptions>(_ => { });
            builder.Services.Configure<OpenTrainingModelTrainingOptions>(_ => { });
            builder.Services.Configure<AnalyticsDataQualityHealthOptions>(_ => { });
            if (withAdminKey)
            {
                builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            }

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
}
