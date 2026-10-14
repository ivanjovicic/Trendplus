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
using Npgsql;
using Xunit;

namespace Api.Tests;

public sealed class SupplierSalesRemainingContractsPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private const string RequestPath = "/api/analytics/supplier-sales-stats?fromDate=2026-07-01&toDate=2026-07-07&dataScope=all";
    private readonly PostgresContainerFixture _fixture;

    public SupplierSalesRemainingContractsPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresRequiredFact(DisplayName = "RQ645 Supplier detail preserves recommendation trust and reference cohort")]
    public async Task SupplierSalesDetail_ExposesRecommendationTrustAndReferenceCohort()
    {
        await using var session = await CreateSessionAsync(nameof(SupplierSalesDetail_ExposesRecommendationTrustAndReferenceCohort));
        var supplierId = (await GetRootAsync(session.Client, RequestPath))
            .GetProperty("suppliers")
            .EnumerateArray()
            .First(supplier => !supplier.GetProperty("isUnknown").GetBoolean())
            .GetProperty("dobavljacId")
            .GetInt32();

        var detail = await GetRootAsync(
            session.Client,
            $"/api/analitika/supplier-sales-stats/{supplierId}?fromDate=2026-07-01&toDate=2026-07-07&dataScope=all");
        var recommendation = detail.GetProperty("recommendation");
        Assert.False(string.IsNullOrWhiteSpace(recommendation.GetProperty("status").GetString()));
        Assert.True(recommendation.TryGetProperty("recommendationAllowed", out _));

        var provenance = detail.GetProperty("provenance");
        Assert.Equal("live_query/supplier_sales_stats", provenance.GetProperty("provenanceBasis").GetString());
        Assert.False(string.IsNullOrWhiteSpace(provenance.GetProperty("displayPopulation").GetString()));
        Assert.Contains("dobavljača", provenance.GetProperty("decisionReferenceCohort").GetString());
        Assert.Equal("all", provenance.GetProperty("dataScope").GetString());
        Assert.False(string.IsNullOrWhiteSpace(provenance.GetProperty("sourceLabel").GetString()));
        Assert.Contains(
            detail.GetProperty("metadata").EnumerateArray(),
            field => field.GetProperty("key").GetString() == "decisionReferenceCohort");
    }

    [PostgresRequiredFact(DisplayName = "RQ645 Supplier comparable pre/post totals reconcile to supplier rows")]
    public async Task SupplierSalesStats_ComparablePrePostTotalsMatchSupplierRows()
    {
        await using var session = await CreateSessionAsync(nameof(SupplierSalesStats_ComparablePrePostTotalsMatchSupplierRows));
        const string comparisonPath = "/api/analytics/supplier-sales-stats?fromDate=2026-07-20&toDate=2026-08-13&dataScope=all";
        var root = await GetRootAsync(session.Client, comparisonPath);
        var suppliers = root.GetProperty("suppliers").EnumerateArray().ToArray();
        var totals = root.GetProperty("totals");

        Assert.Equal(
            suppliers.Sum(supplier => supplier.GetProperty("comparablePreNivelacijePromet").GetDecimal()),
            totals.GetProperty("comparablePrePromet").GetDecimal());
        Assert.Equal(
            suppliers.Sum(supplier => supplier.GetProperty("comparablePostNivelacijePromet").GetDecimal()),
            totals.GetProperty("comparablePoslePromet").GetDecimal());
        Assert.Equal(
            suppliers.Sum(supplier => supplier.GetProperty("comparablePreNivelacijeKolicina").GetInt32()),
            totals.GetProperty("comparablePreKolicina").GetInt32());
        Assert.Equal(
            suppliers.Sum(supplier => supplier.GetProperty("comparablePostNivelacijeKolicina").GetInt32()),
            totals.GetProperty("comparablePosleKolicina").GetInt32());
        Assert.Equal(
            suppliers.Sum(supplier => supplier.GetProperty("prePostComparableArticleCount").GetInt32()),
            totals.GetProperty("prePostComparableArticleCount").GetInt32());
        Assert.True(totals.GetProperty("prePostComparableArticleCount").GetInt32() > 0,
            "The seeded price event must make the comparable cohort non-vacuous.");
        Assert.Equal(totals.GetProperty("prePromet").GetDecimal(), totals.GetProperty("observedPrePromet").GetDecimal());
        Assert.Equal(totals.GetProperty("poslePromet").GetDecimal(), totals.GetProperty("observedPoslePromet").GetDecimal());
    }

    [PostgresRequiredFact(DisplayName = "RQ645 Supplier reference cohort excludes unknown suppliers without hiding their revenue")]
    public async Task SupplierSalesStats_DeclaresRecommendationReferenceCohort()
    {
        await using var session = await CreateSessionAsync(nameof(SupplierSalesStats_DeclaresRecommendationReferenceCohort));
        var root = await GetRootAsync(session.Client, RequestPath);
        var suppliers = root.GetProperty("suppliers").EnumerateArray().ToArray();
        var knownSuppliers = suppliers.Where(supplier => !supplier.GetProperty("isUnknown").GetBoolean()).ToArray();
        var unknownSupplier = Assert.Single(suppliers, supplier => supplier.GetProperty("isUnknown").GetBoolean());

        Assert.NotEmpty(knownSuppliers);
        Assert.True(unknownSupplier.GetProperty("ukupanPromet").GetDecimal() > 0m,
            "The fixture must make unknown-supplier exclusion non-vacuous.");
        var cohort = root.GetProperty("recommendationReferenceCohort");
        Assert.Equal("known_supplier_rows", cohort.GetProperty("scope").GetString());
        Assert.Equal(knownSuppliers.Length, cohort.GetProperty("supplierCount").GetInt32());
        Assert.False(cohort.GetProperty("includesUnknown").GetBoolean());
        Assert.Equal("supplier_trust_contract_revenue_unknown_gate_v1", cohort.GetProperty("basis").GetString());
        Assert.Equal(
            root.GetProperty("dataQuality").GetProperty("unknownSupplierRevenueSharePct").GetDouble(),
            cohort.GetProperty("unknownSupplierRevenueSharePct").GetDouble());
        Assert.Equal("supplier_trust_contract_revenue", cohort.GetProperty("unknownSupplierRevenueDenominator").GetString());
    }

    [PostgresRequiredFact(DisplayName = "RQ645 Supplier missing schema returns a typed error without fallback values")]
    public async Task SupplierSalesStats_HandlesMissingSchemaGracefully()
    {
        await using var session = await CreateSessionAsync(nameof(SupplierSalesStats_HandlesMissingSchemaGracefully));
        await using (var connection = new NpgsqlConnection(session.ConnectionString))
        {
            await connection.OpenAsync();
            await using var rename = new NpgsqlCommand(
                "ALTER TABLE public.analytics_cost_snapshot_batches RENAME TO rq645_removed_cost_snapshot_batches;",
                connection);
            await rename.ExecuteNonQueryAsync();
        }

        var correlationId = $"rq645-{Guid.NewGuid():N}";
        session.Client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);
        using var response = await session.Client.GetAsync(RequestPath);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var problem = document.RootElement;

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("SUPPLIER_OVERVIEW_SCHEMA_MISSING", problem.GetProperty("errorCode").GetString());
        Assert.Equal(correlationId, problem.GetProperty("correlationId").GetString());
        Assert.False(problem.TryGetProperty("suppliers", out _));
        Assert.False(problem.TryGetProperty("totals", out _));
        Assert.DoesNotContain("42P01", body, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresRequiredFact(DisplayName = "RQ645 Supplier response matches the reviewed July fixture golden projection")]
    public async Task SupplierSalesStats_MatchesGoldenSnapshot()
    {
        await using var session = await CreateSessionAsync(nameof(SupplierSalesStats_MatchesGoldenSnapshot));
        var root = await GetRootAsync(session.Client, RequestPath);
        var rows = root.GetProperty("suppliers").EnumerateArray()
            .Select(supplier => new SupplierGoldenRow(
                supplier.GetProperty("dobavljacNaziv").GetString()!,
                supplier.GetProperty("ukupanPromet").GetDecimal(),
                supplier.GetProperty("ukupnaKolicina").GetInt32(),
                Math.Round(supplier.GetProperty("sharePct").GetDouble(), 2),
                supplier.GetProperty("isUnknown").GetBoolean()))
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                new SupplierGoldenRow("Dobavljac A", 200m, 2, 37.04d, false),
                new SupplierGoldenRow("Dobavljac C", 260m, 2, 48.15d, false),
                new SupplierGoldenRow("Nepoznato", 80m, 1, 14.81d, true)
            },
            rows);
        Assert.Equal(540m, root.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(5, root.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(14.81d, Math.Round(root.GetProperty("dataQuality").GetProperty("unknownSupplierRevenueSharePct").GetDouble(), 2));
    }

    [PostgresRequiredFact(DisplayName = "RQ645 Supplier response is deterministic after recomputation")]
    public async Task SupplierSalesStats_ProducesDeterministicJson()
    {
        await using var session = await CreateSessionAsync(nameof(SupplierSalesStats_ProducesDeterministicJson));
        var first = await GetBodyAsync(session.Client, RequestPath);
        session.Cache.Clear();
        var second = await GetBodyAsync(session.Client, RequestPath);

        Assert.Equal(CanonicalizeStableJson(first), CanonicalizeStableJson(second));
    }

    [PostgresRequiredFact(DisplayName = "RQ645 Supplier weighted margin uses known covered revenue and declares its cost basis")]
    public async Task SupplierSalesStats_UsesWeightedMarginBenchmark()
    {
        await using var session = await CreateSessionAsync(nameof(SupplierSalesStats_UsesWeightedMarginBenchmark));
        var root = await GetRootAsync(session.Client, RequestPath);
        var suppliers = root.GetProperty("suppliers").EnumerateArray().ToArray();
        var knownSuppliers = suppliers.Where(supplier => !supplier.GetProperty("isUnknown").GetBoolean()).ToArray();
        var unknownSupplier = Assert.Single(suppliers, supplier => supplier.GetProperty("isUnknown").GetBoolean());
        Assert.True(unknownSupplier.GetProperty("ukupanPromet").GetDecimal() > 0m);

        var coveredRevenue = knownSuppliers.Sum(supplier => supplier.GetProperty("revenueWithCost").GetDecimal());
        var marginContribution = knownSuppliers.Sum(supplier => supplier.GetProperty("marginContribution").GetDecimal());
        Assert.True(coveredRevenue > 0m);
        var totals = root.GetProperty("totals");
        Assert.Equal(coveredRevenue, totals.GetProperty("weightedMarginRevenue").GetDecimal());
        Assert.Equal(marginContribution, totals.GetProperty("weightedMarginContribution").GetDecimal());
        Assert.Equal("known_supplier_covered_revenue_weighted", totals.GetProperty("marginBenchmarkBasis").GetString());
        Assert.Equal(Math.Round((double)(marginContribution / coveredRevenue * 100m), 2), totals.GetProperty("prosecnaMarza").GetDouble());
        Assert.Equal(
            "historical_sale_line_then_snapshot_then_product_fallback_then_unavailable",
            root.GetProperty("dataQuality").GetProperty("costSourceBasis").GetString());
    }

    private async Task<SupplierEndpointSession> CreateSessionAsync(string testName)
    {
        Assert.True(_fixture.IsAvailable, $"{testName} requires its disposable PostgreSQL Testcontainer.");
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"rq645_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await SupplierSalesStatsEndpointPostgresParityTests.CreateFixtureAsync(connectionString!);

        var registry = new OperationsAnalyticsIntegrityRegistry();
        SupplierSalesStatsEndpointPostgresParityTests.SetBoundSupplierEvidence(
            registry,
            OperationsAnalyticsIntegrityStates.Verified,
            $"rq645-{testName}-evidence",
            blocksDecisionSignals: false,
            trigger: "rq645_fixture_verified",
            summary: "Isolated RQ645 PostgreSQL fixture evidence is verified.");
        var cache = new TestAnalyticsCacheService();
        var factory = new SupplierEndpointFactory(connectionString!, cache, registry);
        return new SupplierEndpointSession(connectionString!, factory, factory.CreateClient(), cache);
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

    private sealed record SupplierGoldenRow(string Name, decimal Revenue, int Units, double SharePct, bool IsUnknown);

    private sealed class SupplierEndpointSession(
        string connectionString,
        SupplierEndpointFactory factory,
        HttpClient client,
        TestAnalyticsCacheService cache) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;
        public SupplierEndpointFactory Factory { get; } = factory;
        public HttpClient Client { get; } = client;
        public TestAnalyticsCacheService Cache { get; } = cache;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }

    private sealed class SupplierEndpointFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _connectionString;
        private readonly TestAnalyticsCacheService _cache;
        private readonly OperationsAnalyticsIntegrityRegistry _registry;

        public SupplierEndpointFactory(
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
        public CacheFootprintSnapshot GetFootprintSnapshot() => new("rq645-test", false, false, _entries.Count);
        public void Clear() => _entries.Clear();
    }
}
