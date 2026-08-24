using System.Data.Common;
using System.Net;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Analytics;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class CachedAnalyticsOperationalFallbackTests
{
    [Fact]
    public async Task DailySales_OperationalFallbackIsWarningObjectNotBareArray()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/daily?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");

        Assert.Equal(JsonValueKind.Object, root.ValueKind);

        var items = root.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.True(item.GetProperty("totalRevenue").GetDecimal() > 0m));

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.True(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal("daily_sales_operational_fallback", meta.GetProperty("warningCode").GetString());
        Assert.Equal("warning", meta.GetProperty("dataQualityStatus").GetString());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
        Assert.Contains("operativ", meta.GetProperty("warningMessage").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InventoryStatus_OperationalFallbackSetsWarningAndKeepsArtikliCounts()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/status?lowStockThreshold=2");

        Assert.Equal(3, root.GetProperty("totalSkuCount").GetInt32());
        Assert.Equal(12, root.GetProperty("totalOnHand").GetInt32());
        Assert.Equal(1, root.GetProperty("lowStockCount").GetInt32());
        Assert.Equal(1, root.GetProperty("outOfStockCount").GetInt32());
        Assert.True(root.GetProperty("usedOperationalFallback").GetBoolean());

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.True(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal("inventory_status_operational_fallback", meta.GetProperty("warningCode").GetString());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
    }

    [Fact]
    public async Task DashboardBootstrap_InventoryOperationalFallbackIsVisibleInMeta()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/dashboard/bootstrap?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");

        var inventory = root.GetProperty("inventory");
        Assert.True(inventory.GetProperty("usedOperationalFallback").GetBoolean());
        Assert.Equal(3, inventory.GetProperty("totalSkuCount").GetInt32());
        Assert.Equal(12, inventory.GetProperty("totalOnHand").GetInt32());

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.True(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal("inventory_status_operational_fallback", meta.GetProperty("warningCode").GetString());
        Assert.Contains("Artikli", meta.GetProperty("warningMessage").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
    }

    [Fact]
    public async Task DashboardBootstrap_AfterRefreshInvalidation_RebuildsFreshSummary()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IAnalyticsCacheService>();
        var cacheAdmin = scope.ServiceProvider.GetRequiredService<AnalyticsCacheAdminService>();

        var url = "/api/analytics/cached/dashboard/bootstrap?fromDate=2026-01-05&toDate=2026-01-07&storeId=1";

        var initial = await GetJsonAsync(factory, url);
        var initialRevenue = initial.GetProperty("summary").GetProperty("totalRevenue").GetDecimal();

        db.ProdajaZaglavlja.Add(
            new ProdajaZaglavlje
            {
                Id = 4,
                DatumProdaje = new DateTime(2026, 1, 6, 12, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
        db.ProdajaStavke.Add(
            new ProdajaStavka
            {
                Id = 15,
                IdProdaja = 4,
                IdArtikal = 101,
                Kolicina = 4,
                Cena = 100m
            });
        db.SaveChanges();

        await cacheAdmin.ClearFamiliesAsync([AnalyticsCachePolicy.DashboardFamily], CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.DashboardBootstrapPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.DashboardAdvancedPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.SalesSummaryPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.DailySalesPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.CategoryDataPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.GenderDataPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.SupplierDataPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.TopProductsPrefix, CancellationToken.None);
        await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.TopProductsAdvancedPrefix, CancellationToken.None);

        var refreshed = await GetJsonAsync(factory, url);
        var refreshedRevenue = refreshed.GetProperty("summary").GetProperty("totalRevenue").GetDecimal();

        Assert.Equal(initialRevenue + 400m, refreshedRevenue);
        Assert.True(refreshed.GetProperty("meta").GetProperty("success").GetBoolean());
    }

    private static OperationalFallbackFactory CreateFactory()
    {
        var factory = new OperationalFallbackFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
        db.Database.EnsureCreated();
        Seed(db);
        return factory;
    }

    private static void Seed(TrendplusDbContext db)
    {
        db.Dobavljaci.AddRange(
            new Dobavljac { Id = 1, Naziv = "Dobavljač A", DataOrigin = "existing" },
            new Dobavljac { Id = 2, Naziv = "Dobavljač B", DataOrigin = "existing" });

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 101,
                Naziv = "Model A",
                IDDobavljac = 1,
                IDObjekat = 1,
                Kolicina = 2,
                MinimalnaKolicina = 5,
                NabavnaCena = 200m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 102,
                Naziv = "Model B",
                IDDobavljac = 2,
                IDObjekat = 1,
                Kolicina = 10,
                MinimalnaKolicina = 2,
                NabavnaCena = 180m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 103,
                Naziv = "Model C",
                IDDobavljac = 1,
                IDObjekat = 2,
                Kolicina = 0,
                MinimalnaKolicina = 1,
                NabavnaCena = 100m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            });

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 1,
                DatumProdaje = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 2,
                DatumProdaje = new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 3,
                DatumProdaje = new DateTime(2026, 1, 5, 11, 0, 0, DateTimeKind.Utc),
                IDObjekat = 2,
                DataOrigin = "existing"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 11, IdProdaja = 1, IdArtikal = 101, Kolicina = 2, Cena = 100m },
            new ProdajaStavka { Id = 12, IdProdaja = 1, IdArtikal = 102, Kolicina = 1, Cena = 600m },
            new ProdajaStavka { Id = 13, IdProdaja = 2, IdArtikal = 101, Kolicina = 3, Cena = 100m },
            new ProdajaStavka { Id = 14, IdProdaja = 3, IdArtikal = 103, Kolicina = 4, Cena = 50m });

        db.SaveChanges();
    }

    private static async Task<JsonElement> GetJsonAsync(WebApplicationFactory<global::Program> factory, string url)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body));
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed class OperationalFallbackFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = $"cached-analytics-fallback-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();
                services.RemoveAll<IAnalyticsDbContext>();

                services.AddDbContextFactory<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddDbContext<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddScoped<ITrendplusDbContext>(sp =>
                    sp.GetRequiredService<TrendplusDbContext>());
                services.AddScoped<IAnalyticsDbContext>(_ => new MissingAnalyticsRelationContext());
            });
        }
    }

    private sealed class MissingAnalyticsRelationContext : IAnalyticsDbContext
    {
        private static PostgresException Missing() =>
            new("relation does not exist", "ERROR", "ERROR", PostgresErrorCodes.UndefinedTable);

        public DbSet<ProductsDim> ProductsDim => throw Missing();
        public DbSet<StoresDim> StoresDim => throw Missing();
        public DbSet<PerformanceLog> PerformanceLogs => throw Missing();
        public DbSet<SalesFact> SalesFacts => throw Missing();
        public DbSet<SalesLineFact> SalesLineFacts => throw Missing();
        public DbSet<SuppliersDim> SuppliersDim => throw Missing();
        public DbSet<SeasonsDim> SeasonsDim => throw Missing();
        public DbSet<FootwearTypesDim> FootwearTypesDim => throw Missing();
        public DbSet<InventoryMovementFact> InventoryMovementFacts => throw Missing();
        public DbSet<ReturnFact> ReturnFacts => throw Missing();
        public DbSet<TrendProductSnapshot> TrendProductSnapshots => throw Missing();
        public DbSet<TrendProductMomentum> TrendProductMomentums => throw Missing();
        public DbSet<TrendplusIndexRecord> TrendplusIndexRecords => throw Missing();
        public DbSet<InventoryRecommendation> InventoryRecommendations => throw Missing();
        public DbSet<AnalyticsActionItem> AnalyticsActionItems => throw Missing();
        public DbSet<AnalyticsActionNote> AnalyticsActionNotes => throw Missing();

        public DbConnection GetDbConnection() => throw Missing();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw Missing();
    }
}
