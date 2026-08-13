using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.RateLimiting;
using Api.Services.DataSources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DataSourceDiscoveryAuthorizationTests
{
    private const string AdminApiKey = "test-admin-key";
    private const string Secret = "SuperSecret_Qdb04!";

    [Fact]
    public async Task List_RejectsMissingAndWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync();

        using var missing = await host.Client.GetAsync("/api/data-sources");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var wrongRequest = new HttpRequestMessage(HttpMethod.Get, "/api/data-sources");
        wrongRequest.Headers.Add("X-Admin-Key", "wrong-admin-key");
        using var wrong = await host.Client.SendAsync(wrongRequest);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(0, host.Session.TestConnectionCount);
    }

    [Fact]
    public async Task List_DoesNotReturnConnectionStringOrPassword()
    {
        await using var host = await TestHost.CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/data-sources");
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);

        var payload = JsonSerializer.Deserialize<List<NamedSourceSummaryDto>>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(payload);
        var retail = Assert.Single(payload!, item => item.Name == "retail");
        Assert.Equal("sqlserver", retail.Provider);
        Assert.True(retail.Configured);
        Assert.Contains("127.0.0.1", retail.Identity, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retail", retail.Identity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sa", retail.Identity, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnection_ReturnsSafeFailureCategory_WithoutLeakingSecret()
    {
        await using var host = await TestHost.CreateAsync(useRealFactory: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/retail/test-connection");
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, body, StringComparison.Ordinal);

        var payload = JsonSerializer.Deserialize<SourceConnectionTestDto>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotEqual("ok", payload.Category);
        Assert.Equal("Connection test failed.", payload.Message);
    }

    [Fact]
    public async Task TestConnection_UnknownSource_ReturnsNotFound_WithoutOpeningSession()
    {
        await using var host = await TestHost.CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/missing/test-connection");
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, host.Session.TestConnectionCount);
    }

    [Fact]
    public async Task TablesAndColumns_ReturnSourceNames_WithoutSecrets()
    {
        await using var host = await TestHost.CreateAsync();

        using var tablesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/data-sources/retail/tables");
        tablesRequest.Headers.Add("X-Admin-Key", AdminApiKey);
        using var tablesResponse = await host.Client.SendAsync(tablesRequest);
        tablesResponse.EnsureSuccessStatusCode();
        var tablesBody = await tablesResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, tablesBody, StringComparison.Ordinal);
        Assert.Contains("dbo.Order", tablesBody, StringComparison.Ordinal);

        using var columnsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/data-sources/retail/columns?table=dbo.Order");
        columnsRequest.Headers.Add("X-Admin-Key", AdminApiKey);
        using var columnsResponse = await host.Client.SendAsync(columnsRequest);
        columnsResponse.EnsureSuccessStatusCode();
        var columnsBody = await columnsResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, columnsBody, StringComparison.Ordinal);
        Assert.Contains("Select", columnsBody, StringComparison.Ordinal);
        Assert.Equal(1, host.Session.GetTablesCount);
        Assert.Equal(1, host.Session.GetColumnsCount);
    }

    [Fact]
    public async Task TestConnection_RateLimit_Returns429()
    {
        await using var host = await TestHost.CreateAsync();

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/retail/test-connection");
        firstRequest.Headers.Add("X-Admin-Key", AdminApiKey);
        using var first = await host.Client.SendAsync(firstRequest);
        first.EnsureSuccessStatusCode();

        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/retail/test-connection");
        secondRequest.Headers.Add("X-Admin-Key", AdminApiKey);
        using var second = await host.Client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(1, host.Session.TestConnectionCount);
    }

    [Fact]
    public void Summary_OmitsConnectionStringEvenWhenConfigured()
    {
        var summary = NamedSourceDiscoveryService.ToSummary(
            "retail",
            new DataSourceProfileOptions
            {
                Provider = "sqlserver",
                DisplayName = "Retail POS",
                ConnectionString = $"Server=tcp:example,1433;User Id=sa;Password={Secret};Initial Catalog=Retail;"
            });

        var json = JsonSerializer.Serialize(summary);
        Assert.DoesNotContain(Secret, json, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionString", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(summary.Configured);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, FakeSourceSession session)
        {
            App = app;
            Client = app.GetTestClient();
            Session = session;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public FakeSourceSession Session { get; }

        public static async Task<TestHost> CreateAsync(bool useRealFactory = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            builder.Configuration["DataSources:Sources:retail:Provider"] = "sqlserver";
            builder.Configuration["DataSources:Sources:retail:DisplayName"] = "Retail POS";
            builder.Configuration["DataSources:Sources:retail:ConnectionString"] =
                $"Server=tcp:127.0.0.1,1;User Id=sa;Password={Secret};Initial Catalog=Retail;Encrypt=False;Connection Timeout=1;";

            builder.Services.Configure<DataSourceConnectorOptions>(
                builder.Configuration.GetSection(DataSourceConnectorOptions.SectionName));
            var session = new FakeSourceSession();
            if (useRealFactory)
            {
                builder.Services.AddSingleton<ISourceSessionFactory, SourceSessionFactory>();
            }
            else
            {
                builder.Services.AddSingleton<ISourceSessionFactory>(new FakeSourceSessionFactory(session));
            }

            builder.Services.AddSingleton<NamedSourceDiscoveryService>();
            builder.Services.AddSingleton<SourceMappingPreviewService>();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("strict", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "source-test",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 1,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapDataSourceDiscoveryEndpoints();
            await app.StartAsync();
            return new TestHost(app, session);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class FakeSourceSessionFactory : ISourceSessionFactory
    {
        private readonly FakeSourceSession _session;

        public FakeSourceSessionFactory(FakeSourceSession session) => _session = session;

        public ISourceDataSession Create(string provider, string connectionString)
        {
            _session.LastConnectionString = connectionString;
            return _session;
        }
    }

    private sealed class FakeSourceSession : ISourceDataSession
    {
        public string Provider => "sqlserver";
        public string Mode => "read-only";
        public string SourceIdentity => "Data Source=fake;Initial Catalog=Retail";
        public SourceCapabilities Capabilities { get; } = new(PredicatePushdown: true);
        public string? LastConnectionString { get; set; }
        public int TestConnectionCount { get; private set; }
        public int GetTablesCount { get; private set; }
        public int GetColumnsCount { get; private set; }

        public Task TestConnectionAsync(CancellationToken ct = default)
        {
            TestConnectionCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
        {
            GetTablesCount++;
            return Task.FromResult<IReadOnlyList<string>>(["dbo.Order"]);
        }

        public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
        {
            GetColumnsCount++;
            return Task.FromResult<IReadOnlyList<string>>(["Id", "Select", "User"]);
        }

        public Task<SourceRowCountResult> TryGetRowCountAsync(string table, CancellationToken ct = default)
            => Task.FromResult(SourceRowCountResult.Exact(0));

        public IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
            => ReadRowsAsync(table, query: null, ct);

        public async IAsyncEnumerable<SourceDataRow> ReadRowsAsync(
            string table,
            SourceReadQuery? query,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
