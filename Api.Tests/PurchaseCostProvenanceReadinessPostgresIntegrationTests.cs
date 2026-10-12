using System.Net;
using System.Text.Json;
using Api.Services.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Collection(IntelligencePostgresCollection.Name)]
public sealed class PurchaseCostProvenanceReadinessPostgresIntegrationTests
{
    private readonly IntelligencePostgresFixture _fixture;

    public PurchaseCostProvenanceReadinessPostgresIntegrationTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AutoMigrationDisabled_MissingContractKeepsReadyEndpointUnavailableWithoutDdl()
    {
        var database = await _fixture.CreateDatabaseAsync("provenance_readiness_missing");
        await using (var connection = await _fixture.OpenConnectionAsync(database))
        await using (var create = new NpgsqlCommand(
                         "CREATE TABLE public.prodaja_stavke (id integer PRIMARY KEY, nabavna_cena numeric(18,2) NULL);",
                         connection))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using var app = await CreateReadinessAppAsync(database.ConnectionString);
        var payload = await WaitForReadinessDecisionAsync(app, expectedReady: false);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, payload.StatusCode);
        Assert.Equal("schema_incompatible", payload.Root.GetProperty("reason").GetString());
        Assert.False(payload.Root.GetProperty("ready").GetBoolean());
        Assert.Equal("failed", payload.Root.GetProperty("status").GetString());
        Assert.Equal(
            "schema_contract_incompatible",
            payload.Root.GetProperty("databaseInitialization").GetProperty("failureCategory").GetString());

        await using var verifyConnection = await _fixture.OpenConnectionAsync(database);
        await using var verify = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relname = 'prodaja_stavke' AND a.attname = 'purchase_cost_provenance' AND a.attnum > 0 AND NOT a.attisdropped;",
            verifyConnection);
        Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Could not verify that the read-only readiness path did not change the schema.")));
    }

    [Fact]
    public async Task AutoMigrationDisabled_ValidContractBecomesReadyAfterLiveDatabaseProbes()
    {
        var database = await _fixture.CreateDatabaseAsync("provenance_readiness_valid");
        await using (var connection = await _fixture.OpenConnectionAsync(database))
        await using (var create = new NpgsqlCommand(
                         "CREATE TABLE public.prodaja_stavke (id integer PRIMARY KEY, nabavna_cena numeric(18,2) NULL, purchase_cost_provenance character varying(64) NULL);",
                         connection))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using var app = await CreateReadinessAppAsync(database.ConnectionString);
        var payload = await WaitForReadinessDecisionAsync(app, expectedReady: true);

        Assert.Equal(HttpStatusCode.OK, payload.StatusCode);
        Assert.True(payload.Root.GetProperty("ready").GetBoolean());
        Assert.True(payload.Root.GetProperty("db").GetProperty("ok").GetBoolean());
        Assert.Equal("not_required", payload.Root.GetProperty("databaseInitialization").GetProperty("state").GetString());
    }

    private static async Task<WebApplication> CreateReadinessAppAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["ConnectionStrings:AnalyticsConnection"] = connectionString,
            ["Database:AutoMigrate"] = "false",
            ["StartupReadiness:MaxWarmupAttempts"] = "1",
            ["StartupReadiness:RetryDelaySeconds"] = "1",
            ["Analytics:AllowLoopbackInProduction"] = "true"
        });
        var readiness = new StartupReadinessState();
        readiness.MarkDatabaseInitializationNotRequired();
        builder.Services.AddSingleton(readiness);
        builder.Services.AddHostedService<ReadinessWarmupHostedService>();

        var app = builder.Build();
        app.MapStartupReadinessEndpoint(_ => "test");
        await app.StartAsync();
        return app;
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Root)> WaitForReadinessDecisionAsync(
        WebApplication app,
        bool expectedReady)
    {
        using var client = app.GetTestClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string? lastBody = null;
        HttpStatusCode? lastStatus = null;

        while (!timeout.IsCancellationRequested)
        {
            using var response = await client.GetAsync("/ready", timeout.Token);
            lastStatus = response.StatusCode;
            lastBody = await response.Content.ReadAsStringAsync(timeout.Token);
            using var document = JsonDocument.Parse(lastBody);
            var root = document.RootElement.Clone();
            var ready = root.GetProperty("ready").GetBoolean();
            var reason = root.GetProperty("reason").GetString();

            if (expectedReady && ready)
                return (response.StatusCode, root);

            if (!expectedReady && string.Equals(reason, "schema_incompatible", StringComparison.Ordinal))
                return (response.StatusCode, root);

            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }

        throw new TimeoutException(
            $"Readiness did not reach the expected schema-contract state. Last response: HTTP {(int?)lastStatus}: {lastBody}");
    }
}
