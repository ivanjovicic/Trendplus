using System.Net;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class CachedAnalyticsCriticalEndpointsIntegrationTests
{
    [Fact]
    public async Task SalesSummary_ReturnsExactScopedTotalsAndHealthyMeta()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/analytics/cached/sales/summary?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");
        request.Headers.Add("X-Correlation-ID", "analytics-summary-test");

        using var response = await client.SendAsync(request);
        var root = await ReadSuccessJsonAsync(response);

        Assert.Equal(1_100m, root.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(2, root.GetProperty("totalTransactions").GetInt32());
        Assert.Equal(6, root.GetProperty("totalUnits").GetInt32());
        Assert.Equal(550m, root.GetProperty("avgBasketValue").GetDecimal());
        Assert.InRange(root.GetProperty("avgItemPrice").GetDecimal(), 183.33m, 183.34m);

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.False(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(meta.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task SalesSummary_SupplierFilterDoesNotLeakOtherSupplierRevenue()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/summary?fromDate=2026-01-05&toDate=2026-01-07&storeId=1&supplierId=1");

        Assert.Equal(500m, root.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(2, root.GetProperty("totalTransactions").GetInt32());
        Assert.Equal(5, root.GetProperty("totalUnits").GetInt32());
        Assert.Equal(250m, root.GetProperty("avgBasketValue").GetDecimal());
        Assert.Equal(100m, root.GetProperty("avgItemPrice").GetDecimal());
        Assert.True(root.GetProperty("meta").GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task SalesSummary_EmptyPeriodReturnsInsufficientDataNotFakeError()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/summary?fromDate=2027-01-01&toDate=2027-01-31&storeId=1");

        Assert.Equal(0m, root.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(0, root.GetProperty("totalTransactions").GetInt32());
        Assert.Equal(0, root.GetProperty("totalUnits").GetInt32());

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal("insufficient_data", meta.GetProperty("dataQualityStatus").GetString());
        Assert.Equal("no_data_in_period", meta.GetProperty("emptyReason").GetString());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
    }

    [Fact]
    public async Task TopProducts_UsesIndependentRevenueAndUnitsRankings()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/top-products?fromDate=2026-01-05&toDate=2026-01-07&storeId=1&top=2");

        var byRevenue = root.GetProperty("byRevenue").EnumerateArray().ToArray();
        var byUnits = root.GetProperty("byUnits").EnumerateArray().ToArray();

        Assert.Equal(2, byRevenue.Length);
        Assert.Equal("Model B", byRevenue[0].GetProperty("productName").GetString());
        Assert.Equal(600m, byRevenue[0].GetProperty("totalRevenue").GetDecimal());
        Assert.Equal("Model A", byRevenue[1].GetProperty("productName").GetString());

        Assert.Equal(2, byUnits.Length);
        Assert.Equal("Model A", byUnits[0].GetProperty("productName").GetString());
        Assert.Equal(5, byUnits[0].GetProperty("totalUnits").GetInt32());
        Assert.Equal("Model B", byUnits[1].GetProperty("productName").GetString());

        Assert.True(root.GetProperty("meta").GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task InventoryBalance_ReturnsExactCountsAndValueForStore()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/balance?storeId=1");

        Assert.Equal(2, root.GetProperty("totalSku").GetInt32());
        Assert.Equal(12, root.GetProperty("totalOnHand").GetInt32());
        Assert.Equal(1, root.GetProperty("lowStockCount").GetInt32());
        Assert.Equal(0, root.GetProperty("outOfStockCount").GetInt32());
        Assert.Equal(2_200m, root.GetProperty("estimatedInventoryValue").GetDecimal());

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("emptyReason").ValueKind);
    }

    [Fact]
    public async Task InventoryBalance_UnknownSupplierReturnsExplicitEmptyMeta()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/balance?supplierId=9999");

        Assert.Equal(0, root.GetProperty("totalSku").GetInt32());
        Assert.Equal(0, root.GetProperty("totalOnHand").GetInt32());
        Assert.Equal(0m, root.GetProperty("estimatedInventoryValue").GetDecimal());

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal("no_inventory_data", meta.GetProperty("emptyReason").GetString());
        Assert.Equal("insufficient_data", meta.GetProperty("dataQualityStatus").GetString());
    }

    [Fact]
    public async Task QuickInsights_ReturnsBestDayTopProductAndScopedLowStockCount()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/quick-insights?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");

        Assert.Equal("Ponedeljak", root.GetProperty("bestDay").GetString());
        Assert.Equal(800m, root.GetProperty("bestDayRevenue").GetDecimal());
        Assert.Equal("Model B", root.GetProperty("topProduct").GetString());
        Assert.Equal(1, root.GetProperty("lowStockAlert").GetInt32());
    }

    [Fact]
    public async Task TransactionStats_ComputesReceiptLevelAveragesInsteadOfLineLevelAverages()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/transaction-stats?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");

        Assert.Equal(2, root.GetProperty("totalTransactions").GetInt32());
        Assert.Equal(1.5m, root.GetProperty("avgItemsPerTransaction").GetDecimal());
        Assert.Equal(550m, root.GetProperty("avgTransactionValue").GetDecimal());
    }

    private static CachedAnalyticsFactory CreateFactory()
    {
        var factory = new CachedAnalyticsFactory();
        Seed(factory.Services);
        return factory;
    }

    private static void Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

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
        return await ReadSuccessJsonAsync(response);
    }

    private static async Task<JsonElement> ReadSuccessJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body));
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed class CachedAnalyticsFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = $"cached-analytics-critical-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();

                services.AddDbContextFactory<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.AddDbContext<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.AddScoped<ITrendplusDbContext>(sp =>
                    sp.GetRequiredService<TrendplusDbContext>());
            });
        }
    }
}
