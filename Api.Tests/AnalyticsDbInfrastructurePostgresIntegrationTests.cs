using Application.Artikli.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Infrastructure.DbContexts;
using Npgsql;
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

/// <summary>
/// RQ642: deterministic disposable PostgreSQL proof for analytics schema columns and route concurrency.
/// Supersedes the legacy <see cref="Trendplus2.Tests.AnalyticsDbInfrastructureTests"/> OperationsIntegrationFact skips.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Suite", "RQ642")]
public sealed class AnalyticsDbInfrastructurePostgresIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AnalyticsDbInfrastructurePostgresIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresRequiredFact(DisplayName = "Critical analytics columns must exist in DB schema (disposable PostgreSQL)")]
    public async Task AnalyticsSchema_CriticalColumnsExist_Postgres()
    {
        var connectionString = await CreateMigratedDatabaseAsync("tp_rq642_schema");
        await SeedCriticalColumnFixtureAsync(connectionString);

        await using var scope = CreateTrendDb(connectionString);
        var db = scope;

        var articleSchema = await db.Artikli.AsNoTracking()
            .Select(a => new { a.Id, a.IDDobavljac, a.IDTipObuce, a.NabavnaCenaDin, a.DataOrigin })
            .FirstOrDefaultAsync();
        Assert.NotNull(articleSchema);

        var stavkaSchema = await db.ProdajaStavke.AsNoTracking()
            .Select(ps => new { ps.Id, ps.IdProdaja, ps.Kolicina, ps.Cena, ps.NabavnaCena })
            .FirstOrDefaultAsync();
        Assert.NotNull(stavkaSchema);

        var zaglavljeSchema = await db.ProdajaZaglavlja.AsNoTracking()
            .Select(pz => new { pz.Id, pz.DatumProdaje, pz.IDObjekat })
            .FirstOrDefaultAsync();
        Assert.NotNull(zaglavljeSchema);

        var dnevnikSchema = await db.DnevnikPromena.AsNoTracking()
            .Select(d => new { d.Id, d.ArtikalId, d.Datum, d.TipPromene })
            .FirstOrDefaultAsync();
        Assert.NotNull(dnevnikSchema);
    }

    [PostgresRequiredFact(DisplayName = "Endpoint handles concurrent requests without crashing (disposable PostgreSQL)")]
    public async Task AnalyticsEndpoint_HandlesParallelRequests_Postgres()
    {
        var connectionString = await CreateMigratedDatabaseAsync("tp_rq642_parallel");
        await SeedParallelRouteFixtureAsync(connectionString);

        await using var factory = new AnalyticsDbInfrastructureEndpointFactory(connectionString);
        var client = factory.CreateClient();
        const string url = "/api/analytics/supplier-sales-stats?fromDate=2026-10-20&toDate=2026-10-22&dataScope=all";

        var tasks = Enumerable.Range(0, 10).Select(_ => client.GetAsync(url));
        var results = await Task.WhenAll(tasks);

        foreach (var response in results)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(
                response.IsSuccessStatusCode,
                $"Concurrent request failed with {(int)response.StatusCode}: {body}");
            Assert.Contains("\"meta\"", body, StringComparison.Ordinal);
            Assert.Contains("\"success\"", body, StringComparison.Ordinal);
        }
    }

    private async Task<string> CreateMigratedDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable)
        {
            throw new InvalidOperationException(
                "PostgreSQL is required for RQ642 infrastructure proof, but the Testcontainers fixture is unavailable.");
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Could not create an isolated PostgreSQL database for RQ642.");
        }

        await using (var trendDb = CreateTrendDb(connectionString))
        await using (var analyticsDb = CreateAnalyticsDb(connectionString))
        {
            await trendDb.Database.MigrateAsync();
            await analyticsDb.Database.MigrateAsync();
        }

        return connectionString;
    }

    private static TrendplusDbContext CreateTrendDb(string connectionString)
        => new(new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options);

    private static AnalyticsDbContext CreateAnalyticsDb(string connectionString)
        => new(new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(connectionString).Options);

    private static async Task SeedCriticalColumnFixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Dobavljaci" ("Id", "Naziv", "DataOrigin")
            VALUES (642001, 'RQ642 supplier', 'existing');

            INSERT INTO "Artikli" ("Id", "PLU", "Naziv", "IDDobavljac", "IDTipObuce", "NabavnaCenaDin", "UpdatedAt", "DataOrigin")
            VALUES (642001, 'RQ642-SCHEMA', 'RQ642 schema probe', 642001, 1, 10, '2026-10-01T00:00:00Z', 'existing');

            INSERT INTO prodaja_zaglavlje (id, broj_racuna, datum_prodaje, id_objekat, data_origin)
            VALUES (642001, 'RQ642-1', '2026-10-20T00:00:00Z', 1, 'access');

            INSERT INTO prodaja_stavke (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena)
            VALUES (642001, 642001, 642001, 1, 25, 10);

            INSERT INTO "DnevnikPromena" ("Id", "ArtikalId", "Datum", "TipPromene", "Iznos", "DataOrigin")
            VALUES (642001, 642001, '2026-10-20T00:00:00Z', 'nivelacija', 0, 'existing');
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedParallelRouteFixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Dobavljaci" ("Id", "Naziv", "DataOrigin")
            VALUES (642002, 'RQ642 parallel supplier', 'existing');

            INSERT INTO "Artikli" ("Id", "PLU", "Naziv", "IDDobavljac", "IDTipObuce", "NabavnaCenaDin", "UpdatedAt", "DataOrigin")
            VALUES (642002, 'RQ642-PAR', 'RQ642 parallel article', 642002, 1, 5, '2026-10-01T00:00:00Z', 'access');

            INSERT INTO prodaja_zaglavlje (id, broj_racuna, datum_prodaje, id_objekat, data_origin)
            VALUES (642002, 'RQ642-PAR', '2026-10-21T12:00:00Z', 1, 'access');

            INSERT INTO prodaja_stavke (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena)
            VALUES (642002, 642002, 642002, 2, 50, 5);
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed class AnalyticsDbInfrastructureEndpointFactory : WebApplicationFactory<global::Program>
{
    private readonly string _connectionString;

    public AnalyticsDbInfrastructureEndpointFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:AutoMigrate"] = "false",
            ["StartupReadiness:GateApiTraffic"] = "false",
            ["PROCESS_TYPE"] = "web",
            ["Workers:Enabled"] = "false",
            ["AnalyticsCache:Provider"] = "disabled",
            ["PerformanceLogging:CaptureHttpRequests"] = "false",
            ["Analytics:AllowLoopbackInProduction"] = "true",
            ["ConnectionStrings:DefaultConnection"] = _connectionString,
            ["ConnectionStrings:AnalyticsConnection"] = _connectionString,
            ["ConnectionStrings:OpenProductTrainingConnection"] = _connectionString,
            ["DailySales:TimeZoneId"] = "Europe/Belgrade"
        };

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
            services.RemoveAll<TrendplusDbContext>();
            services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
            services.RemoveAll<ITrendplusDbContext>();
            services.AddDbContextFactory<TrendplusDbContext>(options => options.UseNpgsql(_connectionString).ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
            services.AddScoped<TrendplusDbContext>(provider =>
                provider.GetRequiredService<IDbContextFactory<TrendplusDbContext>>().CreateDbContext());
            services.AddScoped<ITrendplusDbContext>(provider => provider.GetRequiredService<TrendplusDbContext>());

            services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
            services.RemoveAll<AnalyticsDbContext>();
            services.RemoveAll<IAnalyticsDbContext>();
            services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(_connectionString).ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
            services.AddScoped<IAnalyticsDbContext>(provider => provider.GetRequiredService<AnalyticsDbContext>());

            services.RemoveAll<IAnalyticsCacheService>();
            services.AddSingleton<IAnalyticsCacheService, DisabledAnalyticsCacheService>();
        });
    }
}

internal sealed class DisabledAnalyticsCacheService : IAnalyticsCacheService
{
    public bool IsRedisAvailable => false;
    public bool IsRedisEnabled => false;
    public CacheFootprintSnapshot GetFootprintSnapshot() => new("disabled", false, false, 0);
    public void SetRedisEnabled(bool enabled) { }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => Task.FromResult<T?>(null);
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        => factory();

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
}
