using System.Net;
using System.Text.Json;
using Api.Config;
using Api.Services.DataSources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DataSourceDiscoveryEndpointsTests : IClassFixture<SqlServerContainerFixture>
{
    private const string AdminApiKey = "test-admin-key";
    private readonly SqlServerContainerFixture _fixture;

    public DataSourceDiscoveryEndpointsTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListProfiles_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(configureSources: _ => { });

        using var response = await host.Client.GetAsync("/api/data-sources");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListProfiles_HidesSecrets()
    {
        const string password = "Trendplus_Strong_123!";
        await using var host = await TestHost.CreateAsync(configureSources: sources =>
        {
            sources.Sources["sql-prod"] = new DataSourceProfileOptions
            {
                Provider = "sqlserver",
                ConnectionString = $"Server=tcp:trendplus.example,1433;Database=Retail;User Id=readonly;Password={password};TrustServerCertificate=true;"
            };
            sources.Sources["oracle-proof"] = new DataSourceProfileOptions
            {
                Provider = "oracle",
                ConnectionString = "Server=hidden;Database=Hidden;Password=super-secret;"
            };
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/data-sources");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain(password, body, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("disabled-profile", body, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(body);
        var profiles = json.RootElement.EnumerateArray().ToArray();
        var profile = Assert.Single(profiles, item => item.GetProperty("name").GetString() == "sql-prod");
        Assert.Equal("sql-prod", profile.GetProperty("name").GetString());
        Assert.Equal("sqlserver", profile.GetProperty("provider").GetString());
        Assert.True(profile.GetProperty("configured").GetBoolean());
        Assert.DoesNotContain("readonly", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("oracle-proof", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnection_ReturnsSafeUnsupportedProviderCategory()
    {
        await using var host = await TestHost.CreateAsync(configureSources: sources =>
        {
            sources.Sources["oracle-proof"] = new DataSourceProfileOptions
            {
                Provider = "oracle",
                ConnectionString = "Server=hidden;Database=Hidden;Password=super-secret;"
            };
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/oracle-proof/test-connection");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not supported", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnection_IsRateLimitedByDedicatedPolicy()
    {
        await using var host = await TestHost.CreateAsync(
            configureSources: sources =>
            {
                sources.Sources["oracle-proof"] = new DataSourceProfileOptions
                {
                    Provider = "oracle",
                    ConnectionString = "Server=hidden;Database=Hidden;Password=super-secret;"
                };
            },
            sourceDiscoveryTestPermitLimit: 1);

        var first = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/oracle-proof/test-connection");
        first.Headers.Add("X-Admin-Key", AdminApiKey);
        using var firstResponse = await host.Client.SendAsync(first);
        Assert.Equal(HttpStatusCode.BadRequest, firstResponse.StatusCode);

        var second = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/oracle-proof/test-connection");
        second.Headers.Add("X-Admin-Key", AdminApiKey);
        using var secondResponse = await host.Client.SendAsync(second);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SqlServerDiscovery_ListsSchemasTablesColumnsAndSafeConnectionTest()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await _fixture.CreateSeededConnectionStringAsync();
        await using var host = await TestHost.CreateAsync(configureSources: sources =>
        {
            sources.Sources["pilot-sql"] = new DataSourceProfileOptions
            {
                Provider = "sqlserver",
                ConnectionString = connectionString,
            };
        });

        using var testResponse = await SendAuthorizedAsync(host.Client, HttpMethod.Post, "/api/data-sources/pilot-sql/test-connection");
        var testPayload = await testResponse.Content.ReadAsStringAsync();
        testResponse.EnsureSuccessStatusCode();
        using var testJson = JsonDocument.Parse(testPayload);
        Assert.True(testJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("ok", testJson.RootElement.GetProperty("category").GetString());
        Assert.DoesNotContain("Password=", testPayload, StringComparison.OrdinalIgnoreCase);

        using var tablesResponse = await SendAuthorizedAsync(host.Client, HttpMethod.Get, "/api/data-sources/pilot-sql/tables");
        var tablesPayload = await tablesResponse.Content.ReadAsStringAsync();
        tablesResponse.EnsureSuccessStatusCode();
        using var tablesJson = JsonDocument.Parse(tablesPayload);
        var schemaNames = tablesJson.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("dbo", schemaNames);
        Assert.Contains("sales", schemaNames);
        var tables = tablesJson.RootElement.GetProperty("tables").EnumerateArray().ToArray();
        Assert.Contains(tables, table => table.GetString() == "[sales].[Order]");

        using var columnsResponse = await SendAuthorizedAsync(
            host.Client,
            HttpMethod.Get,
            "/api/data-sources/pilot-sql/columns?table=%5Bsales%5D.%5BOrder%5D");
        var columnsPayload = await columnsResponse.Content.ReadAsStringAsync();
        columnsResponse.EnsureSuccessStatusCode();
        using var columnsJson = JsonDocument.Parse(columnsPayload);
        var columns = columnsJson.RootElement.GetProperty("columns").EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Equal(new[] { "ID", "Updated At", "Naziv", "Price", "Optional Note" }, columns);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        return request;
    }

    private static Task<HttpResponseMessage> SendAuthorizedAsync(HttpClient client, HttpMethod method, string path)
        => client.SendAsync(CreateAuthorizedRequest(method, path));

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<TestHost> CreateAsync(
            Action<DataSourceConnectorOptions> configureSources,
            int sourceDiscoveryTestPermitLimit = 8)
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

                static FixedWindowRateLimiterOptions CreatePolicy(int limit) => new()
                {
                    PermitLimit = limit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                };

                foreach (var policyName in new[] { "writes", "fixed", "db-heavy" })
                {
                    options.AddPolicy(policyName, _ =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: policyName,
                            factory: _ => CreatePolicy(100)));
                }

                options.AddPolicy("strict", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "strict",
                        factory: _ => CreatePolicy(sourceDiscoveryTestPermitLimit)));
            });

            builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            builder.Services.Configure<DataSourceConnectorOptions>(options => configureSources(options));
            builder.Services.AddSingleton<ISourceSessionFactory, SourceSessionFactory>();
            builder.Services.AddSingleton<NamedSourceDiscoveryService>();
            builder.Services.AddSingleton<ILogger<Program>>(NullLogger<Program>.Instance);

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapDataSourceDiscoveryEndpoints();
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
