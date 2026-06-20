using Api.Config;
using Api.Endpoints;
using Api.Services;
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
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests;

public sealed class WorkerConfigurationEndpointsTests
{
    [Fact]
    public async Task GetConfiguration_ReturnsAllRegistryWorkers()
    {
        await using var host = await WorkerConfigurationTestHost.CreateAsync();

        using var response = await host.Client.GetAsync("/api/workers/configuration");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WorkerConfigurationResponseDto>();
        Assert.NotNull(payload);
        Assert.True(payload!.Workers.Count > 1);
        Assert.Equal(WorkerRegistryCatalog.Definitions.Count, payload.Workers.Count);

        foreach (var definition in WorkerRegistryCatalog.Definitions)
        {
            Assert.Contains(payload.Workers, w => w.WorkerName == definition.WorkerName);
        }
    }

    [Fact]
    public async Task StartWorker_RejectsNonAdminRequest_InProduction()
    {
        await using var host = await WorkerConfigurationTestHost.CreateAsync();

        using var response = await host.Client.PostAsync("/api/workers/AccessImportBackgroundWorker/start", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartWorker_RejectsRequest_WhenNoAdminKeyIsConfigured()
    {
        await using var host = await WorkerConfigurationTestHost.CreateAsync(withAdminKey: false);

        using var response = await host.Client.PostAsync("/api/workers/TrendIngestionWorker/start", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartWorker_RejectsRequest_WithWrongAdminKey()
    {
        await using var host = await WorkerConfigurationTestHost.CreateAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/workers/TrendIngestionWorker/start");
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StartWorker_WithScheduleEnabled_SetsManualRunRequest()
    {
        // "Pokreni odmah" must work even when the schedule is enabled.
        await using var host = await WorkerConfigurationTestHost.CreateAsync();
        const string workerName = "NightlyAnalyticsRefreshWorker";

        // Schedule is enabled by default; do NOT disable it first.
        using (var scope = host.App.Services.CreateScope())
        {
            var policyService = scope.ServiceProvider.GetRequiredService<WorkerRuntimePolicyService>();
            var before = await policyService.GetPolicyAsync(workerName);
            Assert.True(before.IsScheduleEnabled, "Precondition: schedule should be enabled for this test.");
        }

        var startRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/workers/{workerName}/start");
        startRequest.Headers.Add("X-Admin-Key", WorkerConfigurationTestHost.AdminApiKey);
        using var startResponse = await host.Client.SendAsync(startRequest);
        startResponse.EnsureSuccessStatusCode();

        using var scope2 = host.App.Services.CreateScope();
        var policy = await scope2.ServiceProvider.GetRequiredService<WorkerRuntimePolicyService>().GetPolicyAsync(workerName);
        Assert.True(policy.ManualRunRequested, "ManualRunRequested should be true after start with schedule enabled.");
        Assert.True(policy.CanRunNow, "CanRunNow should be true.");
    }

    [Fact]
    public async Task StartWorker_ReturnsNotFound_ForUnknownWorkerName()
    {
        await using var host = await WorkerConfigurationTestHost.CreateAsync();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/workers/NonExistentWorkerXYZ/start");
        req.Headers.Add("X-Admin-Key", WorkerConfigurationTestHost.AdminApiKey);
        using var response = await host.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ScheduleDisable_BlocksAutomaticRun_ManualStartStillAllowsSingleRun()
    {
        await using var host = await WorkerConfigurationTestHost.CreateAsync();
        const string workerName = "TrendIngestionWorker";

        var disableRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/workers/{workerName}/schedule/disable");
        disableRequest.Headers.Add("X-Admin-Key", WorkerConfigurationTestHost.AdminApiKey);
        using var disableResponse = await host.Client.SendAsync(disableRequest);
        disableResponse.EnsureSuccessStatusCode();

        using (var scope = host.App.Services.CreateScope())
        {
            var policyService = scope.ServiceProvider.GetRequiredService<WorkerRuntimePolicyService>();
            var policy = await policyService.GetPolicyAsync(workerName);
            Assert.False(policy.IsScheduleEnabled);
            Assert.False(policy.CanRunNow);
        }

        var startRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/workers/{workerName}/start");
        startRequest.Headers.Add("X-Admin-Key", WorkerConfigurationTestHost.AdminApiKey);
        using var startResponse = await host.Client.SendAsync(startRequest);
        startResponse.EnsureSuccessStatusCode();

        using (var scope = host.App.Services.CreateScope())
        {
            var policyService = scope.ServiceProvider.GetRequiredService<WorkerRuntimePolicyService>();
            var policy = await policyService.GetPolicyAsync(workerName);

            Assert.False(policy.IsScheduleEnabled);
            Assert.True(policy.ManualRunRequested);
            Assert.True(policy.CanRunNow);
            Assert.False(string.IsNullOrWhiteSpace(policy.ManualRunToken));

            var consumed = await policyService.TryConsumeManualRunRequestAsync(workerName, policy.ManualRunToken!);
            Assert.True(consumed);

            var afterConsume = await policyService.GetPolicyAsync(workerName);
            Assert.False(afterConsume.CanRunNow);
            Assert.False(afterConsume.ManualRunRequested);
        }
    }

    private sealed class WorkerConfigurationTestHost : IAsyncDisposable
    {
        public const string AdminApiKey = "test-admin-key";

        private WorkerConfigurationTestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<WorkerConfigurationTestHost> CreateAsync(bool withAdminKey = true)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();

            var config = new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = "worker",
                ["Workers:Enabled"] = "true",
                ["AccessImport:WorkerEnabled"] = "true"
            };
            if (withAdminKey)
                config["Admin:ApiKey"] = AdminApiKey;

            builder.Configuration.AddInMemoryCollection(config);

            builder.Services.AddRouting();
            builder.Services.AddLogging();

            var dbName = $"worker-config-tests-{Guid.NewGuid():N}";
            builder.Services.AddDbContext<TrendplusDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            builder.Services.AddSingleton<WorkerHealthService>();
            builder.Services.AddSingleton(new WorkerRuntimeControlService(
                initialEnabled: true,
                runtimeToggleAllowed: true,
                initialSource: "test"));
            builder.Services.AddScoped<WorkerConfigurationService>();
            builder.Services.AddSingleton<WorkerRuntimePolicyService>();
            builder.Services.AddScoped<WorkerRegistryService>();

            builder.Services.Configure<AccessImportOptions>(_ => { });
            builder.Services.Configure<TrendIngestionOptions>(_ => { });
            builder.Services.Configure<NightlyAnalyticsRefreshOptions>(_ => { });
            builder.Services.Configure<OpenTrainingModelTrainingOptions>(_ => { });
            builder.Services.Configure<AnalyticsDataQualityHealthOptions>(_ => { });

            var app = builder.Build();
            app.MapWorkerConfigurationEndpoints();
            await app.StartAsync();

            return new WorkerConfigurationTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}
