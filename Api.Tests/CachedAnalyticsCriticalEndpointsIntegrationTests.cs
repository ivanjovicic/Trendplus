using System.Globalization;
using System.Net;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Inventory.Models;
using Api.Services;
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
        Assert.Equal(JsonValueKind.Null, root.GetProperty("avgBasketValue").ValueKind);
        Assert.Equal("receipt_grain_unavailable", root.GetProperty("basketMetricsReasonCode").GetString());
        Assert.Equal("sales_document", root.GetProperty("salesUnit").GetString());
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
        Assert.Equal(JsonValueKind.Null, root.GetProperty("avgBasketValue").ValueKind);
        Assert.Equal("receipt_grain_unavailable", root.GetProperty("basketMetricsReasonCode").GetString());
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
        Assert.Equal(JsonValueKind.Null, root.GetProperty("avgBasketValue").ValueKind);
        Assert.Equal("receipt_grain_unavailable", root.GetProperty("basketMetricsReasonCode").GetString());

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
        Assert.Equal(JsonValueKind.Null, root.GetProperty("estimatedInventoryValue").ValueKind);

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
    public async Task DashboardBootstrap_UndatedSectionsShareTheObservedThirtyDaySalesWindow()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 90,
                DatumProdaje = new DateTime(2025, 11, 1, 9, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka
            {
                Id = 90,
                IdProdaja = 90,
                IdArtikal = 101,
                Kolicina = 2,
                Cena = 500m
            });
            await db.SaveChangesAsync();
        }

        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/dashboard/bootstrap?storeId=1&dataScope=all");
        var meta = root.GetProperty("meta");
        Assert.Equal("source_horizon", meta.GetProperty("defaultPeriodBasis").GetString());
        Assert.Equal("2025-12-08T00:00:00Z", meta.GetProperty("requestedPeriodFromUtc").GetDateTime().ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        Assert.Equal("2026-01-06T00:00:00Z", meta.GetProperty("requestedPeriodToUtc").GetDateTime().ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        Assert.Equal(meta.GetProperty("requestedPeriodFromUtc").GetDateTime(), meta.GetProperty("effectivePeriodFromUtc").GetDateTime());
        Assert.Equal(meta.GetProperty("requestedPeriodToUtc").GetDateTime(), meta.GetProperty("effectivePeriodToUtc").GetDateTime());

        const decimal expectedPeriodRevenue = 1_100m;
        Assert.Equal(expectedPeriodRevenue, root.GetProperty("summary").GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(expectedPeriodRevenue, root.GetProperty("dailySales").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(expectedPeriodRevenue, root.GetProperty("categoryData").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(expectedPeriodRevenue, root.GetProperty("genderData").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(expectedPeriodRevenue, root.GetProperty("supplierData").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(expectedPeriodRevenue, root.GetProperty("weekdayData").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(expectedPeriodRevenue, root.GetProperty("hourData").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(expectedPeriodRevenue, root.GetProperty("paymentData").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.DoesNotContain(root.GetProperty("dailySales").EnumerateArray(), row => row.GetProperty("date").GetString() == "2025-11-01");
    }

    [Fact]
    public async Task DashboardBootstrap_ExecutiveSuppliersUseRevenuePopulationNotDecisionSample()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.Dobavljaci.Add(new Dobavljac { Id = 3, Naziv = "Bez prodaje", DataOrigin = "existing" });
            db.Artikli.Add(new Artikli
            {
                Id = 104,
                Naziv = "Nema prodaju",
                IDDobavljac = 3,
                IDObjekat = 1,
                Kolicina = 0,
                MinimalnaKolicina = 0,
                NabavnaCena = null,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            });
            db.ProdajaZaglavlja.AddRange(
                new ProdajaZaglavlje { Id = 20, DatumProdaje = new DateTime(2026, 7, 7, 9, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "existing" },
                new ProdajaZaglavlje { Id = 21, DatumProdaje = new DateTime(2026, 7, 8, 9, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "existing" });
            db.ProdajaStavke.AddRange(
                new ProdajaStavka { Id = 20, IdProdaja = 20, IdArtikal = 101, Kolicina = 2, Cena = 100m },
                new ProdajaStavka { Id = 21, IdProdaja = 21, IdArtikal = 102, Kolicina = 1, Cena = 500m });
            await db.SaveChangesAsync();
        }

        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/dashboard/bootstrap?fromDate=2026-07-07&toDate=2026-08-06&storeId=1&dataScope=all");
        var supplierRows = root.GetProperty("supplierData").EnumerateArray().ToArray();
        var executiveSuppliers = root.GetProperty("executive").GetProperty("topSuppliers").EnumerateArray().ToArray();

        var revenueSupplierRows = supplierRows.Where(row => row.GetProperty("totalRevenue").GetDecimal() != 0m).ToArray();
        Assert.Equal(new[] { 2, 1 }, revenueSupplierRows.Select(row => row.GetProperty("dobavljacId").GetInt32()).ToArray());
        Assert.Equal(revenueSupplierRows.Select(row => row.GetProperty("dobavljacId").GetInt32()), executiveSuppliers.Select(row => row.GetProperty("supplierId").GetInt32()));
        Assert.Equal(revenueSupplierRows.Select(row => row.GetProperty("totalRevenue").GetDecimal()), executiveSuppliers.Select(row => row.GetProperty("revenue").GetDecimal()));
        Assert.DoesNotContain(executiveSuppliers, row => row.GetProperty("revenue").GetDecimal() == 0m);
        Assert.All(executiveSuppliers, row => Assert.Equal(JsonValueKind.Null, row.GetProperty("marginContribution").ValueKind));
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
    public async Task TransactionStats_HidesReceiptMetricsButPreservesSalesDocumentValueAndCount()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/transaction-stats?fromDate=2026-01-05&toDate=2026-01-07&storeId=1");

        Assert.Equal(2, root.GetProperty("totalTransactions").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("avgItemsPerTransaction").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("avgUnitsPerTransaction").ValueKind);
        Assert.Equal(550m, root.GetProperty("avgTransactionValue").GetDecimal());
        Assert.Equal("receipt_grain_unavailable", root.GetProperty("basketMetricsReasonCode").GetString());
        Assert.Equal("sales_document", root.GetProperty("salesUnit").GetString());
    }

    [Fact]
    public async Task WeeklyHeatmap_DisablesUnsupportedReceiptCountButKeepsRevenueAndUnits()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/advanced/v2/weekly-heatmap?fromDate=2026-01-05&toDate=2026-01-07");

        Assert.Equal("receipt_grain_unavailable", root.GetProperty("transactionMetricReasonCode").GetString());
        var cells = root.GetProperty("cells").EnumerateArray().ToArray();
        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.Equal(JsonValueKind.Null, cell.GetProperty("transactions").ValueKind));
        Assert.Equal(1_300m, cells.Sum(cell => cell.GetProperty("revenue").GetDecimal()));
        Assert.Equal(10, cells.Sum(cell => cell.GetProperty("units").GetInt32()));
    }

    [Fact]
    public async Task V2DateOnlyUpperBound_IncludesTheEntireFinalCalendarDay_AndReportsUnknownFreshness()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 4,
                DatumProdaje = new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 15, IdProdaja = 4, IdArtikal = 101, Kolicina = 2, Cena = 100m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/v2/weekly-heatmap?fromDate=2026-01-07&toDate=2026-01-07");
        var cells = root.GetProperty("cells").EnumerateArray().ToArray();

        Assert.Equal(200m, cells.Sum(cell => cell.GetProperty("revenue").GetDecimal()));
        Assert.Equal(2, cells.Sum(cell => cell.GetProperty("units").GetInt32()));
        Assert.True(root.GetProperty("meta").GetProperty("success").GetBoolean());
        Assert.Equal("unknown", root.GetProperty("meta").GetProperty("dataFreshnessStatus").GetString());
        Assert.Equal("source_refresh_unverified", root.GetProperty("meta").GetProperty("dataFreshnessReasonCode").GetString());
    }

    [Fact]
    public async Task SmartReorder_KeepsProfitUnknownWhenRecommendedUnitsHaveNoCost()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.Artikli.Add(new Artikli
            {
                Id = 104, Naziv = "No cost probe", IDDobavljac = 1, IDObjekat = 1,
                Kolicina = 0, MinimalnaKolicina = 5, ProdajnaCena = 100m,
                DataOrigin = "existing", UpdatedAt = DateTime.UtcNow
            });
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 5, DatumProdaje = new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1, DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 16, IdProdaja = 5, IdArtikal = 104, Kolicina = 5, Cena = 100m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/v2/smart-reorder?fromDate=2026-01-07&toDate=2026-01-07");
        var item = root.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("artikalId").GetInt32() == 104);

        Assert.True(item.GetProperty("recommendedQty").GetInt32() > 0);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("reorderCost").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("expectedProfit").ValueKind);
        Assert.False(item.GetProperty("profitReliable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("summary").GetProperty("expectedProfitFromReorder").ValueKind);
        Assert.Equal("reorder_profit_incomplete_cost_or_price", root.GetProperty("meta").GetProperty("warningCode").GetString());
    }

    [Fact]
    public async Task WeeklyChangelog_LeavesPercentChangesUnknownWithoutPriorWeekBaseline()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 6, DatumProdaje = DateTime.UtcNow.AddDays(-1), IDObjekat = 1, DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 17, IdProdaja = 6, IdArtikal = 101, Kolicina = 1, Cena = 100m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory, "/api/analytics/advanced/v2/weekly-changelog");

        Assert.Equal(JsonValueKind.Null, root.GetProperty("revenueChangePct").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("unitChangePct").ValueKind);
        Assert.All(root.GetProperty("categoryChanges").EnumerateArray(), item =>
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("changePct").ValueKind);
            Assert.Equal("no_baseline", item.GetProperty("baselineStatus").GetString());
        });
        Assert.Equal("comparison_baseline_unavailable", root.GetProperty("meta").GetProperty("warningCode").GetString());
        Assert.Equal("current_snapshot_transition_history_unavailable", root.GetProperty("oosCountScope").GetString());
    }

    [Fact]
    public async Task ProductLifecycle_ReportsNoBaselineInsteadOfZeroPercentGrowth()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.Artikli.Add(new Artikli
            {
                Id = 104, Naziv = "Launch probe", IDDobavljac = 1, IDObjekat = 1,
                Kolicina = 3, MinimalnaKolicina = 1, NabavnaCena = 50m,
                DataOrigin = "existing", UpdatedAt = DateTime.UtcNow
            });
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 7, DatumProdaje = new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1, DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 18, IdProdaja = 7, IdArtikal = 104, Kolicina = 2, Cena = 100m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/v2/product-lifecycle?fromDate=2026-01-05&toDate=2026-01-07");
        var item = root.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("artikalId").GetInt32() == 104);

        Assert.Equal(JsonValueKind.Null, item.GetProperty("trendPct").ValueKind);
        Assert.Equal("no_baseline", item.GetProperty("baselineStatus").GetString());
        Assert.Equal("LAUNCH", item.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task SupplierScoringV2_UsesItemsEnvelopeWithUnknownFreshnessMeta()
    {
        await using var factory = CreateFactory();

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/v2/supplier-scoring-v2?fromDate=2026-01-05&toDate=2026-01-07");

        Assert.NotEmpty(root.GetProperty("items").EnumerateArray());
        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal("unknown", meta.GetProperty("dataFreshnessStatus").GetString());
        Assert.Equal("source_refresh_unverified", meta.GetProperty("dataFreshnessReasonCode").GetString());
    }

    [Fact]
    public async Task V2EmptyResult_HasExplicitEmptyMetaInsteadOfBareZeroData()
    {
        await using var factory = CreateFactory();

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/v2/weekly-heatmap?fromDate=2030-01-01&toDate=2030-01-02");

        Assert.Empty(root.GetProperty("cells").EnumerateArray());
        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal("no_sales_in_period", meta.GetProperty("emptyReason").GetString());
        Assert.Equal("insufficient_data", meta.GetProperty("dataQualityStatus").GetString());
        Assert.Equal("unknown", meta.GetProperty("dataFreshnessStatus").GetString());
    }

    [Fact]
    public async Task BasketAffinity_ReturnsUnavailableReasonInsteadOfAnEmptyBasketResult()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/advanced/v2/basket-affinity?fromDate=2026-01-05&toDate=2026-01-07");

        Assert.Equal("receipt_grain_unavailable", root.GetProperty("reasonCode").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("totalMultiItemTransactions").ValueKind);
        Assert.Empty(root.GetProperty("pairs").EnumerateArray());
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

        Assert.Equal("source_horizon", existingWorkflow.HorizonBasis);
        var expectedExistingHorizon = await ObservedSalesHorizonResolver.ResolveAsync(
            trendDb,
            storeId: null,
            supplierId: null,
            dataScope: "existing",
            CancellationToken.None);
        Assert.Equal(expectedExistingHorizon?.Date, existingWorkflow.AsOfUtc?.Date);

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
    public async Task InventoryActionWorkflow_TransfersOnlyToMateriallyStrongerDemandAndKeepsUnknownCostNull()
    {
        await using var factory = CreateFactory();
        SeedInventoryTransferProbeData(factory.Services);

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var workflow = await InventoryEndpoints.GetInventoryActionWorkflowAsync(
            services.GetRequiredService<IAnalyticsCacheService>(),
            services.GetRequiredService<TrendplusDbContext>(),
            services.GetRequiredService<AnalyticsDbContext>(),
            new NoopInventoryActionDecisionService(),
            storeId: null,
            supplierId: null,
            search: "TransferProbe",
            ct: CancellationToken.None,
            dataScope: "existing");

        var transfer = Assert.Single(workflow.Items.Where(item => item.ActionType == "transfer"));
        Assert.Equal("Prodavnica 1", transfer.FromStoreName);
        Assert.Equal("Prodavnica 2", transfer.ToStoreName);
        Assert.Equal(5, transfer.SuggestedQty);
        Assert.Null(transfer.EstimatedValue);
        Assert.Equal("source_horizon", transfer.DatasetContext?.HorizonBasis);
        Assert.Contains("destination_demand_materially_stronger", transfer.SignalReasonCodes!);
        Assert.Contains(workflow.Items, item => item.ActionType == "clearance" && item.ArtikalId == 924);
        Assert.Contains(workflow.Items, item => item.ActionType == "markdown" && item.ArtikalId == 926);
        Assert.DoesNotContain(workflow.Items, item => item.ActionType == "clearance" && item.ArtikalId == 925);
        Assert.Contains(workflow.Items, item => item.ActionType == "dopuna" && item.ArtikalId == 921);
        Assert.DoesNotContain(workflow.Items, item => item.ActionType == "dopuna" && item.ArtikalId == 927);
        Assert.Contains(workflow.Items, item => item.ActionType == "dopuna" && item.ArtikalId == 928);
    }

    private static void SeedInventoryTransferProbeData(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
        var recentSaleDate = DateTime.UtcNow.Date.AddDays(-1).AddHours(12);
        db.Artikli.AddRange(
            new Artikli { Id = 920, PLU = "TRANSFERPROBE-STRONG", Naziv = "TransferProbe Strong source", IDObjekat = 1, Kolicina = 20, MinimalnaKolicina = 5, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 921, PLU = "TRANSFERPROBE-STRONG", Naziv = "TransferProbe Strong destination", IDObjekat = 2, Kolicina = 1, MinimalnaKolicina = 6, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 922, PLU = "TRANSFERPROBE-EQUAL", Naziv = "TransferProbe Equal source", IDObjekat = 1, Kolicina = 20, MinimalnaKolicina = 5, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 923, PLU = "TRANSFERPROBE-EQUAL", Naziv = "TransferProbe Equal destination", IDObjekat = 2, Kolicina = 1, MinimalnaKolicina = 6, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 924, PLU = "TRANSFERPROBE-CLEARANCE", Naziv = "TransferProbe Clearance candidate", IDObjekat = 1, Kolicina = 20, MinimalnaKolicina = 5, NabavnaCena = 25m, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 925, PLU = "TRANSFERPROBE-NEW", Naziv = "TransferProbe Fresh receipt", IDObjekat = 1, Kolicina = 20, MinimalnaKolicina = 5, NabavnaCena = 25m, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 926, PLU = "TRANSFERPROBE-MARKDOWN", Naziv = "TransferProbe Markdown candidate", IDObjekat = 1, Kolicina = 20, MinimalnaKolicina = 5, NabavnaCena = 25m, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 927, PLU = "TRANSFERPROBE-DORMANT", Naziv = "TransferProbe Dormant sold out", IDObjekat = 1, Kolicina = 0, MinimalnaKolicina = 0, NabavnaCena = 25m, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 928, PLU = "TRANSFERPROBE-OLDMIN", Naziv = "TransferProbe Old minimum no demand", IDObjekat = 1, Kolicina = 0, MinimalnaKolicina = 4, NabavnaCena = 25m, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow });
        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje { Id = 80, DatumProdaje = recentSaleDate, IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 81, DatumProdaje = recentSaleDate, IDObjekat = 2, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 82, DatumProdaje = recentSaleDate, IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 83, DatumProdaje = recentSaleDate, IDObjekat = 2, DataOrigin = "existing" });
        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 80, IdProdaja = 80, IdArtikal = 920, Kolicina = 4, Cena = 0m },
            new ProdajaStavka { Id = 81, IdProdaja = 81, IdArtikal = 921, Kolicina = 8, Cena = 0m },
            new ProdajaStavka { Id = 82, IdProdaja = 82, IdArtikal = 922, Kolicina = 4, Cena = 0m },
            new ProdajaStavka { Id = 83, IdProdaja = 83, IdArtikal = 923, Kolicina = 4, Cena = 0m });
        db.DnevnikPromena.AddRange(
            new DnevnikPromena { Id = 8400, ArtikalId = 924, IDObjekat = 1, TipPromene = TipPromeneConstants.UlazRobe, Datum = recentSaleDate.AddDays(-105), Kolicina = 20, Iznos = 500m, DataOrigin = "existing" },
            new DnevnikPromena { Id = 8401, ArtikalId = 925, IDObjekat = 1, TipPromene = TipPromeneConstants.UlazRobe, Datum = recentSaleDate.AddHours(-1), Kolicina = 20, Iznos = 500m, DataOrigin = "existing" },
            new DnevnikPromena { Id = 8403, ArtikalId = 928, IDObjekat = 1, TipPromene = TipPromeneConstants.UlazRobe, Datum = recentSaleDate.AddDays(-200), Kolicina = 4, Iznos = 100m, DataOrigin = "existing" },
            new DnevnikPromena { Id = 8402, ArtikalId = 926, IDObjekat = 1, TipPromene = TipPromeneConstants.UlazRobe, Datum = recentSaleDate.AddDays(-75), Kolicina = 20, Iznos = 500m, DataOrigin = "existing" });
        db.SaveChanges();
    }

    [Fact]
    public async Task CachedInventoryList_RespectsArticleAndJournalDataScope()
    {
        await using var factory = CreateFactory();
        SeedInventoryJournalScopeProbeData(factory.Services);

        var importedRoot = await GetJsonAsync(
            factory,
            "/api/analytics/cached/inventory/list?page=1&pageSize=10&storeId=1&search=JournalProbe&dataScope=imported");

        var importedItem = importedRoot.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(903, importedItem.GetProperty("id").GetInt32());
        Assert.Equal("insufficient_data", importedItem.GetProperty("sellThroughStatus").GetString());
        Assert.Equal(JsonValueKind.Null, importedItem.GetProperty("sellThroughRatio").ValueKind);
        Assert.False(importedItem.GetProperty("recommendationAllowed").GetBoolean());
        Assert.False(importedItem.GetProperty("isOpeningStockDerived").GetBoolean());
        Assert.Equal("unknown", importedItem.GetProperty("openingStockConfidence").GetString());
        Assert.Contains("opening_stock_unavailable", importedItem.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()));
        var importedMeta = importedRoot.GetProperty("meta");
        Assert.Equal("imported", importedMeta.GetProperty("requestedDataScope").GetString());
        Assert.Equal("imported", importedMeta.GetProperty("effectiveDataScope").GetString());
        Assert.Equal("article-and-sale-header-data-origin", importedMeta.GetProperty("provenanceBasis").GetString());

        var existingRoot = await GetJsonAsync(
            factory,
            "/api/analytics/cached/inventory/list?page=1&pageSize=10&storeId=1&search=JournalProbe&dataScope=existing");

        var existingItem = existingRoot.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(906, existingItem.GetProperty("id").GetInt32());
        Assert.Equal("insufficient_data", existingItem.GetProperty("sellThroughStatus").GetString());
        Assert.Equal(JsonValueKind.Null, existingItem.GetProperty("sellThroughRatio").ValueKind);
        Assert.False(existingItem.GetProperty("recommendationAllowed").GetBoolean());
        Assert.False(existingItem.GetProperty("isOpeningStockDerived").GetBoolean());
        Assert.Equal("unknown", existingItem.GetProperty("openingStockConfidence").GetString());
        Assert.Contains("opening_stock_unavailable", existingItem.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()));
        var existingMeta = existingRoot.GetProperty("meta");
        Assert.Equal("existing", existingMeta.GetProperty("requestedDataScope").GetString());
        Assert.Equal("existing", existingMeta.GetProperty("effectiveDataScope").GetString());
        Assert.Equal(
            importedItem.GetProperty("signalConfidencePct").GetDecimal(),
            existingItem.GetProperty("signalConfidencePct").GetDecimal());
    }

    [Fact]
    public async Task CachedInventoryList_UsesMatchingSaleHeaderOriginForSellThroughWindow()
    {
        await using var factory = CreateFactory();
        SeedInventorySalesOriginProbeData(factory.Services);
        var fromDate = Uri.EscapeDataString(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));
        var toDate = Uri.EscapeDataString(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));

        var importedRoot = await GetJsonAsync(
            factory,
            $"/api/analytics/cached/inventory/list?pageSize=10&search=OriginProbe&dataScope=imported&fromDate={fromDate}&toDate={toDate}");
        var importedItem = importedRoot.GetProperty("items").EnumerateArray().Single();

        var existingRoot = await GetJsonAsync(
            factory,
            $"/api/analytics/cached/inventory/list?pageSize=10&search=OriginProbe&dataScope=existing&fromDate={fromDate}&toDate={toDate}");
        var existingItem = existingRoot.GetProperty("items").EnumerateArray().Single();

        var allRoot = await GetJsonAsync(
            factory,
            $"/api/analytics/cached/inventory/list?pageSize=10&search=OriginProbe&dataScope=all&fromDate={fromDate}&toDate={toDate}");
        var allItems = allRoot.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(904, importedItem.GetProperty("id").GetInt32());
        Assert.Equal(905, existingItem.GetProperty("id").GetInt32());
        Assert.Equal(2, allItems.Length);
        Assert.True(
            importedItem.GetProperty("stockCoverDays").GetDecimal()
            > allItems.Single(item => item.GetProperty("id").GetInt32() == 904).GetProperty("stockCoverDays").GetDecimal());
        Assert.True(
            existingItem.GetProperty("stockCoverDays").GetDecimal()
            > allItems.Single(item => item.GetProperty("id").GetInt32() == 905).GetProperty("stockCoverDays").GetDecimal());
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
        var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        analyticsDb.Database.EnsureDeleted();
        analyticsDb.Database.EnsureCreated();

        analyticsDb.StoresDim.AddRange(
            new StoresDim { StoreKey = 1, StoreId = 1, StoreName = "Prodavnica 1", DataOrigin = "existing" },
            new StoresDim { StoreKey = 2, StoreId = 2, StoreName = "Prodavnica 2", DataOrigin = "existing" });
        analyticsDb.SuppliersDim.AddRange(
            new SuppliersDim { SupplierKey = 1, SupplierId = 1, Naziv = "Dobavljač A", DataOrigin = "existing" },
            new SuppliersDim { SupplierKey = 2, SupplierId = 2, Naziv = "Dobavljač B", DataOrigin = "existing" });

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
        analyticsDb.SaveChanges();
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

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 903,
                PLU = "JOURNALPROBE-001",
                Naziv = "JournalProbe Imported",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 10,
                MinimalnaKolicina = 5,
                NabavnaCena = 20m,
                DataOrigin = "access",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 906,
                PLU = "JOURNALPROBE-002",
                Naziv = "JournalProbe Existing",
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

    private static void SeedInventorySalesOriginProbeData(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 904,
                PLU = "ORIGINPROBE-IMPORTED",
                Naziv = "OriginProbe Imported",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 10,
                MinimalnaKolicina = 5,
                NabavnaCena = 20m,
                DataOrigin = "access",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 905,
                PLU = "ORIGINPROBE-EXISTING",
                Naziv = "OriginProbe Existing",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 10,
                MinimalnaKolicina = 5,
                NabavnaCena = 20m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            });

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje { Id = 9401, DatumProdaje = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "access" },
            new ProdajaZaglavlje { Id = 9402, DatumProdaje = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 9403, DatumProdaje = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 9404, DatumProdaje = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "access" });
        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 9501, IdProdaja = 9401, IdArtikal = 904, Kolicina = 2, Cena = 100m },
            new ProdajaStavka { Id = 9502, IdProdaja = 9402, IdArtikal = 904, Kolicina = 20, Cena = 100m },
            new ProdajaStavka { Id = 9503, IdProdaja = 9403, IdArtikal = 905, Kolicina = 3, Cena = 100m },
            new ProdajaStavka { Id = 9504, IdProdaja = 9404, IdArtikal = 905, Kolicina = 20, Cena = 100m });

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
        private readonly string _analyticsDatabaseName = $"cached-analytics-critical-dim-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();
                services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
                services.RemoveAll<AnalyticsDbContext>();
                services.RemoveAll<IDbContextFactory<AnalyticsDbContext>>();
                services.RemoveAll<IAnalyticsDbContext>();

                services.AddDbContextFactory<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddDbContext<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddScoped<ITrendplusDbContext>(sp =>
                    sp.GetRequiredService<TrendplusDbContext>());
                services.AddDbContextFactory<AnalyticsDbContext>(options =>
                    options.UseInMemoryDatabase(_analyticsDatabaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddDbContext<AnalyticsDbContext>(options =>
                    options.UseInMemoryDatabase(_analyticsDatabaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddScoped<IAnalyticsDbContext>(sp =>
                    sp.GetRequiredService<AnalyticsDbContext>());
            });
        }
    }
}
