using System.Net;
using System.Text.Json;
using Api.Services.Startup;
using Infrastructure.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public sealed class StartupReadinessEndpointTests
{
    [Fact]
    public async Task Ready_ExposesOnlySafeInitializationSummaryAnonymously()
    {
        var readiness = CreateReadyState();
        readiness.MarkDatabaseInitializationNotRequired();
        await using var host = await CreateHostAsync(readiness);

        using var response = await host.Client.GetAsync("/ready");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(json);
        var initialization = document.RootElement.GetProperty("databaseInitialization");
        Assert.Equal("not_required", initialization.GetProperty("state").GetString());
        Assert.False(initialization.GetProperty("required").GetBoolean());
        Assert.Equal(0, initialization.GetProperty("attempts").GetInt32());
        Assert.False(json.Contains("failureStage", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("scriptPath", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("AutoMigrate", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("FailFast", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ready_CompletedWithErrorsIsAvailableButExplicitlyDegraded()
    {
        var readiness = CreateReadyState();
        readiness.RequireDatabaseInitialization();
        readiness.BeginDatabaseInitializationAttempt();
        readiness.MarkDatabaseInitializationCompleted(
            DatabaseInitializationOutcome.WithErrors("schema_verification_failed", "vendor_view_verification"));
        await using var host = await CreateHostAsync(readiness);

        using var response = await host.Client.GetAsync("/ready");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("degraded", document.RootElement.GetProperty("status").GetString());
        Assert.True(document.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("ready_degraded_schema", document.RootElement.GetProperty("reason").GetString());
        Assert.Equal("completed_with_errors", document.RootElement.GetProperty("databaseInitialization").GetProperty("state").GetString());
    }

    [Fact]
    public async Task Ready_FailedStrictInitializationRemainsUnavailable()
    {
        var readiness = CreateReadyState();
        readiness.RequireDatabaseInitialization();
        readiness.BeginDatabaseInitializationAttempt();
        readiness.RecordDatabaseInitializationFailure("schema_verification_failed", "vendor_view_verification");
        readiness.MarkDatabaseInitializationFailed();
        await using var host = await CreateHostAsync(readiness);

        using var response = await host.Client.GetAsync("/ready");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(document.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("failed", document.RootElement.GetProperty("databaseInitialization").GetProperty("state").GetString());
        Assert.Equal("schema_verification_failed", document.RootElement.GetProperty("databaseInitialization").GetProperty("failureCategory").GetString());
        Assert.False(document.RootElement.ToString().Contains("vendor_view_verification", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ready_PendingInitializationRemainsUnavailableWithRetryAfter()
    {
        var readiness = CreateReadyState();
        readiness.RequireDatabaseInitialization();
        readiness.BeginDatabaseInitializationAttempt();
        await using var host = await CreateHostAsync(readiness);

        using var response = await host.Client.GetAsync("/ready");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("5", response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString());
        Assert.Equal("pending", document.RootElement.GetProperty("databaseInitialization").GetProperty("state").GetString());
    }

    private static StartupReadinessState CreateReadyState()
    {
        var state = new StartupReadinessState();
        state.ReportProbe(
            new StartupReadinessState.DatabaseProbeState { Ok = true, LatencyMs = 2 },
            new StartupReadinessState.DatabaseProbeState { Ok = true, LatencyMs = 3 });
        state.MarkReady();
        return state;
    }

    private static async Task<ReadinessTestHost> CreateHostAsync(StartupReadinessState readiness)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(readiness);
        var app = builder.Build();
        app.MapStartupReadinessEndpoint(_ => "test");
        await app.StartAsync();
        return new ReadinessTestHost(app, app.GetTestClient());
    }

    private sealed class ReadinessTestHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
