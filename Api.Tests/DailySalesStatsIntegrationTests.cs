using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Models;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trendplus2.Tests;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DailySalesStatsIntegrationTests
{
    [Fact(DisplayName = "Daily sales endpoint returns valid JSON contract")]
    public async Task DailySalesStats_ReturnsValidJsonContract()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=3&dataScope=all");

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("requestedFrom", out _));
        Assert.True(root.TryGetProperty("requestedTo", out _));
        Assert.True(root.TryGetProperty("storeId", out _));
        Assert.True(root.TryGetProperty("topN", out _));
        Assert.True(root.TryGetProperty("dataScope", out _));
        Assert.True(root.TryGetProperty("topSuppliers", out _));
        Assert.True(root.TryGetProperty("topSuppliersOrder", out _));
        Assert.True(root.TryGetProperty("dateRows", out _));
        Assert.True(root.TryGetProperty("metadata", out _));

        Assert.Equal(JsonValueKind.Array, root.GetProperty("topSuppliers").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("dateRows").ValueKind);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("metadata").ValueKind);
    }

    [Fact(DisplayName = "Daily sales endpoint matches golden snapshot")]
    public async Task DailySalesStats_MatchesGoldenSnapshot()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=3&dataScope=all");

        GoldenSnapshotAssert.Matches("daily-sales-stats.contract.json", ProjectSnapshot(root));
    }

    [Fact(DisplayName = "Daily sales endpoint rejects invalid period")]
    public async Task DailySalesStats_InvalidPeriod_ReturnsBadRequest()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/analytics/daily-sales?fromDate=2026-01-03&toDate=2026-01-01");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Daily sales dataScope filters imported rows")]
    public async Task DailySalesStats_DataScopeFiltersRows()
    {
        await using var factory = CreateFactory();

        var allRoot = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=3&dataScope=all");
        var existingRoot = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=3&dataScope=existing");
        var importedRoot = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=3&dataScope=imported");

        Assert.Equal("all", allRoot.GetProperty("dataScope").GetString());
        Assert.Equal("existing", existingRoot.GetProperty("dataScope").GetString());
        Assert.Equal("imported", importedRoot.GetProperty("dataScope").GetString());

        Assert.True(allRoot.GetProperty("topSuppliers").GetArrayLength() >= existingRoot.GetProperty("topSuppliers").GetArrayLength());
        Assert.Equal(1, importedRoot.GetProperty("topSuppliers").GetArrayLength());
        Assert.Single(importedRoot.GetProperty("topSuppliersOrder").EnumerateArray());

        Assert.Contains(importedRoot.GetProperty("topSuppliers").EnumerateArray(), item => item.GetProperty("supplierName").GetString() == "Dobavljac C");
        Assert.DoesNotContain(importedRoot.GetProperty("topSuppliers").EnumerateArray(), item => item.GetProperty("supplierName").GetString() == "Dobavljac A");
    }

    [Fact(DisplayName = "Daily sales store filter can return no data with availability warning")]
    public async Task DailySalesStats_StoreFilterReturnsAvailabilityWarning()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-02-01&toDate=2026-02-03&storeId=1&topN=3&dataScope=all");

        Assert.Equal(1, root.GetProperty("storeId").GetInt32());
        Assert.Equal(3, root.GetProperty("dateRows").GetArrayLength());
        Assert.All(root.GetProperty("dateRows").EnumerateArray(), row =>
        {
            Assert.Equal(0, row.GetProperty("totalItemsSold").GetInt32());
            Assert.Equal(0m, row.GetProperty("totalRevenue").GetDecimal());
        });
        Assert.Contains(root.GetProperty("metadata").GetProperty("warnings").EnumerateArray(), x => x.GetString()!.Contains("Podaci su dostupni od", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Daily sales topN parameter limits supplier count")]
    public async Task DailySalesStats_TopNLimitsSuppliersCorrectly()
    {
        await using var factory = CreateFactory();

        var topN1 = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=1&dataScope=all");
        var topN2 = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=2&dataScope=all");
        var topN10 = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=10&dataScope=all");

        Assert.Equal(1, topN1.GetProperty("topSuppliers").GetArrayLength());
        Assert.Equal(2, topN2.GetProperty("topSuppliers").GetArrayLength());
        Assert.True(topN10.GetProperty("topSuppliers").GetArrayLength() <= 4);
    }

    [Fact(DisplayName = "Daily sales suppliers have valid revenue values")]
    public async Task DailySalesStats_SuppliersHaveValidRevenue()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=3&dataScope=all");

        var suppliers = root.GetProperty("topSuppliers").EnumerateArray().ToList();
        Assert.NotEmpty(suppliers);

        foreach (var supplier in suppliers)
        {
            var revenue = supplier.GetProperty("totalRevenue").GetDecimal();
            var qty = supplier.GetProperty("totalQty").GetInt32();
            
            Assert.True(revenue >= 0, "Revenue should not be negative");
            Assert.True(qty > 0, "Quantity should be greater than zero");
        }
    }

    [Fact(DisplayName = "Daily sales single-day range returns one date row")]
    public async Task DailySalesStats_SingleDayRangeReturnsSingleRow()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-01&storeId=1&topN=3&dataScope=all");

        Assert.Equal(1, root.GetProperty("dateRows").GetArrayLength());
        var row = root.GetProperty("dateRows").EnumerateArray().First();
        Assert.Equal("2026-01-01", row.GetProperty("date").GetDateTime().ToString("yyyy-MM-dd"));
    }

    [Fact(DisplayName = "Daily sales shift distribution is accurate")]
    public async Task DailySalesStats_ShiftDistributionIsAccurate()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-02&storeId=1&topN=3&dataScope=all");

        var dateRows = root.GetProperty("dateRows").EnumerateArray().ToList();
        Assert.True(dateRows.Count > 0, "Should have at least one date row");

        // Verify each date row has non-negative shift counts
        foreach (var dateRow in dateRows)
        {
            var firstShift = dateRow.GetProperty("firstShiftTotalItems").GetInt32();
            var secondShift = dateRow.GetProperty("secondShiftTotalItems").GetInt32();
            var total = dateRow.GetProperty("totalItemsSold").GetInt32();

            Assert.True(firstShift >= 0, "First shift count should be non-negative");
            Assert.True(secondShift >= 0, "Second shift count should be non-negative");
            Assert.True(total >= 0, "Total items should be non-negative");
        }
    }

    [Fact(DisplayName = "Daily sales without storeId returns data")]
    public async Task DailySalesStats_WithoutStoreIdReturnsData()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&topN=3&dataScope=all");

        Assert.Equal(JsonValueKind.Null, root.GetProperty("storeId").ValueKind);
        Assert.True(root.GetProperty("topSuppliers").GetArrayLength() > 0);
        Assert.True(root.GetProperty("dateRows").GetArrayLength() > 0);
    }

    [Fact(DisplayName = "Daily sales metadata provides diagnostics")]
    public async Task DailySalesStats_MetadataProvidesDiagnostics()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonRootAsync(factory, "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=3&dataScope=all");

        var metadata = root.GetProperty("metadata");
        
        // Verify all required metadata fields exist
        Assert.True(metadata.TryGetProperty("totalDays", out _));
        Assert.True(metadata.TryGetProperty("uniqueSuppliersInRange", out _));
        Assert.True(metadata.TryGetProperty("totalItemsInRange", out _));
        Assert.True(metadata.TryGetProperty("warnings", out var warnings));
        Assert.Equal(JsonValueKind.Array, warnings.ValueKind);
    }

    [Fact(DisplayName = "Daily sales endpoint is deterministic")]
    public async Task DailySalesStats_ProducesDeterministicJson()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        var url = "/api/analytics/daily-sales?fromDate=2026-01-01&toDate=2026-01-03&storeId=1&topN=3&dataScope=all";

        var first = await client.GetAsync(url);
        var second = await client.GetAsync(url);

        Assert.Equal(CanonicalizeJson(await first.Content.ReadAsStringAsync()), CanonicalizeJson(await second.Content.ReadAsStringAsync()));
    }

    private static DailySalesWebApplicationFactory CreateFactory()
    {
        var factory = new DailySalesWebApplicationFactory();
        SeedDatabase(factory.Services);
        return factory;
    }

    private static void SeedDatabase(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        db.Dobavljaci.AddRange(
            new Dobavljac { Id = 1, Naziv = "Dobavljac A", DataOrigin = "existing" },
            new Dobavljac { Id = 2, Naziv = "Dobavljac B", DataOrigin = "existing" },
            new Dobavljac { Id = 3, Naziv = "Dobavljac C", DataOrigin = "access" });

        db.Artikli.AddRange(
            new Artikli { Id = 101, Naziv = "A1", IDDobavljac = 1, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 102, Naziv = "B1", IDDobavljac = 2, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 103, Naziv = "Unknown", IDDobavljac = null, DataOrigin = "existing", UpdatedAt = DateTime.UtcNow },
            new Artikli { Id = 104, Naziv = "Imported", IDDobavljac = 3, DataOrigin = "access", UpdatedAt = DateTime.UtcNow });

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje { Id = 1, DatumProdaje = new DateTime(2026, 1, 1, 7, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 2, DatumProdaje = new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 3, DatumProdaje = new DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 4, DatumProdaje = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 5, DatumProdaje = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "access" });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 11, IdProdaja = 1, IdArtikal = 101, Kolicina = 5, Cena = 100m },
            new ProdajaStavka { Id = 12, IdProdaja = 2, IdArtikal = 102, Kolicina = 3, Cena = 200m },
            new ProdajaStavka { Id = 13, IdProdaja = 3, IdArtikal = 101, Kolicina = 2, Cena = 100m },
            new ProdajaStavka { Id = 14, IdProdaja = 4, IdArtikal = 103, Kolicina = 5, Cena = 150m },
            new ProdajaStavka { Id = 15, IdProdaja = 5, IdArtikal = 104, Kolicina = 7, Cena = 50m });

        db.SaveChanges();
    }

    private static async Task<JsonElement> GetJsonRootAsync(WebApplicationFactory<global::Program> factory, string url)
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync(url);

        Assert.True(response.IsSuccessStatusCode, $"Expected success for {url}, got {response.StatusCode}");
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));

        return JsonDocument.Parse(content).RootElement;
    }

    private static string CanonicalizeJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return json;
        }
    }

    private static object ProjectSnapshot(JsonElement root)
    {
        var topSuppliers = root.GetProperty("topSuppliers")
            .EnumerateArray()
            .Select(item => new
            {
                supplierId = item.TryGetProperty("supplierId", out var idEl) && idEl.ValueKind != JsonValueKind.Null ? idEl.GetInt32() : (int?)null,
                supplierName = item.GetProperty("supplierName").GetString(),
                isUnknown = item.GetProperty("isUnknown").GetBoolean(),
                totalQty = item.GetProperty("totalQty").GetInt32(),
                totalRevenue = item.GetProperty("totalRevenue").GetDecimal()
            })
            .ToList();

        var dateRows = root.GetProperty("dateRows")
            .EnumerateArray()
            .Select(item => new
            {
                date = item.GetProperty("date").GetDateTime().ToString("yyyy-MM-dd"),
                firstShiftTotalItems = item.GetProperty("firstShiftTotalItems").GetInt32(),
                secondShiftTotalItems = item.GetProperty("secondShiftTotalItems").GetInt32(),
                totalRevenue = item.GetProperty("totalRevenue").GetDecimal(),
                othersCount = item.GetProperty("othersCount").GetInt32(),
                totalItemsSold = item.GetProperty("totalItemsSold").GetInt32(),
                topSupplierCounts = item.GetProperty("topSupplierCounts").EnumerateArray().Select(x => x.GetInt32()).ToList()
            })
            .ToList();

        var metadata = root.GetProperty("metadata");
        return new
        {
            requestedFrom = root.GetProperty("requestedFrom").GetDateTime().ToString("yyyy-MM-dd"),
            requestedTo = root.GetProperty("requestedTo").GetDateTime().ToString("yyyy-MM-dd"),
            storeId = root.GetProperty("storeId").ValueKind == JsonValueKind.Null ? (int?)null : root.GetProperty("storeId").GetInt32(),
            topN = root.GetProperty("topN").GetInt32(),
            dataScope = root.GetProperty("dataScope").GetString(),
            topSuppliers,
            topSuppliersOrder = root.GetProperty("topSuppliersOrder").EnumerateArray().Select(x => x.GetString()).ToList(),
            dateRows,
            metadata = new
            {
                totalDays = metadata.GetProperty("totalDays").GetInt32(),
                uniqueSuppliersInRange = metadata.GetProperty("uniqueSuppliersInRange").GetInt32(),
                unknownSupplierPct = Math.Round(metadata.GetProperty("unknownSupplierPct").GetDecimal(), 2),
                unknownSupplierItems = metadata.GetProperty("unknownSupplierItems").GetInt32(),
                offShiftItems = metadata.GetProperty("offShiftItems").GetInt32(),
                offShiftRevenue = metadata.GetProperty("offShiftRevenue").GetDecimal(),
                totalItemsInRange = metadata.GetProperty("totalItemsInRange").GetInt32(),
                duplicateReceiptGroupCount = metadata.GetProperty("duplicateReceiptGroupCount").GetInt32(),
                duplicateReceiptHeaderCount = metadata.GetProperty("duplicateReceiptHeaderCount").GetInt32(),
                receiptAmountMismatchCount = metadata.GetProperty("receiptAmountMismatchCount").GetInt32(),
                receiptAmountMismatchRevenue = metadata.GetProperty("receiptAmountMismatchRevenue").GetDecimal(),
                nonStandardReceiptCount = metadata.GetProperty("nonStandardReceiptCount").GetInt32(),
                nonStandardReceiptRevenue = metadata.GetProperty("nonStandardReceiptRevenue").GetDecimal(),
                debtReceiptCount = metadata.GetProperty("debtReceiptCount").GetInt32(),
                debtReceiptRevenue = metadata.GetProperty("debtReceiptRevenue").GetDecimal(),
                minAvailableDate = metadata.GetProperty("minAvailableDate").ValueKind == JsonValueKind.Null ? null : metadata.GetProperty("minAvailableDate").GetDateTime().ToString("yyyy-MM-dd"),
                maxAvailableDate = metadata.GetProperty("maxAvailableDate").ValueKind == JsonValueKind.Null ? null : metadata.GetProperty("maxAvailableDate").GetDateTime().ToString("yyyy-MM-dd"),
                warningsCount = metadata.GetProperty("warnings").GetArrayLength()
            }
        };
    }

    private sealed class DailySalesWebApplicationFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _dbName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<Application.Artikli.Common.Interfaces.ITrendplusDbContext>();

                services.AddDbContextFactory<TrendplusDbContext>(options => options.UseInMemoryDatabase(_dbName));
                services.AddDbContext<TrendplusDbContext>(options => options.UseInMemoryDatabase(_dbName));
                services.AddScoped<Application.Artikli.Common.Interfaces.ITrendplusDbContext>(sp => sp.GetRequiredService<TrendplusDbContext>());
            });
        }
    }
}