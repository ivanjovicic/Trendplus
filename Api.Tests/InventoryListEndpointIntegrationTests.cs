using System.Net;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class InventoryListEndpointIntegrationTests
{
    [Fact]
    public async Task InventoryList_ComputesOosAndSellThroughSignalsFromSalesAndMovementHistory()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/list?search=OOS-101");

        var item = Assert.Single(root.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(101, item.GetProperty("id").GetInt32());
        Assert.Equal(0, item.GetProperty("kolicina").GetInt32());
        Assert.Equal(0m, item.GetProperty("estimatedValue").GetDecimal());
        Assert.Equal(InventorySignalCalculator.StockCoverOutOfStockRisk, item.GetProperty("stockCoverStatus").GetString());
        Assert.Equal(InventorySignalCalculator.SellThroughGood, item.GetProperty("sellThroughStatus").GetString());
        Assert.Equal(1m, item.GetProperty("sellThroughRatio").GetDecimal());
        Assert.True(item.GetProperty("recommendationAllowed").GetBoolean());
        Assert.Equal("good", item.GetProperty("dataQualityStatus").GetString());
        Assert.Contains("replenish_needed", item.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("stock_cover_out_of_stock_risk", item.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()));
        Assert.InRange(item.GetProperty("signalConfidencePct").GetDecimal(), 60m, 99m);

        AssertSuccessMeta(root.GetProperty("meta"));
    }

    [Fact]
    public async Task InventoryList_NoEvidenceReturnsInsufficientDataAndBlocksRecommendation()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/list?search=EMPTY-104");

        var item = Assert.Single(root.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(104, item.GetProperty("id").GetInt32());
        Assert.Equal(InventorySignalCalculator.StockCoverInsufficientData, item.GetProperty("stockCoverStatus").GetString());
        Assert.Equal(InventorySignalCalculator.SellThroughInsufficientData, item.GetProperty("sellThroughStatus").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("stockCoverDays").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("sellThroughRatio").ValueKind);
        Assert.False(item.GetProperty("recommendationAllowed").GetBoolean());
        Assert.Equal("insufficient_data", item.GetProperty("dataQualityStatus").GetString());
        Assert.Equal(35m, item.GetProperty("signalConfidencePct").GetDecimal());
        Assert.Contains("stock_cover_insufficient_data", item.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task InventoryList_AppliesStoreSupplierAndSearchFiltersTogether()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/inventory/list?storeId=1&supplierId=1&search=Model");

        var items = root.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.All(items, item => Assert.Equal(1, item.GetProperty("idObjekat").GetInt32()));
        Assert.All(items, item => Assert.Equal(1, item.GetProperty("idDobavljac").GetInt32()));
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetInt32() == 103);
    }

    [Fact]
    public async Task InventoryList_ValueSortAndPaginationAreDeterministic()
    {
        await using var factory = CreateFactory();
        var firstPage = await GetJsonAsync(
            factory,
            "/api/analytics/cached/inventory/list?storeId=1&sortBy=vrednost&page=1&pageSize=2");
        var secondPage = await GetJsonAsync(
            factory,
            "/api/analytics/cached/inventory/list?storeId=1&sortBy=vrednost&page=2&pageSize=2");

        var firstItems = firstPage.GetProperty("items").EnumerateArray().ToArray();
        var secondItems = secondPage.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(3, firstPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, firstItems.Length);
        Assert.Single(secondItems);
        Assert.Equal(102, firstItems[0].GetProperty("id").GetInt32());
        Assert.Equal(2_000m, firstItems[0].GetProperty("estimatedValue").GetDecimal());
        Assert.Equal(103, firstItems[1].GetProperty("id").GetInt32());
        Assert.Equal(500m, firstItems[1].GetProperty("estimatedValue").GetDecimal());
        Assert.Equal(101, secondItems[0].GetProperty("id").GetInt32());
        Assert.Equal(1, firstPage.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, secondPage.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task InventoryList_EmptyFilterReturnsExplicitEmptySuccessMeta()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/list?search=DOES-NOT-EXIST");

        Assert.Empty(root.GetProperty("items").EnumerateArray());
        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal("no_inventory_items", meta.GetProperty("emptyReason").GetString());
        Assert.Equal("insufficient_data", meta.GetProperty("dataQualityStatus").GetString());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
    }

    [Fact]
    public async Task InventoryList_UncachedEmptyFilterReturnsExplicitEmptySuccessMeta()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/inventory/list?search=DOES-NOT-EXIST");

        Assert.Empty(root.GetProperty("items").EnumerateArray());
        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal("no_inventory_items", meta.GetProperty("emptyReason").GetString());
        Assert.Equal("insufficient_data", meta.GetProperty("dataQualityStatus").GetString());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
    }

    [Fact]
    public async Task InventoryList_ClampsInvalidPagingArguments()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/list?page=-10&pageSize=5001");

        Assert.Equal(1, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(1000, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(4, root.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task InventoryList_UncachedRouteMatchesSeededRowCountAndEmptyMeta()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory, "/api/analytics/inventory/list?page=-10&pageSize=5001");

        Assert.Equal(1, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(1000, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(4, root.GetProperty("items").GetArrayLength());
    }

    private static InventoryFactory CreateFactory()
    {
        var factory = new InventoryFactory();
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
                PLU = "OOS-101",
                Naziv = "Model OOS",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 0,
                MinimalnaKolicina = 5,
                NabavnaCena = 100m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 102,
                PLU = "HEALTHY-102",
                Naziv = "Model Healthy",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 10,
                MinimalnaKolicina = 2,
                NabavnaCena = 200m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new Artikli
            {
                Id = 103,
                PLU = "OTHER-103",
                Naziv = "Drugi artikal",
                IDObjekat = 1,
                IDDobavljac = 2,
                Kolicina = 5,
                MinimalnaKolicina = 1,
                NabavnaCena = 100m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new Artikli
            {
                Id = 104,
                PLU = "EMPTY-104",
                Naziv = "Bez signala",
                IDObjekat = 2,
                IDDobavljac = 1,
                Kolicina = 0,
                MinimalnaKolicina = 2,
                NabavnaCena = 50m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow.AddMinutes(-4)
            });

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 201,
                DatumProdaje = DateTime.UtcNow.AddDays(-5),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 202,
                DatumProdaje = DateTime.UtcNow.AddDays(-4),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
        db.ProdajaStavke.AddRange(
            new ProdajaStavka
            {
                Id = 301,
                IdProdaja = 201,
                IdArtikal = 101,
                Kolicina = 12,
                Cena = 250m,
                NabavnaCena = 100m
            },
            new ProdajaStavka
            {
                Id = 302,
                IdProdaja = 202,
                IdArtikal = 102,
                Kolicina = 4,
                Cena = 400m,
                NabavnaCena = 200m
            });

        db.DnevnikPromena.AddRange(
            new DnevnikPromena
            {
                Id = 401,
                ArtikalId = 101,
                Datum = DateTime.UtcNow.AddDays(-5),
                TipPromene = TipPromeneConstants.Prodaja,
                Kolicina = -12,
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new DnevnikPromena
            {
                Id = 402,
                ArtikalId = 102,
                Datum = DateTime.UtcNow.AddDays(-10),
                TipPromene = TipPromeneConstants.UlazRobe,
                Kolicina = 5,
                IDObjekat = 1,
                DataOrigin = "existing"
            });

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

    private static void AssertSuccessMeta(JsonElement meta)
    {
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(meta.GetProperty("correlationId").GetString()));
    }

    private sealed class InventoryFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = $"inventory-list-screen-{Guid.NewGuid():N}";
        private readonly InMemoryDatabaseRoot _databaseRoot = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();

                services.AddDbContextFactory<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName, _databaseRoot));
                services.AddDbContext<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName, _databaseRoot));
                services.AddScoped<ITrendplusDbContext>(sp => sp.GetRequiredService<TrendplusDbContext>());
            });
        }
    }
}
