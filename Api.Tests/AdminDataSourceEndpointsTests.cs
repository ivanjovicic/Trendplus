using System.Net;
using System.Net.Http.Json;
using Api.Config;
using Api.Endpoints;
using Api.Services;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Collection(SqlServerSourceDataSessionCollection.CollectionName)]
public sealed class AdminDataSourceEndpointsTests
{
    private const string AdminApiKey = "test-admin-key";

    private readonly SqlServerSourceDataSessionFixture _fixture;

    public AdminDataSourceEndpointsTests(SqlServerSourceDataSessionFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<HttpMethod, string> ProtectedRoutes => new()
    {
        { HttpMethod.Get, "/api/admin/data-sources/profiles" },
        { HttpMethod.Post, "/api/admin/data-sources/live-sql/test" },
        { HttpMethod.Post, "/api/admin/data-sources/unclaimed-sql/test" },
        { HttpMethod.Get, "/api/admin/data-sources/live-sql/schemas" },
        { HttpMethod.Get, "/api/admin/data-sources/live-sql/tables?schema=dbo" },
        { HttpMethod.Get, "/api/admin/data-sources/live-sql/columns?schema=dbo&table=SourceItems" },
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoutes_RejectWithoutAdminKey(HttpMethod method, string route)
    {
        await using var host = await CreateHostAsync(_fixture.ConnectionString, $"empty-{Guid.NewGuid():N}");

        using var response = await SendAsync(host.Client, method, route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoutes_RejectWithWrongAdminKey(HttpMethod method, string route)
    {
        await using var host = await CreateHostAsync(_fixture.ConnectionString, $"empty-{Guid.NewGuid():N}");

        using var request = new HttpRequestMessage(method, route);
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Profiles_ReturnConfiguredEntriesWithoutSecrets()
    {
        var emptyProfileName = $"empty-{Guid.NewGuid():N}";
        await using var host = await CreateHostAsync(_fixture.ConnectionString, emptyProfileName);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/data-sources/profiles");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<NamedDataSourceProfileDto[]>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload!.Length);

        var live = Assert.Single(payload, item => string.Equals(item.Name, "live-sql", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("sqlserver", live.Provider);
        Assert.Equal("Live SQL", live.DisplayName);
        Assert.True(live.Configured);

        var empty = Assert.Single(payload, item => string.Equals(item.Name, emptyProfileName, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("sqlserver", empty.Provider);
        Assert.False(empty.Configured);

        var oracle = Assert.Single(payload, item => string.Equals(item.Name, "oracle-sql", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("oracle", oracle.Provider);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password=", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoveryEndpoints_ReturnSafeResultsForConfiguredSqlServerProfile()
    {
        var emptyProfileName = $"empty-{Guid.NewGuid():N}";
        await PrepareDiscoveryDataAsync();
        await using var host = await CreateHostAsync(_fixture.ConnectionString, emptyProfileName);

        using var connectionTestRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/data-sources/live-sql/test");
        connectionTestRequest.Headers.Add("X-Admin-Key", AdminApiKey);

        using var connectionTestResponse = await host.Client.SendAsync(connectionTestRequest);
        connectionTestResponse.EnsureSuccessStatusCode();
        var connectionTest = await connectionTestResponse.Content.ReadFromJsonAsync<DataSourceConnectionTestResponse>();
        Assert.NotNull(connectionTest);
        Assert.True(connectionTest!.Success);
        Assert.Equal("success", connectionTest.Category);

        using var emptyProfileRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/data-sources/{emptyProfileName}/test");
        emptyProfileRequest.Headers.Add("X-Admin-Key", AdminApiKey);

        using var emptyProfileResponse = await host.Client.SendAsync(emptyProfileRequest);
        emptyProfileResponse.EnsureSuccessStatusCode();
        var emptyProfileTest = await emptyProfileResponse.Content.ReadFromJsonAsync<DataSourceConnectionTestResponse>();
        Assert.NotNull(emptyProfileTest);
        Assert.False(emptyProfileTest!.Success);
        Assert.Equal("invalid_configuration", emptyProfileTest.Category);

        using var unsupportedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/data-sources/oracle-sql/test");
        unsupportedRequest.Headers.Add("X-Admin-Key", AdminApiKey);

        using var unsupportedResponse = await host.Client.SendAsync(unsupportedRequest);
        unsupportedResponse.EnsureSuccessStatusCode();
        var unsupported = await unsupportedResponse.Content.ReadFromJsonAsync<DataSourceConnectionTestResponse>();
        Assert.NotNull(unsupported);
        Assert.False(unsupported!.Success);
        Assert.Equal("unsupported_provider", unsupported.Category);

        using var schemasRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/data-sources/live-sql/schemas");
        schemasRequest.Headers.Add("X-Admin-Key", AdminApiKey);

        using var schemasResponse = await host.Client.SendAsync(schemasRequest);
        schemasResponse.EnsureSuccessStatusCode();
        var schemas = await schemasResponse.Content.ReadFromJsonAsync<DataSourceSchemasResponse>();
        Assert.NotNull(schemas);
        Assert.Equal("live-sql", schemas!.ProfileName);
        Assert.Contains("dbo", schemas.Schemas);
        Assert.Contains("reporting", schemas.Schemas);

        using var tablesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/data-sources/live-sql/tables?schema=dbo");
        tablesRequest.Headers.Add("X-Admin-Key", AdminApiKey);

        using var tablesResponse = await host.Client.SendAsync(tablesRequest);
        tablesResponse.EnsureSuccessStatusCode();
        var tables = await tablesResponse.Content.ReadFromJsonAsync<DataSourceTablesResponse>();
        Assert.NotNull(tables);
        Assert.Equal("dbo", tables!.Schema);
        Assert.Contains("SourceItems", tables.Tables);

        using var columnsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/data-sources/live-sql/columns?schema=dbo&table=SourceItems");
        columnsRequest.Headers.Add("X-Admin-Key", AdminApiKey);

        using var columnsResponse = await host.Client.SendAsync(columnsRequest);
        columnsResponse.EnsureSuccessStatusCode();
        var columns = await columnsResponse.Content.ReadFromJsonAsync<DataSourceColumnsResponse>();
        Assert.NotNull(columns);
        Assert.Equal("dbo", columns!.Schema);
        Assert.Equal("SourceItems", columns.Table);
        Assert.Equal(["Id", "Name", "Quantity"], columns.Columns);
    }

    private async Task PrepareDiscoveryDataAsync()
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = """
            IF SCHEMA_ID(N'reporting') IS NULL
                EXEC(N'CREATE SCHEMA reporting');

            IF OBJECT_ID(N'[dbo].[SourceItems]', N'U') IS NOT NULL
                DROP TABLE [dbo].[SourceItems];

            IF OBJECT_ID(N'[reporting].[SourceItemsArchive]', N'U') IS NOT NULL
                DROP TABLE [reporting].[SourceItemsArchive];

            CREATE TABLE [dbo].[SourceItems] (
                [Id] INT NOT NULL PRIMARY KEY,
                [Name] NVARCHAR(100) NOT NULL,
                [Quantity] INT NOT NULL
            );

            CREATE TABLE [reporting].[SourceItemsArchive] (
                [Id] INT NOT NULL PRIMARY KEY,
                [ArchivedAt] DATETIME2(0) NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<TestHost> CreateHostAsync(string connectionString, string emptyProfileName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddLogging();
        builder.Services.AddDbContext<TrendplusDbContext>(options =>
            options.UseInMemoryDatabase($"admin-data-sources-{Guid.NewGuid():N}"));
        builder.Services.AddSingleton<WorkerHealthService>();
        builder.Services.AddSingleton(new WorkerRuntimeControlService(
            initialEnabled: true,
            runtimeToggleAllowed: false,
            initialSource: "test"));
        builder.Services.AddScoped<WorkerConfigurationService>();
        builder.Services.AddScoped<WorkerRegistryService>();
        builder.Services.Configure<AccessImportOptions>(_ => { });
        builder.Services.Configure<TrendIngestionOptions>(_ => { });
        builder.Services.Configure<NightlyAnalyticsRefreshOptions>(_ => { });
        builder.Services.Configure<OpenTrainingModelTrainingOptions>(_ => { });
        builder.Services.Configure<AnalyticsDataQualityHealthOptions>(_ => { });
        builder.Configuration["Admin:ApiKey"] = AdminApiKey;
        builder.Configuration["DataSources:NamedProfiles:live-sql:Provider"] = "sqlserver";
        builder.Configuration["DataSources:NamedProfiles:live-sql:DisplayName"] = "Live SQL";
        builder.Configuration["DataSources:NamedProfiles:live-sql:ConnectionString"] = connectionString;
        builder.Configuration[$"DataSources:NamedProfiles:{emptyProfileName}:Provider"] = "sqlserver";
        builder.Configuration[$"DataSources:NamedProfiles:{emptyProfileName}:DisplayName"] = "Empty SQL";
        builder.Configuration["DataSources:NamedProfiles:oracle-sql:Provider"] = "oracle";
        builder.Configuration["DataSources:NamedProfiles:oracle-sql:ConnectionString"] = "Data Source=example;User Id=demo;Password=secret";

        var app = builder.Build();
        app.UseRouting();
        app.MapAdminConfigEndpoints();
        await app.StartAsync();
        return new TestHost(app);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string route)
    {
        using var request = new HttpRequestMessage(method, route);
        return await client.SendAsync(request);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        public TestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}
