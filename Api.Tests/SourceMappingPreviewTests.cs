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
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class SourceMappingPreviewTests
{
    private const string AdminApiKey = "test-admin-key";
    private const string Secret = "SuperSecret_Qdb05!";

    [Fact]
    public async Task Preview_RejectsMissingAndWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync();

        using var missing = await host.Client.PostAsJsonAsync("/api/data-sources/retail/mapping-preview", ValidRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var wrong = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/retail/mapping-preview")
        {
            Content = JsonContent.Create(ValidRequest())
        };
        wrong.Headers.Add("X-Admin-Key", "wrong-admin-key");
        using var forbidden = await host.Client.SendAsync(wrong);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, host.Session.ReadCount);
    }

    [Fact]
    public async Task Preview_DoesNotSilentlyGuessUnmappedRequiredFields()
    {
        await using var host = await TestHost.CreateAsync();

        var request = ValidRequest();
        request.Fields = [new SourceMappingFieldRequest { Target = "Id", Source = "Id" }];

        using var http = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/retail/mapping-preview")
        {
            Content = JsonContent.Create(request)
        };
        http.Headers.Add("X-Admin-Key", AdminApiKey);
        using var response = await host.Client.SendAsync(http);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, body, StringComparison.Ordinal);

        var payload = Deserialize(body);
        var naziv = Assert.Single(payload.Fields, field => field.Target == "Naziv");
        Assert.Equal("rejected", naziv.Status);
        Assert.Equal("target_required_unmapped", naziv.Reason);
        Assert.Null(naziv.ResolvedSource);
        Assert.DoesNotContain("Select", payload.Preview.SelectMany(row => row.Keys), StringComparer.OrdinalIgnoreCase);
        Assert.All(payload.Preview, row => Assert.False(row.ContainsKey("Naziv")));
    }

    [Fact]
    public async Task Preview_ProjectsExplicitMapping_AndBoundsRows()
    {
        await using var host = await TestHost.CreateAsync();

        var request = ValidRequest();
        request.MaxRows = 2;

        using var http = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/retail/mapping-preview")
        {
            Content = JsonContent.Create(request)
        };
        http.Headers.Add("X-Admin-Key", AdminApiKey);
        using var response = await host.Client.SendAsync(http);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var payload = Deserialize(body);
        Assert.Equal("ok", payload.ExternalKey.Status);
        Assert.Equal("Id", payload.ExternalKey.Column);
        Assert.Equal(2, payload.PreviewRowCount);
        Assert.Equal(1, payload.RejectedRowCount);
        Assert.StartsWith("sha256:", payload.SchemaFingerprint, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", payload.Identity, StringComparison.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(body);
        var preview = doc.RootElement.GetProperty("preview");
        Assert.Equal(2, preview.GetArrayLength());
        Assert.True(TryGetProperty(preview[0], "Id", out var id));
        Assert.Equal(1, id.GetInt32());
        Assert.True(TryGetProperty(preview[0], "Naziv", out var naziv));
        Assert.Equal("čizma", naziv.GetString());
    }

    [Fact]
    public async Task Preview_RejectsMissingSourceColumn()
    {
        await using var host = await TestHost.CreateAsync();

        var request = ValidRequest();
        request.Fields =
        [
            new SourceMappingFieldRequest { Target = "Id", Source = "Id" },
            new SourceMappingFieldRequest { Target = "Naziv", Source = "DoesNotExist" }
        ];

        using var http = new HttpRequestMessage(HttpMethod.Post, "/api/data-sources/retail/mapping-preview")
        {
            Content = JsonContent.Create(request)
        };
        http.Headers.Add("X-Admin-Key", AdminApiKey);
        using var response = await host.Client.SendAsync(http);
        response.EnsureSuccessStatusCode();

        var payload = Deserialize(await response.Content.ReadAsStringAsync());
        var naziv = Assert.Single(payload.Fields, field => field.Target == "Naziv");
        Assert.Equal("source_column_missing", naziv.Reason);
        Assert.Equal("rejected", naziv.Status);
    }

    [Fact]
    public void Fingerprint_IsStableAndChangesWhenColumnsChange()
    {
        var first = SourceSchemaFingerprint.Compute("sqlserver", "dbo.Order", ["Id", "Select"]);
        var same = SourceSchemaFingerprint.Compute("sqlserver", "dbo.Order", ["Select", "Id"]);
        var changed = SourceSchemaFingerprint.Compute("sqlserver", "dbo.Order", ["Id", "Select", "Price"]);

        Assert.Equal(first, same);
        Assert.NotEqual(first, changed);
        Assert.StartsWith("sha256:", first, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFields_DoesNotAutoMapCanonicalAliases()
    {
        Assert.True(CanonicalSourceEntities.TryGet("artikli", out var entity));
        var results = SourceMappingPreviewService.ValidateFields(
            entity,
            [new SourceMappingFieldRequest { Target = "Id", Source = "Id" }],
            ["Id", "Naziv", "Select"]);

        Assert.Contains(results, field => field.Target == "Naziv" && field.Reason == "target_required_unmapped");
        Assert.DoesNotContain(results, field => field.Target == "Naziv" && field.Status == "ok");
    }

    private static SourceMappingPreviewRequest ValidRequest() => new()
    {
        Table = "dbo.Order",
        Entity = "artikli",
        ExternalKeyColumn = "Id",
        CursorMode = "id",
        CursorIdColumn = "Id",
        MaxRows = 25,
        Fields =
        [
            new SourceMappingFieldRequest { Target = "Id", Source = "Id" },
            new SourceMappingFieldRequest { Target = "Naziv", Source = "Select" }
        ]
    };

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static SourceMappingPreviewDto Deserialize(string body)
        => JsonSerializer.Deserialize<SourceMappingPreviewDto>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, FakeSession session)
        {
            App = app;
            Client = app.GetTestClient();
            Session = session;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public FakeSession Session { get; }

        public static async Task<TestHost> CreateAsync()
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
            builder.Configuration["DataSources:Sources:retail:ConnectionString"] =
                $"Server=tcp:127.0.0.1,1;User Id=sa;Password={Secret};Initial Catalog=Retail;Encrypt=False;";

            builder.Services.Configure<DataSourceConnectorOptions>(
                builder.Configuration.GetSection(DataSourceConnectorOptions.SectionName));
            var session = new FakeSession();
            builder.Services.AddSingleton<ISourceSessionFactory>(new FakeFactory(session));
            builder.Services.AddSingleton<NamedSourceDiscoveryService>();
            builder.Services.AddSingleton<SourceMappingPreviewService>();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("strict", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "mapping-preview",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
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

    private sealed class FakeFactory : ISourceSessionFactory
    {
        private readonly FakeSession _session;
        public FakeFactory(FakeSession session) => _session = session;
        public ISourceDataSession Create(string provider, string connectionString) => _session;
    }

    private sealed class FakeSession : ISourceDataSession
    {
        public string Provider => "sqlserver";
        public string Mode => "read-only";
        public string SourceIdentity => "Data Source=fake;Initial Catalog=Retail";
        public SourceCapabilities Capabilities { get; } = new(PredicatePushdown: true);
        public int ReadCount { get; private set; }

        public Task TestConnectionAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["dbo.Order"]);

        public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["Id", "Select", "User", "Price", "UpdatedAt"]);

        public Task<IReadOnlyList<SourceColumnDefinition>> GetColumnDefinitionsAsync(string table, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SourceColumnDefinition>>([
                new("Id", null, null, 0),
                new("Select", null, null, 1),
                new("User", null, null, 2),
                new("Price", null, null, 3),
                new("UpdatedAt", null, null, 4)
            ]);

        public Task<SourceRowCountResult> TryGetRowCountAsync(string table, CancellationToken ct = default)
            => Task.FromResult(SourceRowCountResult.Exact(3));

        public IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
            => ReadRowsAsync(table, query: null, ct);

        public async IAsyncEnumerable<SourceDataRow> ReadRowsAsync(
            string table,
            SourceReadQuery? query,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ReadCount++;
            var schema = new SourceDataSchema(["Id", "Select", "User", "Price", "UpdatedAt"]);
            var rows = new object?[][]
            {
                [1, "čizma", "чизма", 12.3456m, new DateTime(2026, 8, 1, 10, 0, 0)],
                [null, "ghost", "skip", 1m, new DateTime(2026, 8, 1, 11, 0, 0)],
                [2, "boot", "boot", 9m, new DateTime(2026, 8, 1, 12, 0, 0)],
                [3, "extra", "extra", 3m, new DateTime(2026, 8, 1, 13, 0, 0)]
            };

            var max = query?.MaxRows is > 0 ? query.MaxRows.Value : rows.Length;
            var yielded = 0;
            foreach (var values in rows)
            {
                if (yielded >= max)
                    yield break;
                yielded++;
                await Task.CompletedTask;
                yield return new SourceDataRow(schema, values);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
