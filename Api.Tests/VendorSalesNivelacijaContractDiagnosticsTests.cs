using System.Net;
using System.Text.Json;
using Api.Config;
using Api.Endpoints;
using Api.Services;
using Api.Services.DataSources;
using Infrastructure.Configuration;
using Infrastructure.Database;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class VendorSalesNivelacijaContractDiagnosticsTests : IClassFixture<PostgresContainerFixture>
{
    private const string AdminApiKey = "rq545-test-admin-key";
    private readonly PostgresContainerFixture _fixture;

    public VendorSalesNivelacijaContractDiagnosticsTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ContractInspector_UsesSearchPathAndClassifiesMissingSchemaColumnAndPrivilege()
    {
        await using var database = await TryCreateDatabaseAsync("tp_rq545_contract");
        if (database is null) return;

        var schema = $"rq545_{Guid.NewGuid():N}";
        await ExecuteAsync(database, $"CREATE SCHEMA {schema};");
        await ExecuteAsync(database,
            $"CREATE VIEW {schema}.vw_vendor_sales_nivelacija AS SELECT 1::numeric AS change_percent_revenue_semantic;");

        var inSchemaSearchPath = BuildConnectionString(database, schema);
        var resolved = await InspectAsync(inSchemaSearchPath);
        Assert.Equal(schema, resolved.ResolvedSchema);
        Assert.Contains("change_percent_revenue_semantic", resolved.Columns);
        Assert.Null(VendorSalesNivelacijaContractInspector.FindIssue(resolved));

        var publicSearchPath = BuildConnectionString(database, "public");
        var outsideSearchPath = await InspectAsync(publicSearchPath);
        var schemaIssue = VendorSalesNivelacijaContractInspector.FindIssue(outsideSearchPath);
        Assert.NotNull(schemaIssue);
        Assert.Equal("schema", schemaIssue!.MissingPart);
        Assert.Contains(schema, schemaIssue.FoundInSchemas!);

        await ExecuteAsync(database, $"DROP VIEW {schema}.vw_vendor_sales_nivelacija;");
        await ExecuteAsync(database,
            $"CREATE VIEW {schema}.vw_vendor_sales_nivelacija AS SELECT 1::numeric AS unexpected_column;");
        var wrongColumn = await InspectAsync(inSchemaSearchPath);
        var columnIssue = VendorSalesNivelacijaContractInspector.FindIssue(wrongColumn);
        Assert.NotNull(columnIssue);
        Assert.Equal("column", columnIssue!.MissingPart);
        Assert.Equal("change_percent_revenue_semantic", columnIssue.MissingColumn);
        Assert.Contains("contract_missing", columnIssue.ErrorCode);

        await ExecuteAsync(database, $"DROP VIEW {schema}.vw_vendor_sales_nivelacija;");
        var missingRelation = await InspectAsync(publicSearchPath);
        var relationIssue = VendorSalesNivelacijaContractInspector.FindIssue(missingRelation);
        Assert.NotNull(relationIssue);
        Assert.Equal("relation", relationIssue!.MissingPart);

        await ExecuteAsync(database,
            $"CREATE VIEW {schema}.vw_vendor_sales_nivelacija AS SELECT 1::numeric AS change_percent_revenue_semantic;");
        var role = $"rq545_reader_{Guid.NewGuid():N}";
        await ExecuteAsync(database,
            $"CREATE ROLE {role}; GRANT USAGE ON SCHEMA {schema} TO {role}; REVOKE ALL ON {schema}.vw_vendor_sales_nivelacija FROM {role};");

        PostgresRelationInspection restrictedInspection;
        await using (var restrictedConnection = new NpgsqlConnection(inSchemaSearchPath))
        {
            await restrictedConnection.OpenAsync();
            await ExecuteAsync(restrictedConnection, $"SET ROLE {role};");
            try
            {
                restrictedInspection = await PostgresRelationInspector.InspectAsync(
                    restrictedConnection,
                    VendorSalesNivelacijaContractInspector.RelationName);
            }
            finally
            {
                await ExecuteAsync(restrictedConnection, "RESET ROLE;");
            }
        }

        var privilegeIssue = VendorSalesNivelacijaContractInspector.FindIssue(restrictedInspection);
        Assert.NotNull(privilegeIssue);
        Assert.Equal(role, restrictedInspection.CurrentUser);
        Assert.False(restrictedInspection.HasSelectPrivilege);
        Assert.Equal("privilege", privilegeIssue!.MissingPart);
        Assert.Equal("vendor_sales_nivelacija_privilege_missing", privilegeIssue.ErrorCode);
        Assert.DoesNotContain("contract_missing", privilegeIssue.ErrorCode);
        Assert.Equal("privilege", privilegeIssue.ToDto().MissingPart);
    }

    [Fact]
    public async Task AdminDiagnostic_RequiresAdminKeyAndReturnsResolvedRelationEvidence()
    {
        await using var database = await TryCreateDatabaseAsync("tp_rq545_admin_diag");
        if (database is null) return;

        var schema = $"rq545_{Guid.NewGuid():N}";
        await ExecuteAsync(database, $"CREATE SCHEMA {schema};");
        await ExecuteAsync(database,
            $"CREATE VIEW {schema}.vw_vendor_sales_nivelacija AS SELECT 1::numeric AS change_percent_revenue_semantic, 100::numeric AS old_price, 110::numeric AS new_price;");

        var appConnectionString = BuildConnectionString(database, schema);
        await using var host = await TestHost.CreateAsync(appConnectionString);

        using var unauthorized = await host.Client.GetAsync("/api/admin/analytics/nivelacija-contract");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var wrongKeyRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/analytics/nivelacija-contract");
        wrongKeyRequest.Headers.Add("X-Admin-Key", "wrong-key");
        using var forbidden = await host.Client.SendAsync(wrongKeyRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/analytics/nivelacija-contract");
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        using var response = await host.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.Equal("vw_vendor_sales_nivelacija", root.GetProperty("relation").GetString());
        Assert.True(string.IsNullOrWhiteSpace(root.GetProperty("missingPart").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("currentUser").GetString()));
        Assert.Contains(schema, root.GetProperty("currentSchemas").EnumerateArray().Select(item => item.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("toRegclass").GetString()));
        Assert.Equal(schema, root.GetProperty("resolvedSchema").GetString());
        Assert.True(root.GetProperty("hasSelectPrivilege").GetBoolean());
        Assert.Contains(
            "change_percent_revenue_semantic",
            root.GetProperty("columns").EnumerateArray().Select(item => item.GetString()));
    }

    private async Task<FixtureDatabase?> TryCreateDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable) return null;
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return new FixtureDatabase(connectionString, connection);
    }

    private static string BuildConnectionString(FixtureDatabase database, string searchPath) =>
        new NpgsqlConnectionStringBuilder(database.ConnectionString) { SearchPath = searchPath }.ConnectionString;

    private static async Task<PostgresRelationInspection> InspectAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return await PostgresRelationInspector.InspectAsync(
            connection,
            VendorSalesNivelacijaContractInspector.RelationName);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static Task ExecuteAsync(FixtureDatabase database, string sql) => ExecuteAsync(database.Connection, sql);

    private sealed class FixtureDatabase(string connectionString, NpgsqlConnection connection) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;
        public NpgsqlConnection Connection { get; } = connection;

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
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

        public static async Task<TestHost> CreateAsync(string connectionString)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(connectionString));
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
            builder.Configuration["Admin:ApiKey"] = AdminApiKey;

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
