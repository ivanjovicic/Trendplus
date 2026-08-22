using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Api.Config;
using Api.Endpoints;
using SourceMappingPreviewRequest = Api.Models.SourceMappingPreviewRequest;
using SourceMappingFieldRequest = Api.Models.SourceMappingFieldRequest;
using Api.Models;
using Api.Services;
using Api.Services.DataSources;
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

    [Fact]
    public async Task MappingPreview_ReturnsBoundedRowsAndStableFingerprint()
    {
        var emptyProfileName = $"empty-{Guid.NewGuid():N}";
        await PrepareDiscoveryDataAsync();
        await using var host = await CreateHostAsync(_fixture.ConnectionString, emptyProfileName);

        var firstRequest = new SourceMappingPreviewRequest
        {
            CanonicalEntity = "source_items",
            SourceTable = "dbo.SourceItems",
            ExternalKeyColumns = ["Id"],
            Cursor = new SourceReadQuery
            {
                CursorMode = "id",
                CursorId = 0,
                IdAliases = ["Id"]
            },
            FieldMappings =
            [
                new SourceMappingFieldRequest { TargetField = "Id", Aliases = ["Id"] },
                new SourceMappingFieldRequest { TargetField = "Name", Aliases = ["Name"] },
                new SourceMappingFieldRequest { TargetField = "Quantity", Aliases = ["Quantity"] }
            ],
            Take = 50
        };

        using var firstResponse = await SendMappingPreviewAsync(host.Client, firstRequest);
        firstResponse.EnsureSuccessStatusCode();

        var firstPreview = await firstResponse.Content.ReadFromJsonAsync<SourceMappingPreviewResponse>();
        Assert.NotNull(firstPreview);
        Assert.Equal("live-sql", firstPreview!.ProfileName);
        Assert.Equal(50, firstPreview.RequestedTake);
        Assert.Equal(25, firstPreview.ReturnedRows);
        Assert.True(firstPreview.Truncated);
        Assert.Equal(25, firstPreview.Rows.Count);
        Assert.All(firstPreview.FieldMappings, mapping => Assert.Equal("matched", mapping.Status));
        Assert.DoesNotContain(firstPreview.Issues, issue => string.Equals(issue.Scope, "field", StringComparison.OrdinalIgnoreCase));

        var secondRequest = new SourceMappingPreviewRequest
        {
            CanonicalEntity = firstRequest.CanonicalEntity,
            SourceTable = firstRequest.SourceTable,
            ExternalKeyColumns = ["Id"],
            Cursor = new SourceReadQuery
            {
                CursorMode = "id",
                CursorId = 0,
                IdAliases = ["Id"]
            },
            FieldMappings =
            [
                new SourceMappingFieldRequest { TargetField = "Id", Aliases = ["Id"] },
                new SourceMappingFieldRequest { TargetField = "Name", Aliases = ["Name"] },
                new SourceMappingFieldRequest { TargetField = "Quantity", Aliases = ["Quantity"] }
            ],
            Take = 5
        };

        using var secondResponse = await SendMappingPreviewAsync(host.Client, secondRequest);
        secondResponse.EnsureSuccessStatusCode();

        var secondPreview = await secondResponse.Content.ReadFromJsonAsync<SourceMappingPreviewResponse>();
        Assert.NotNull(secondPreview);
        Assert.Equal(firstPreview.SchemaFingerprint, secondPreview!.SchemaFingerprint);
        Assert.Equal(5, secondPreview.RequestedTake);
    }

    [Fact]
    public async Task CheckpointSync_UsesLiveSqlPreviewRows_AndPersistsCheckpointState()
    {
        var emptyProfileName = $"empty-{Guid.NewGuid():N}";
        await PrepareDiscoveryDataAsync();
        var syncStore = new InMemorySourceSyncStore();
        await using var host = await CreateHostAsync(_fixture.ConnectionString, emptyProfileName, syncStore);

        var previewRequest = new SourceMappingPreviewRequest
        {
            CanonicalEntity = "source_items",
            SourceTable = "dbo.SourceItems",
            ExternalKeyColumns = ["Id"],
            Cursor = new SourceReadQuery
            {
                CursorMode = "id",
                CursorId = 0,
                IdAliases = ["Id"]
            },
            FieldMappings =
            [
                new SourceMappingFieldRequest { TargetField = "Id", Aliases = ["Id"] },
                new SourceMappingFieldRequest { TargetField = "Name", Aliases = ["Name"] },
                new SourceMappingFieldRequest { TargetField = "Quantity", Aliases = ["Quantity"] }
            ],
            Take = 2
        };

        using var previewResponse = await SendMappingPreviewAsync(host.Client, previewRequest);
        previewResponse.EnsureSuccessStatusCode();

        var preview = await previewResponse.Content.ReadFromJsonAsync<SourceMappingPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(2, preview!.Rows.Count);
        Assert.Equal("sqlserver", preview.Provider);
        Assert.Equal("live-sql", preview.ProfileName);

        var mappingProfileId = SourceMappingProfileId.Compute(
            "live-sql",
            preview.CanonicalEntity,
            preview.SourceTable,
            preview.ExternalKeyColumns.FirstOrDefault(),
            preview.Cursor?.CursorMode,
            preview.FieldMappings
                .Where(field => string.Equals(field.Status, "matched", StringComparison.OrdinalIgnoreCase))
                .Select(field => (field.TargetField, field.SourceColumn ?? string.Empty)));

        var rows = preview.Rows.Select(row =>
        {
            var externalKey = GetPreviewValue(row, "Id")?.ToString();
            return new SourceSyncRow(
                externalKey,
                null,
                externalKey,
                BuildPayloadHash(row));
        }).ToArray();

        var syncRequest = new SourceSyncBatchRequest(
            new SourceSyncIdentity("live-sql", mappingProfileId, "dbo.SourceItems"),
            preview.Cursor?.CursorMode ?? "id",
            preview.SchemaFingerprint,
            60,
            Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            rows);

        using var syncHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/data-sources/live-sql/checkpoint-sync")
        {
            Content = JsonContent.Create(syncRequest)
        };
        syncHttpRequest.Headers.Add("X-Admin-Key", AdminApiKey);

        using var syncResponse = await host.Client.SendAsync(syncHttpRequest);
        syncResponse.EnsureSuccessStatusCode();

        var result = await syncResponse.Content.ReadFromJsonAsync<SourceSyncBatchResult>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(2, result.Metrics.Read);
        Assert.Equal(2, result.Metrics.Inserted);
        Assert.Equal(0, result.Metrics.Updated);
        Assert.Equal(0, result.Metrics.Skipped);
        Assert.Equal(0, result.Metrics.Rejected);
        Assert.NotNull(result.Checkpoint);
        Assert.Equal(SourceCheckpointSyncEngine.DedicatedTenantScope, result.Checkpoint!.TenantScope);
        Assert.Equal("live-sql", result.Checkpoint.Identity.ConnectionId);
        Assert.Equal(mappingProfileId, result.Checkpoint.Identity.MappingProfileId);

        Assert.Equal(2, syncStore.Rows.Count);
        var checkpoint = syncStore.GetCheckpoint(result.Checkpoint.Identity);
        Assert.NotNull(checkpoint);
        Assert.Equal(preview.SchemaFingerprint, checkpoint!.SchemaFingerprint);
        Assert.Equal(SourceCheckpointSyncEngine.DedicatedTenantScope, checkpoint.TenantScope);
    }

    [Fact]
    public async Task MappingPreview_RejectsWithoutAdminKey()
    {
        var emptyProfileName = $"empty-{Guid.NewGuid():N}";
        await PrepareDiscoveryDataAsync();
        await using var host = await CreateHostAsync(_fixture.ConnectionString, emptyProfileName);

        using var response = await host.Client.PostAsync(
            "/api/admin/data-sources/live-sql/mapping-preview",
            JsonContent.Create(CreateMappingPreviewRequest(50)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MappingPreview_RejectsWithWrongAdminKey()
    {
        var emptyProfileName = $"empty-{Guid.NewGuid():N}";
        await PrepareDiscoveryDataAsync();
        await using var host = await CreateHostAsync(_fixture.ConnectionString, emptyProfileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/data-sources/live-sql/mapping-preview")
        {
            Content = JsonContent.Create(CreateMappingPreviewRequest(50))
        };
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

            ;WITH Numbers AS (
                SELECT 1 AS n
                UNION ALL
                SELECT n + 1
                FROM Numbers
                WHERE n < 30
            )
            INSERT INTO [dbo].[SourceItems] ([Id], [Name], [Quantity])
            SELECT n, CONCAT(N'Item ', n), n * 10
            FROM Numbers
            OPTION (MAXRECURSION 30);

            CREATE TABLE [reporting].[SourceItemsArchive] (
                [Id] INT NOT NULL PRIMARY KEY,
                [ArchivedAt] DATETIME2(0) NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HttpResponseMessage> SendMappingPreviewAsync(HttpClient client, SourceMappingPreviewRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/data-sources/live-sql/mapping-preview")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("X-Admin-Key", AdminApiKey);

        return await client.SendAsync(httpRequest);
    }

    private static SourceMappingPreviewRequest CreateMappingPreviewRequest(int take)
        => new()
        {
            CanonicalEntity = "source_items",
            SourceTable = "dbo.SourceItems",
            ExternalKeyColumns = ["Id"],
            Cursor = new SourceReadQuery
            {
                CursorMode = "id",
                CursorId = 0,
                IdAliases = ["Id"]
            },
            FieldMappings =
            [
                new SourceMappingFieldRequest { TargetField = "Id", Aliases = ["Id"] },
                new SourceMappingFieldRequest { TargetField = "Name", Aliases = ["Name"] },
                new SourceMappingFieldRequest { TargetField = "Quantity", Aliases = ["Quantity"] }
            ],
            Take = take
        };

    private static async Task<TestHost> CreateHostAsync(
        string connectionString,
        string emptyProfileName,
        InMemorySourceSyncStore? syncStore = null)
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
        syncStore ??= new InMemorySourceSyncStore();
        builder.Services.AddSingleton<ISourceSyncStore>(syncStore);
        builder.Services.AddSingleton(syncStore);
        builder.Services.AddSingleton<SourceCheckpointSyncEngine>();
        builder.Services.AddScoped<SourceCheckpointSyncService>();
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

    private static object? GetPreviewValue(SourceMappingPreviewRow row, string fieldName)
        => row.Values.FirstOrDefault(value => string.Equals(value.TargetField, fieldName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string BuildPayloadHash(SourceMappingPreviewRow row)
    {
        var builder = new StringBuilder();
        foreach (var value in row.Values.OrderBy(value => value.TargetField, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(value.TargetField);
            builder.Append('=');
            builder.Append(value.Value?.ToString() ?? string.Empty);
            builder.Append('|');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
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
