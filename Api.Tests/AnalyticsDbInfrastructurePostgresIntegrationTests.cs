using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
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
        await using var db = CreateTrendDb(connectionString);
        db.Artikli.Add(new Artikli
        {
            Id = 642001,
            PLU = "RQ642-SCHEMA",
            Naziv = "RQ642 schema probe",
            IDDobavljac = 642001,
            IDTipObuce = 1,
            NabavnaCenaDin = 10m,
            DataOrigin = "existing",
            UpdatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        db.Dobavljaci.Add(new Dobavljac { Id = 642001, Naziv = "RQ642 supplier", DataOrigin = "existing" });
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 642001,
            BrojRacuna = "RQ642-1",
            DatumProdaje = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            IDObjekat = 1,
            DataOrigin = "access"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 642001,
            IdProdaja = 642001,
            IdArtikal = 642001,
            Kolicina = 1,
            Cena = 25m,
            NabavnaCena = 10m
        });
        db.DnevnikPromena.Add(new DnevnikPromena
        {
            Id = 642001,
            ArtikalId = 642001,
            Datum = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            TipPromene = "nivelacija"
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedParallelRouteFixtureAsync(string connectionString)
    {
        await using var db = CreateTrendDb(connectionString);
        db.Dobavljaci.Add(new Dobavljac { Id = 642002, Naziv = "RQ642 parallel supplier", DataOrigin = "existing" });
        db.Artikli.Add(new Artikli
        {
            Id = 642002,
            PLU = "RQ642-PAR",
            Naziv = "RQ642 parallel article",
            IDDobavljac = 642002,
            IDTipObuce = 1,
            NabavnaCenaDin = 5m,
            DataOrigin = "access",
            UpdatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 642002,
            BrojRacuna = "RQ642-PAR",
            DatumProdaje = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc),
            IDObjekat = 1,
            DataOrigin = "access"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 642002,
            IdProdaja = 642002,
            IdArtikal = 642002,
            Kolicina = 2,
            Cena = 50m,
            NabavnaCena = 5m
        });
        await db.SaveChangesAsync();
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
            services.AddDbContextFactory<TrendplusDbContext>(options => options.UseNpgsql(_connectionString));
            services.AddScoped<TrendplusDbContext>(provider =>
                provider.GetRequiredService<IDbContextFactory<TrendplusDbContext>>().CreateDbContext());
            services.AddScoped<ITrendplusDbContext>(provider => provider.GetRequiredService<TrendplusDbContext>());

            services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
            services.RemoveAll<AnalyticsDbContext>();
            services.RemoveAll<IAnalyticsDbContext>();
            services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(_connectionString));
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
