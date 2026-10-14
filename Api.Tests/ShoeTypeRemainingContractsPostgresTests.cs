using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Application.Config;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

public sealed class ShoeTypeRemainingContractsPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private const string RequestPath = "/api/analytics/shoe-type-sales-stats?fromDate=2026-07-01&toDate=2026-07-07&dataScope=all";
    private readonly PostgresContainerFixture _fixture;

    public ShoeTypeRemainingContractsPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresRequiredFact(DisplayName = "RQ646 ShoeType detail preserves recommendation trust and canonical unknown identity")]
    public async Task ShoeTypeDetail_PreservesDecisionTrustAndUnknownIdentity()
    {
        await using var session = await CreateSessionAsync(nameof(ShoeTypeDetail_PreservesDecisionTrustAndUnknownIdentity));
        var rows = (await GetRootAsync(session.Client, RequestPath)).GetProperty("shoeTypes").EnumerateArray().ToArray();
        var known = rows.First(row => !row.GetProperty("isUnknown").GetBoolean());
        var knownId = known.GetProperty("tipObuceId").GetInt32();

        var detail = await GetRootAsync(
            session.Client,
            $"/api/analitika/shoe-type-sales-stats/{knownId}?fromDate=2026-07-01&toDate=2026-07-07&dataScope=all");
        var recommendation = detail.GetProperty("recommendation");
        Assert.False(string.IsNullOrWhiteSpace(recommendation.GetProperty("status").GetString()));
        Assert.True(recommendation.TryGetProperty("recommendationAllowed", out _));
        Assert.True(recommendation.TryGetProperty("reasonCodes", out _));

        var provenance = detail.GetProperty("provenance");
        Assert.Equal("all", provenance.GetProperty("dataScope").GetString());
        Assert.True(provenance.TryGetProperty("effectiveFromUtc", out _));
        Assert.True(provenance.TryGetProperty("generatedAtUtc", out _));
        Assert.Contains(
            detail.GetProperty("metadata").EnumerateArray(),
            field => field.GetProperty("label").GetString() == "Opseg podataka");

        var unknown = Assert.Single(rows, row => row.GetProperty("isUnknown").GetBoolean());
        Assert.Equal("Nepoznato", unknown.GetProperty("tipObuceNaziv").GetString());
        var unknownDetail = await GetRootAsync(
            session.Client,
            "/api/analitika/shoe-type-sales-stats/unknown-nepoznato?fromDate=2026-07-01&toDate=2026-07-07&dataScope=all");
        Assert.Equal("Nepoznato", unknownDetail.GetProperty("title").GetString());
        Assert.False(unknownDetail.GetProperty("recommendation").GetProperty("recommendationAllowed").GetBoolean());

        using var collidingUnknown = await session.Client.GetAsync(
            "/api/analitika/shoe-type-sales-stats/unknown-drugi-tip?fromDate=2026-07-01&toDate=2026-07-07&dataScope=all");
        Assert.Equal(HttpStatusCode.NotFound, collidingUnknown.StatusCode);
    }

    [PostgresRequiredFact(DisplayName = "RQ646 ShoeType response matches the reviewed July fixture golden projection")]
    public async Task ShoeTypeSalesStats_MatchesGoldenSnapshot()
    {
        await using var session = await CreateSessionAsync(nameof(ShoeTypeSalesStats_MatchesGoldenSnapshot));
        var root = await GetRootAsync(session.Client, RequestPath);
        var rows = root.GetProperty("shoeTypes").EnumerateArray()
            .Select(row => new ShoeTypeGoldenRow(
                row.GetProperty("tipObuceNaziv").GetString()!,
                row.GetProperty("ukupanPromet").GetDecimal(),
                row.GetProperty("ukupnaKolicina").GetInt32(),
                Math.Round(row.GetProperty("sharePct").GetDouble(), 2),
                row.GetProperty("isUnknown").GetBoolean()))
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                new ShoeTypeGoldenRow("Cipele", 260m, 2, 48.15d, false),
                new ShoeTypeGoldenRow("Nepoznato", 80m, 1, 14.81d, true),
                new ShoeTypeGoldenRow("Patike", 200m, 2, 37.04d, false)
            },
            rows);
        Assert.Equal(540m, root.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(5, root.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(14.81d, Math.Round(root.GetProperty("dataQuality").GetProperty("unknownTypeRevenueSharePct").GetDouble(), 2));
    }

    [PostgresRequiredFact(DisplayName = "RQ646 ShoeType response is deterministic after recomputation")]
    public async Task ShoeTypeSalesStats_ProducesDeterministicJson()
    {
        await using var session = await CreateSessionAsync(nameof(ShoeTypeSalesStats_ProducesDeterministicJson));
        var first = await GetBodyAsync(session.Client, RequestPath);
        session.Cache.Clear();
        var second = await GetBodyAsync(session.Client, RequestPath);

        Assert.Equal(CanonicalizeStableJson(first), CanonicalizeStableJson(second));
    }

    private async Task<ShoeTypeEndpointSession> CreateSessionAsync(string testName)
    {
        Assert.True(_fixture.IsAvailable, $"{testName} requires its disposable PostgreSQL Testcontainer.");
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"rq646_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await SupplierSalesStatsEndpointPostgresParityTests.CreateFixtureAsync(connectionString!);

        var registry = new OperationsAnalyticsIntegrityRegistry();
        SupplierSalesStatsEndpointPostgresParityTests.SetBoundSupplierEvidence(
            registry,
            OperationsAnalyticsIntegrityStates.Verified,
            $"rq646-{testName}-evidence",
            blocksDecisionSignals: false,
            trigger: "rq646_fixture_verified",
            summary: "Isolated RQ646 PostgreSQL fixture evidence is verified.");
        var cache = new TestAnalyticsCacheService();
        var factory = new ShoeTypeEndpointFactory(connectionString!, cache, registry);
        return new ShoeTypeEndpointSession(factory, factory.CreateClient(), cache);
    }

    private static async Task<JsonElement> GetRootAsync(HttpClient client, string path)
    {
        var body = await GetBodyAsync(client, path);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<string> GetBodyAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    private static string CanonicalizeStableJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteStableValue(document.RootElement, writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteStableValue(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject()
                             .Where(property => !IsVolatileTimestamp(property.Name))
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteStableValue(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteStableValue(item, writer);
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }

    private static bool IsVolatileTimestamp(string propertyName) =>
        propertyName.Equals("generatedAt", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("generatedAtUtc", StringComparison.OrdinalIgnoreCase);

    private sealed record ShoeTypeGoldenRow(string Name, decimal Revenue, int Units, double SharePct, bool IsUnknown);

    private sealed class ShoeTypeEndpointSession(
        ShoeTypeEndpointFactory factory,
        HttpClient client,
        TestAnalyticsCacheService cache) : IAsyncDisposable
    {
        public ShoeTypeEndpointFactory Factory { get; } = factory;
        public HttpClient Client { get; } = client;
        public TestAnalyticsCacheService Cache { get; } = cache;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }

    private sealed class ShoeTypeEndpointFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _connectionString;
        private readonly TestAnalyticsCacheService _cache;
        private readonly OperationsAnalyticsIntegrityRegistry _registry;

        public ShoeTypeEndpointFactory(
            string connectionString,
            TestAnalyticsCacheService cache,
            OperationsAnalyticsIntegrityRegistry registry)
        {
            _connectionString = connectionString;
            _cache = cache;
            _registry = registry;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:AutoMigrate"] = "false",
                    ["StartupReadiness:GateApiTraffic"] = "false",
                    ["PROCESS_TYPE"] = "web",
                    ["Workers:Enabled"] = "false",
                    ["Caching:Provider"] = "disabled",
                    ["Analytics:UseSnapshotCost"] = "true",
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["ConnectionStrings:AnalyticsConnection"] = _connectionString,
                    ["ConnectionStrings:OpenProductTrainingConnection"] = _connectionString
                }));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();
                services.AddDbContextFactory<TrendplusDbContext>(options => options.UseNpgsql(_connectionString).ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(_connectionString).ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddScoped<ITrendplusDbContext>(provider => provider.GetRequiredService<TrendplusDbContext>());

                services.RemoveAll<IAnalyticsCacheService>();
                services.AddSingleton<IAnalyticsCacheService>(_cache);
                services.RemoveAll<OperationsAnalyticsIntegrityRegistry>();
                services.AddSingleton(_registry);
                services.Configure<AnalyticsSnapshotOptions>(options => options.UseSnapshotCost = true);
                services.Configure<PerformanceLoggingOptions>(options => options.CaptureHttpRequests = false);
            });
        }
    }

    private sealed class TestAnalyticsCacheService : IAnalyticsCacheService
    {
        private readonly ConcurrentDictionary<string, object> _entries = new(StringComparer.Ordinal);

        public bool IsRedisAvailable => false;
        public bool IsRedisEnabled => false;

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        {
            var exists = _entries.TryGetValue(key, out var value) && value is T;
            return Task.FromResult(exists ? (T?)value : null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        {
            _entries[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _entries.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        {
            foreach (var key in _entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
                _entries.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        {
            var cached = await GetAsync<T>(key, ct);
            if (cached is not null)
                return cached;

            var value = await factory();
            await SetAsync(key, value, expiration, ct);
            return value;
        }

        public void SetRedisEnabled(bool enabled) { }
        public CacheFootprintSnapshot GetFootprintSnapshot() => new("rq646-test", false, false, _entries.Count);
        public void Clear() => _entries.Clear();
    }
}
