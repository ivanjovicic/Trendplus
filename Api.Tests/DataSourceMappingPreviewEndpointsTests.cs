using System.Net;
using System.Text;
using System.Text.Json;
using Api.Config;
using Api.Models;
using Api.Services.DataSources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.RateLimiting;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DataSourceMappingPreviewEndpointsTests : IClassFixture<SqlServerContainerFixture>
{
    private const string AdminApiKey = "test-admin-key";
    private readonly SqlServerContainerFixture _fixture;

    public DataSourceMappingPreviewEndpointsTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MappingPreview_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(_ => { });
        using var content = CreateJsonContent(new DataSourceMappingPreviewRequest
        {
            CanonicalEntity = "Product",
            Table = "[sales].[Order]",
            ExternalKeyColumns = ["ID"]
        });

        using var response = await host.Client.PostAsync("/api/data-sources/probe/mapping-preview", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MappingPreview_SqlServerResponseIncludesFingerprintMetadataAndBoundedRows()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await _fixture.CreateSeededConnectionStringAsync();
        await using var host = await TestHost.CreateAsync(options =>
        {
            options.PreviewSampleLimit = 2;
            options.PreviewTimeoutSeconds = 15;
            options.Profiles.Add(new NamedDataSourceProfileOptions
            {
                Name = "pilot-sql",
                Provider = "sqlserver",
                ConnectionString = connectionString,
                DefaultSchema = "sales"
            });
        });

        var request = new DataSourceMappingPreviewRequest
        {
            CanonicalEntity = "Product",
            Table = "[sales].[Order]",
            ExternalKeyColumns = ["ID"],
            Cursor = new DataSourceCursorSelection
            {
                Mode = "timestamp_then_id",
                TimestampColumn = "Updated At",
                TieBreakerColumn = "ID"
            },
            ColumnMappings =
            [
                new DataSourceFieldMappingSelection
                {
                    TargetField = "name",
                    SourceColumn = "Naziv",
                    Transforms = ["trim"]
                },
                new DataSourceFieldMappingSelection
                {
                    TargetField = "price",
                    SourceColumn = "Price"
                }
            ],
            SampleSize = 10
        };

        using var response = await SendAuthorizedAsync(host.Client, "/api/data-sources/pilot-sql/mapping-preview", request);
        var payload = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;

        Assert.True(root.GetProperty("canPreview").GetBoolean());
        Assert.True(root.GetProperty("canSync").GetBoolean());
        Assert.Equal(2, root.GetProperty("sampleSize").GetInt32());
        Assert.Equal(2, root.GetProperty("previewedRows").GetInt32());
        Assert.Equal(3, root.GetProperty("rowCount").GetInt32());
        Assert.Equal("exact", root.GetProperty("rowCountMode").GetString());

        var fingerprint = root.GetProperty("schemaFingerprint").GetString();
        Assert.NotNull(fingerprint);
        Assert.Equal(64, fingerprint!.Length);

        var columns = root.GetProperty("columns").EnumerateArray().ToArray();
        Assert.Contains(columns, column =>
            column.GetProperty("name").GetString() == "ID"
            && column.GetProperty("sourceType").GetString() == "bigint"
            && column.GetProperty("isNullable").GetBoolean() == false);
        Assert.Contains(columns, column =>
            column.GetProperty("name").GetString() == "Optional Note"
            && column.GetProperty("sourceType").GetString() == "nvarchar"
            && column.GetProperty("isNullable").GetBoolean());

        var fieldMappings = root.GetProperty("fieldMappings").EnumerateArray().ToArray();
        Assert.Contains(fieldMappings, mapping =>
            mapping.GetProperty("targetField").GetString() == "name"
            && mapping.GetProperty("status").GetString() == "mapped");

        var previewRows = root.GetProperty("previewRows").EnumerateArray().ToArray();
        Assert.Equal(2, previewRows.Length);
        Assert.All(previewRows, row => Assert.Equal("accepted", row.GetProperty("status").GetString()));
    }

    private static StringContent CreateJsonContent<T>(T value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static HttpRequestMessage CreateAuthorizedRequest<T>(string path, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = CreateJsonContent(body)
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        return request;
    }

    private static Task<HttpResponseMessage> SendAuthorizedAsync<T>(HttpClient client, string path, T body)
        => client.SendAsync(CreateAuthorizedRequest(path, body));

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<TestHost> CreateAsync(Action<DataSourceOptions> configureProfiles)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddOptions();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                static FixedWindowRateLimiterOptions CreatePolicy() => new()
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                };

                foreach (var policyName in new[] { "writes", "fixed", "db-heavy", "strict", "source-discovery-tests" })
                {
                    options.AddPolicy(policyName, _ =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: policyName,
                            factory: _ => CreatePolicy()));
                }
            });

            builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            builder.Services.Configure<AccessImportOptions>(_ => { });
            builder.Services.Configure<DataSourceOptions>(options => configureProfiles(options));
            builder.Services.AddSingleton<IDataSourceProfileCatalog, DataSourceProfileCatalog>();
            builder.Services.AddSingleton<ISourceDataSessionFactory, SourceDataSessionFactory>();
            builder.Services.AddScoped<IDataSourceMappingPreviewService, DataSourceMappingPreviewService>();
            builder.Services.AddSingleton<ILogger<Program>>(NullLogger<Program>.Instance);

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapDataSourceMappingPreviewEndpoints();
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
