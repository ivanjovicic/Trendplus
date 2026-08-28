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
        await using var host = await TestHost.CreateAsync(configureProfiles: _ => { });

        using var response = await host.Client.GetAsync("/api/data-sources");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListProfiles_HidesSecretsAndDisabledProfiles()
    {
        const string password = "Trendplus_Strong_123!";
        await using var host = await TestHost.CreateAsync(configureProfiles: profiles =>
        {
            profiles.ConnectionTestTimeoutSeconds = 5;
            profiles.DiscoveryTimeoutSeconds = 10;
            profiles.Profiles.Add(new NamedDataSourceProfileOptions
            {
                Name = "sql-prod",
                Provider = "sqlserver",
                ConnectionString = $"Server=tcp:trendplus.example,1433;Database=Retail;User Id=readonly;Password={password};TrustServerCertificate=true;",
                DefaultSchema = "sales",
                Description = "Primary retail SQL source"
            });
            profiles.Profiles.Add(new NamedDataSourceProfileOptions
            {
                Name = "disabled-profile",
                Provider = "sqlserver",
                ConnectionString = "Server=hidden;Database=Hidden;Password=super-secret;",
                Enabled = false
            });
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
        var profiles = json.RootElement.GetProperty("profiles");
        var profile = Assert.Single(profiles.EnumerateArray());
        Assert.Equal("sql-prod", profile.GetProperty("name").GetString());
        Assert.Equal("sqlserver", profile.GetProperty("provider").GetString());
        Assert.Equal("sqlclient", profile.GetProperty("mode").GetString());
        Assert.Equal("sales", profile.GetProperty("defaultSchema").GetString());
    }

    [Fact]
    public async Task TestConnection_ReturnsSafeUnsupportedProviderCategory()
    {
        await using var host = await TestHost.CreateAsync(configureProfiles: profiles =>
        {
            profiles.Profiles.Add(new NamedDataSourceProfileOptions
            {
                Name = "oracle-proof",
                Provider = "oracle"
            });
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/oracle-proof/test");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(payload);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("unsupported_provider", json.RootElement.GetProperty("category").GetString());
        Assert.DoesNotContain("connection string", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnection_IsRateLimitedByDedicatedPolicy()
    {
        await using var host = await TestHost.CreateAsync(
            configureProfiles: profiles =>
            {
                profiles.Profiles.Add(new NamedDataSourceProfileOptions
                {
                    Name = "oracle-proof",
                    Provider = "oracle"
                });
            },
            sourceDiscoveryTestPermitLimit: 1);

        var first = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/oracle-proof/test");
        first.Headers.Add("X-Admin-Key", AdminApiKey);
        using var firstResponse = await host.Client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var second = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/oracle-proof/test");
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
        await using var host = await TestHost.CreateAsync(configureProfiles: profiles =>
        {
            profiles.ConnectionTestTimeoutSeconds = 5;
            profiles.DiscoveryTimeoutSeconds = 10;
            profiles.Profiles.Add(new NamedDataSourceProfileOptions
            {
                Name = "pilot-sql",
                Provider = "sqlserver",
                ConnectionString = connectionString,
                DefaultSchema = "sales"
            });
        });

        using var testResponse = await SendAuthorizedAsync(host.Client, HttpMethod.Post, "/api/data-sources/pilot-sql/test");
        var testPayload = await testResponse.Content.ReadAsStringAsync();
        testResponse.EnsureSuccessStatusCode();
        using var testJson = JsonDocument.Parse(testPayload);
        Assert.True(testJson.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("ok", testJson.RootElement.GetProperty("category").GetString());
        Assert.DoesNotContain("Password=", testPayload, StringComparison.OrdinalIgnoreCase);

        using var schemasResponse = await SendAuthorizedAsync(host.Client, HttpMethod.Get, "/api/data-sources/pilot-sql/schemas");
        var schemasPayload = await schemasResponse.Content.ReadAsStringAsync();
        schemasResponse.EnsureSuccessStatusCode();
        using var schemasJson = JsonDocument.Parse(schemasPayload);
        var schemaNames = schemasJson.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("dbo", schemaNames);
        Assert.Contains("sales", schemaNames);

        using var tablesResponse = await SendAuthorizedAsync(host.Client, HttpMethod.Get, "/api/data-sources/pilot-sql/tables?schema=sales");
        var tablesPayload = await tablesResponse.Content.ReadAsStringAsync();
        tablesResponse.EnsureSuccessStatusCode();
        using var tablesJson = JsonDocument.Parse(tablesPayload);
        var table = Assert.Single(tablesJson.RootElement.EnumerateArray());
        Assert.Equal("[sales].[Order]", table.GetProperty("identifier").GetString());
        Assert.Equal("sales", table.GetProperty("schema").GetString());
        Assert.Equal("Order", table.GetProperty("name").GetString());

        using var columnsResponse = await SendAuthorizedAsync(
            host.Client,
            HttpMethod.Get,
            "/api/data-sources/pilot-sql/columns?table=%5Bsales%5D.%5BOrder%5D");
        var columnsPayload = await columnsResponse.Content.ReadAsStringAsync();
        columnsResponse.EnsureSuccessStatusCode();
        using var columnsJson = JsonDocument.Parse(columnsPayload);
        var columns = columnsJson.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
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
            Action<DataSourceOptions> configureProfiles,
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

                foreach (var policyName in new[] { "writes", "fixed", "db-heavy", "strict" })
                {
                    options.AddPolicy(policyName, _ =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: policyName,
                            factory: _ => CreatePolicy(100)));
                }

                options.AddPolicy("source-discovery-tests", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "source-discovery-tests",
                        factory: _ => CreatePolicy(sourceDiscoveryTestPermitLimit)));
            });

            builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            builder.Services.Configure<AccessImportOptions>(_ => { });
            builder.Services.Configure<DataSourceOptions>(options => configureProfiles(options));
            builder.Services.AddSingleton<IDataSourceProfileCatalog, DataSourceProfileCatalog>();
            builder.Services.AddSingleton<ISourceDataSessionFactory, SourceDataSessionFactory>();
            builder.Services.AddScoped<IDataSourceDiscoveryService, DataSourceDiscoveryService>();
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
