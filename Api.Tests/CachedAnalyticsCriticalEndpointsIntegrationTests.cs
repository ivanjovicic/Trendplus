using System.Net;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Inventory.Models;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trendplus2.Endpoints;
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
    public async Task SalesSummary_StoreFilterDoesNotLeakOtherStoreRevenue()
    {
        await using var factory = CreateFactory();
        var store1 = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/summary?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");
        var store2 = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/summary?fromDate=2026-01-05&toDate=2026-01-07&storeId=2");

        Assert.Equal(1_100m, store1.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(200m, store2.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(1, store2.GetProperty("totalTransactions").GetInt32());
        Assert.Equal(4, store2.GetProperty("totalUnits").GetInt32());
        Assert.True(store1.GetProperty("meta").GetProperty("success").GetBoolean());
        Assert.True(store2.GetProperty("meta").GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task SalesSummary_AdjacentDayWindowsDoNotOverlap()
    {
        await using var factory = CreateFactory();
        var firstDay = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/summary?fromDate=2026-01-05&toDate=2026-01-06&storeId=1");
        var secondDay = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/summary?fromDate=2026-01-06&toDate=2026-01-07&storeId=1");
        var bothDays = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/summary?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");

        Assert.Equal(800m, firstDay.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(300m, secondDay.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(1_100m, bothDays.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(
            bothDays.GetProperty("totalRevenue").GetDecimal(),
            firstDay.GetProperty("totalRevenue").GetDecimal() + secondDay.GetProperty("totalRevenue").GetDecimal());
    }

    [Fact]
    public async Task SalesSummary_InvalidPeriod_ReturnsBadRequestNotEmptySuccess()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/analytics/cached/sales/summary?fromDate=2026-01-07&toDate=2026-01-05&storeId=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertInvalidRangeIsNotEmptySuccess(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TopProducts_InvalidPeriod_ReturnsBadRequestNotEmptySuccess()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/analytics/cached/sales/top-products?fromDate=2026-01-07&toDate=2026-01-05&storeId=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertInvalidRangeIsNotEmptySuccess(await response.Content.ReadAsStringAsync());
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
    public async Task TopProducts_ExposesMarginTrustPayloadForDashboardRows()
    {
        var payload = new TopProductsAdvancedResultDto
        {
            ByRevenue =
            [
                new TopProductAdvancedItemDto
                {
                    ProductId = 101,
                    Sku = "SKU-101",
                    ProductName = "Runner 101",
                    Revenue = 125000m,
                    Units = 12,
                    VelocityUnitsPerDay = 1.5m,
                    MarginImpact = 34000m,
                    StockStatus = "good",
                    TrendPct = 12.4m,
                    MarginQualityLabel = "Margin signal dostupan",
                    MarginQualityTier = "good",
                    MarginQualityShortLabel = "Dostupno",
                    MarginQualityTooltip = "Margin impact je izračunat iz dostupne nabavne cene.",
                    DataQualityStatus = "good",
                    StatusReason = "Margin signal je potvrđen na osnovu dostupne nabavne cene.",
                    ReasonCodes = ["margin_available"]
                }
            ],
            ByUnits =
            [
                new TopProductAdvancedItemDto
                {
                    ProductId = 102,
                    Sku = "SKU-102",
                    ProductName = "Runner 102",
                    Revenue = 98000m,
                    Units = 9,
                    VelocityUnitsPerDay = 1.1m,
                    MarginImpact = null,
                    StockStatus = "warning",
                    TrendPct = -4.8m,
                    MarginQualityLabel = "Nedovoljno podataka",
                    MarginQualityTier = "insufficient_data",
                    MarginQualityShortLabel = "Nedostaje dokaz",
                    MarginQualityTooltip = "Nabavna cena nije dostupna, pa margin signal nije potvrđen.",
                    DataQualityStatus = "insufficient_data",
                    StatusReason = "Nabavna cena nije dostupna za ovaj artikal.",
                    ReasonCodes = ["missing_cost"]
                }
            ],
            ByVelocity = [],
            ByMarginImpact =
            [
                new TopProductAdvancedItemDto
                {
                    ProductId = 101,
                    Sku = "SKU-101",
                    ProductName = "Runner 101",
                    Revenue = 125000m,
                    Units = 12,
                    VelocityUnitsPerDay = 1.5m,
                    MarginImpact = 34000m,
                    StockStatus = "good",
                    TrendPct = 12.4m,
                    MarginQualityLabel = "Margin signal dostupan",
                    MarginQualityTier = "good",
                    MarginQualityShortLabel = "Dostupno",
                    MarginQualityTooltip = "Margin impact je izračunat iz dostupne nabavne cene.",
                    DataQualityStatus = "good",
                    StatusReason = "Margin signal je potvrđen na osnovu dostupne nabavne cene.",
                    ReasonCodes = ["margin_available"]
                },
                new TopProductAdvancedItemDto
                {
                    ProductId = 102,
                    Sku = "SKU-102",
                    ProductName = "Runner 102",
                    Revenue = 98000m,
                    Units = 9,
                    VelocityUnitsPerDay = 1.1m,
                    MarginImpact = null,
                    StockStatus = "warning",
                    TrendPct = -4.8m,
                    MarginQualityLabel = "Nedovoljno podataka",
                    MarginQualityTier = "insufficient_data",
                    MarginQualityShortLabel = "Nedostaje dokaz",
                    MarginQualityTooltip = "Nabavna cena nije dostupna, pa margin signal nije potvrđen.",
                    DataQualityStatus = "insufficient_data",
                    StatusReason = "Nabavna cena nije dostupna za ovaj artikal.",
                    ReasonCodes = ["missing_cost"]
                }
            ],
            MarginAvailable = true,
            MarginMessage = null
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var byMarginImpact = root.GetProperty("byMarginImpact").EnumerateArray().ToArray();

        Assert.Equal(2, byMarginImpact.Length);

        var firstRow = byMarginImpact[0];
        Assert.Equal("good", firstRow.GetProperty("marginQualityTier").GetString());
        Assert.Equal("Margin signal dostupan", firstRow.GetProperty("marginQualityLabel").GetString());
        Assert.Equal("Dostupno", firstRow.GetProperty("marginQualityShortLabel").GetString());
        Assert.Equal("Margin impact je izračunat iz dostupne nabavne cene.", firstRow.GetProperty("marginQualityTooltip").GetString());
        Assert.Equal("good", firstRow.GetProperty("dataQualityStatus").GetString());
        Assert.Equal("Margin signal je potvrđen na osnovu dostupne nabavne cene.", firstRow.GetProperty("statusReason").GetString());

        var reasonCodes = firstRow.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("margin_available", reasonCodes);
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
        Assert.Equal(0, root.GetProperty("lowStockCount").GetInt32());
        Assert.Equal(0, root.GetProperty("outOfStockCount").GetInt32());
        Assert.Equal(0m, root.GetProperty("estimatedInventoryValue").GetDecimal());

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal("no_inventory_data", meta.GetProperty("emptyReason").GetString());
        Assert.Equal("insufficient_data", meta.GetProperty("dataQualityStatus").GetString());
        Assert.Equal(JsonValueKind.Null, meta.GetProperty("errorCode").ValueKind);
    }

    [Fact]
    public async Task DashboardBootstrap_SeededData_ReturnsNonEmptyExecutiveSnapshot()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/dashboard/bootstrap?fromDate=2026-01-05&toDate=2026-01-07&storeId=1&dataScope=all");

        Assert.Equal(1_100m, root.GetProperty("summary").GetProperty("totalRevenue").GetDecimal());

        var executive = root.GetProperty("executive");
        Assert.True(executive.GetProperty("topSuppliers").EnumerateArray().Any());
        Assert.True(executive.GetProperty("topMarginProducts").EnumerateArray().Any());
        Assert.True(executive.GetProperty("negativeSignals").EnumerateArray().Any());

        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.NotEqual("ANALYTICS_TIMEOUT", meta.GetProperty("errorCode").GetString());
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
    public async Task TransactionStats_DistinguishesAverageLinesFromAverageUnits()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/transaction-stats?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");

        Assert.Equal(2, root.GetProperty("totalTransactions").GetInt32());
        // Receipt A: 2 lines (qty 2 + 1); receipt B: 1 line (qty 3) => avg lines = 1.5
        Assert.Equal(1.5m, root.GetProperty("avgItemsPerTransaction").GetDecimal());
        // Same receipts => avg units = (3 + 3) / 2 = 3.0
        Assert.Equal(3.0m, root.GetProperty("avgUnitsPerTransaction").GetDecimal());
        Assert.Equal(550m, root.GetProperty("avgTransactionValue").GetDecimal());
    }

    [Fact]
    public async Task InventoryInsightsAndDecisionBoard_RespectArticleDataScope()
    {
        await using var factory = CreateFactory();
        SeedInventoryScopeProbeData(factory.Services);

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var cache = services.GetRequiredService<IAnalyticsCacheService>();
        var trendDb = services.GetRequiredService<TrendplusDbContext>();
        var analyticsDb = services.GetRequiredService<AnalyticsDbContext>();
        var actionDecisionService = new NoopInventoryActionDecisionService();

        var importedInsights = await InventoryEndpoints.GetInventoryInsightsAsync(
            cache,
            trendDb,
            analyticsDb,
            storeId: null,
            supplierId: null,
            search: "ScopeProbe",
            sortBy: null,
            ct: CancellationToken.None,
            dataScope: "imported");

        Assert.Equal(1, importedInsights.TotalItems);
        Assert.Single(importedInsights.TopAgedItems);
        Assert.Equal(902, importedInsights.TopAgedItems[0].Id);

        var existingInsights = await InventoryEndpoints.GetInventoryInsightsAsync(
            cache,
            trendDb,
            analyticsDb,
            storeId: null,
            supplierId: null,
            search: "ScopeProbe",
            sortBy: null,
            ct: CancellationToken.None,
            dataScope: "existing");

        Assert.Equal(1, existingInsights.TotalItems);
        Assert.Single(existingInsights.TopAgedItems);
        Assert.Equal(901, existingInsights.TopAgedItems[0].Id);

        var importedWorkflow = await InventoryEndpoints.GetInventoryActionWorkflowAsync(
            cache,
            trendDb,
            analyticsDb,
            actionDecisionService,
            storeId: null,
            supplierId: null,
            search: "ScopeProbe",
            ct: CancellationToken.None,
            dataScope: "imported");

        var importedBoard = DecisionBoardEndpoints.BuildDecisionBoardResponse(
            generatedAtUtc: DateTime.UtcNow,
            periodFromUtc: null,
            periodToUtc: null,
            lastRefreshAtUtc: null,
            productDecisionCenter: null,
            inventoryInsights: null,
            inventoryWorkflow: importedWorkflow,
            supplierSummary: null,
            actions: [],
            outcomeSummary: null,
            refreshStatus: null,
            dataQualityHealth: null,
            loadWarnings: [],
            dataScope: "imported",
            storeId: null,
            supplierId: null);

        var importedInventoryCards = importedBoard.Sections
            .SelectMany(section => section.Cards)
            .Where(card => card.Kind == "inventory")
            .DistinctBy(card => card.Id)
            .ToArray();
        Assert.Single(importedInventoryCards);
        Assert.Contains("Imported", importedInventoryCards[0].Title, StringComparison.OrdinalIgnoreCase);

        var existingWorkflow = await InventoryEndpoints.GetInventoryActionWorkflowAsync(
            cache,
            trendDb,
            analyticsDb,
            actionDecisionService,
            storeId: null,
            supplierId: null,
            search: "ScopeProbe",
            ct: CancellationToken.None,
            dataScope: "existing");

        var existingBoard = DecisionBoardEndpoints.BuildDecisionBoardResponse(
            generatedAtUtc: DateTime.UtcNow,
            periodFromUtc: null,
            periodToUtc: null,
            lastRefreshAtUtc: null,
            productDecisionCenter: null,
            inventoryInsights: null,
            inventoryWorkflow: existingWorkflow,
            supplierSummary: null,
            actions: [],
            outcomeSummary: null,
            refreshStatus: null,
            dataQualityHealth: null,
            loadWarnings: [],
            dataScope: "existing",
            storeId: null,
            supplierId: null);

        var existingInventoryCards = existingBoard.Sections
            .SelectMany(section => section.Cards)
            .Where(card => card.Kind == "inventory")
            .DistinctBy(card => card.Id)
            .ToArray();
        Assert.Single(existingInventoryCards);
        Assert.Contains("Existing", existingInventoryCards[0].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CachedInventoryList_RespectsJournalDataScope()
    {
        await using var factory = CreateFactory();
        SeedInventoryJournalScopeProbeData(factory.Services);

        var importedRoot = await GetJsonAsync(
            factory,
            "/api/analytics/cached/inventory/list?page=1&pageSize=10&storeId=1&search=JournalProbe&dataScope=imported");

        var importedItem = importedRoot.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(903, importedItem.GetProperty("id").GetInt32());
        Assert.Equal("warning", importedItem.GetProperty("sellThroughStatus").GetString());
        Assert.Equal(0.4m, importedItem.GetProperty("sellThroughRatio").GetDecimal());

        var existingRoot = await GetJsonAsync(
            factory,
            "/api/analytics/cached/inventory/list?page=1&pageSize=10&storeId=1&search=JournalProbe&dataScope=existing");

        var existingItem = existingRoot.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(903, existingItem.GetProperty("id").GetInt32());
        Assert.Equal("critical", existingItem.GetProperty("sellThroughStatus").GetString());
        Assert.Equal(0.2667m, existingItem.GetProperty("sellThroughRatio").GetDecimal());
        Assert.NotEqual(
            importedItem.GetProperty("signalConfidencePct").GetDecimal(),
            existingItem.GetProperty("signalConfidencePct").GetDecimal());
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

    private static void SeedInventoryScopeProbeData(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 901,
                PLU = "SCOPEPROBE-EXISTING",
                Naziv = "ScopeProbe Existing",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 0,
                MinimalnaKolicina = 5,
                NabavnaCena = 10m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 902,
                PLU = "SCOPEPROBE-IMPORTED",
                Naziv = "ScopeProbe Imported",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 0,
                MinimalnaKolicina = 5,
                NabavnaCena = 10m,
                DataOrigin = "access",
                UpdatedAt = DateTime.UtcNow
            });

        db.SaveChanges();
    }

    private static void SeedInventoryJournalScopeProbeData(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

        db.Artikli.Add(new Artikli
        {
            Id = 903,
            PLU = "JOURNALPROBE-001",
            Naziv = "JournalProbe Item",
            IDObjekat = 1,
            IDDobavljac = 1,
            Kolicina = 10,
            MinimalnaKolicina = 5,
            NabavnaCena = 20m,
            DataOrigin = "existing",
            UpdatedAt = DateTime.UtcNow
        });

        db.DnevnikPromena.AddRange(
            new DnevnikPromena
            {
                Id = 7001,
                ArtikalId = 903,
                IDObjekat = 1,
                TipPromene = TipPromeneConstants.UlazRobe,
                Datum = new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
                Kolicina = 2,
                Iznos = 40m,
                DataOrigin = "access"
            },
            new DnevnikPromena
            {
                Id = 7002,
                ArtikalId = 903,
                IDObjekat = 1,
                TipPromene = TipPromeneConstants.Prodaja,
                Datum = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
                Kolicina = -5,
                Iznos = 100m,
                DataOrigin = "existing"
            });

        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 4,
            DatumProdaje = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc),
            IDObjekat = 1,
            DataOrigin = "existing"
        });

        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 15,
            IdProdaja = 4,
            IdArtikal = 903,
            Kolicina = 4,
            Cena = 100m
        });

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

    private sealed class NoopInventoryActionDecisionService : IInventoryActionDecisionService
    {
        public Task<IReadOnlyDictionary<string, InventoryActionDecisionDefinition>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, InventoryActionDecisionDefinition>>(new Dictionary<string, InventoryActionDecisionDefinition>());

        public Task<InventoryActionDecisionDefinition> UpsertAsync(InventoryActionDecisionUpsertRequest request, CancellationToken ct = default)
            => throw new NotSupportedException("Noop test double does not persist inventory action decisions.");
    }

    private static void AssertInvalidRangeIsNotEmptySuccess(string body)
    {
        Assert.False(string.IsNullOrWhiteSpace(body));
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var looksLikeEmptySuccess = root.TryGetProperty("meta", out var meta)
            && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("success", out var success)
            && success.ValueKind == JsonValueKind.True
            && meta.TryGetProperty("emptyReason", out var emptyReason)
            && emptyReason.ValueKind == JsonValueKind.String;
        Assert.False(looksLikeEmptySuccess);
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
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddDbContext<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddScoped<ITrendplusDbContext>(sp =>
                    sp.GetRequiredService<TrendplusDbContext>());
            });
        }
    }
}
