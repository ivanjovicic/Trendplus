using System.Net;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class InsightStudioLegacyEndpointsContractTests
{
    [Fact]
    public async Task KpiSnapshot_DateOnlyUpperBound_IncludesSalesOnFinalCalendarDay()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 20,
                DatumProdaje = new DateTime(2026, 1, 7, 15, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 120, IdProdaja = 20, IdArtikal = 101, Kolicina = 1, Cena = 250m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/kpi-snapshot?fromDate=2026-01-07&toDate=2026-01-07");

        Assert.Equal(250m, root.GetProperty("revenue").GetDecimal());
        Assert.Equal(1, root.GetProperty("transactions").GetInt32());
    }

    [Fact]
    public async Task KpiSnapshot_BoundarySaleIsNotDoubleCountedAcrossPeriods()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 21,
                DatumProdaje = new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 121, IdProdaja = 21, IdArtikal = 101, Kolicina = 1, Cena = 77m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/kpi-snapshot?fromDate=2026-01-06&toDate=2026-01-07");

        Assert.Equal(377m, root.GetProperty("revenue").GetDecimal());
        Assert.Equal(2, root.GetProperty("transactions").GetInt32());
    }

    [Fact]
    public async Task SupplierScorecard_DoesNotFabricateMarginBenchmarkWithoutCostCoverage()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.Artikli.Add(new Artikli
            {
                Id = 201,
                Naziv = "No cost supplier probe",
                IDDobavljac = 1,
                IDObjekat = 1,
                Kolicina = 5,
                MinimalnaKolicina = 1,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            });
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 22,
                DatumProdaje = new DateTime(2026, 1, 7, 9, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 122, IdProdaja = 22, IdArtikal = 201, Kolicina = 2, Cena = 100m });
            db.SaveChanges();
        }

        var rows = await GetJsonArrayAsync(factory,
            "/api/analytics/advanced/supplier-scorecard?fromDate=2026-01-07&toDate=2026-01-07");
        var supplier = rows.Single(x => x.GetProperty("dobavljacId").GetInt32() == 1);

        Assert.False(supplier.GetProperty("systemBenchmarkAvailable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, supplier.GetProperty("systemMarginPct").ValueKind);
    }

    [Fact]
    public async Task AbcClassification_EmptyPeriodHasExplicitMeta()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/abc-classification?fromDate=2030-01-01&toDate=2030-01-31");

        Assert.Empty(root.GetProperty("items").EnumerateArray());
        var meta = root.GetProperty("meta");
        Assert.True(meta.GetProperty("success").GetBoolean());
        Assert.Equal("no_sales_in_period", meta.GetProperty("emptyReason").GetString());
        Assert.Equal("insufficient_data", meta.GetProperty("dataQualityStatus").GetString());
    }

    [Fact]
    public async Task AgingStock_NeverSoldProductDoesNotUseUpdatedAtAsSaleEvidence()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.Artikli.Add(new Artikli
            {
                Id = 202,
                Naziv = "Never sold aging probe",
                IDDobavljac = 1,
                IDObjekat = 1,
                Kolicina = 4,
                MinimalnaKolicina = 1,
                NabavnaCena = 50m,
                DataOrigin = "existing",
                SourceUpdatedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory, "/api/analytics/advanced/aging-stock");
        var item = root.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == 202);

        Assert.True(item.GetProperty("neverSold").GetBoolean());
        Assert.Equal("never_sold", item.GetProperty("agingEvidenceStatus").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("lastSaleDate").ValueKind);
        Assert.True(item.GetProperty("daysWithoutSale").GetInt32() > 365);
    }

    [Fact]
    public async Task DailyAnalysis_MissingTargetDayIsNotNormalZeroSalesDay()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/daily-analysis?analysisDate=2026-01-07&fromDate=2026-01-05&toDate=2026-01-06");

        Assert.Equal("missing", root.GetProperty("targetDataStatus").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("targetRevenue").ValueKind);
        Assert.Equal("Nema podataka za ciljni dan", root.GetProperty("outlierLabel").GetString());
    }

    [Fact]
    public async Task DailyAnalysis_ExcludesTargetDayFromZScoreBaseline()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.ProdajaZaglavlja.AddRange(
                new ProdajaZaglavlje
                {
                    Id = 23, DatumProdaje = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc),
                    IDObjekat = 1, DataOrigin = "existing"
                },
                new ProdajaZaglavlje
                {
                    Id = 24, DatumProdaje = new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc),
                    IDObjekat = 1, DataOrigin = "existing"
                },
                new ProdajaZaglavlje
                {
                    Id = 25, DatumProdaje = new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc),
                    IDObjekat = 1, DataOrigin = "existing"
                });
            db.ProdajaStavke.AddRange(
                new ProdajaStavka { Id = 123, IdProdaja = 23, IdArtikal = 101, Kolicina = 1, Cena = 100m },
                new ProdajaStavka { Id = 124, IdProdaja = 24, IdArtikal = 101, Kolicina = 1, Cena = 100m },
                new ProdajaStavka { Id = 125, IdProdaja = 25, IdArtikal = 101, Kolicina = 1, Cena = 10_000m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/daily-analysis?analysisDate=2026-01-07&fromDate=2026-01-05&toDate=2026-01-07");

        Assert.Equal("present", root.GetProperty("targetDataStatus").GetString());
        Assert.True(root.GetProperty("isExtremeOutlier").GetBoolean());
        Assert.Equal(2, root.GetProperty("baselineSampleSize").GetInt32());
    }

    [Fact]
    public async Task CategoryIntelligence_ReportsMixedVelocityDenominatorAndNullableBenchmark()
    {
        await using var factory = CreateFactory();
        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/category-intelligence?fromDate=2026-01-05&toDate=2026-01-07");

        Assert.Equal(
            "period_sales_over_current_catalog_average_stock",
            root.GetProperty("velocityDenominatorBasis").GetString());
        Assert.True(root.GetProperty("systemBenchmarkAvailable").GetBoolean());
        var firstCategory = root.GetProperty("byCategory").EnumerateArray().First();
        Assert.Equal(
            "period_sales_over_current_catalog_average_stock",
            firstCategory.GetProperty("velocityDenominatorBasis").GetString());
    }

    [Fact]
    public async Task ReorderPlan_SeparatesPotentialRevenueFromProcurementCost()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.Artikli.Add(new Artikli
            {
                Id = 203,
                Naziv = "Reorder semantics probe",
                IDDobavljac = 1,
                IDObjekat = 1,
                Kolicina = 0,
                MinimalnaKolicina = 5,
                ProdajnaCena = 100m,
                NabavnaCena = 40m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            });
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 26,
                DatumProdaje = new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 126, IdProdaja = 26, IdArtikal = 203, Kolicina = 10, Cena = 100m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/reorder-plan?fromDate=2026-01-07&toDate=2026-01-07");
        var item = root.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("artikalId").GetInt32() == 203);
        var summary = root.GetProperty("summary");

        Assert.True(item.GetProperty("recommendedQty").GetInt32() > 0);
        Assert.True(item.GetProperty("potentialRevenueRsd").GetDecimal() > item.GetProperty("estimatedProcurementCostRsd").GetDecimal());
        Assert.Equal("potential_revenue_at_selling_price", summary.GetProperty("reorderValueBasis").GetString());
        Assert.Equal(summary.GetProperty("potentialRevenueRsd").GetDecimal(), summary.GetProperty("totalReorderValue").GetDecimal());
    }

    [Fact]
    public async Task ReorderPlan_MissingSellingPriceKeepsPotentialRevenueUnavailable()
    {
        await using var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            db.Artikli.Add(new Artikli
            {
                Id = 204,
                Naziv = "Reorder missing price probe",
                IDDobavljac = 1,
                IDObjekat = 1,
                Kolicina = 0,
                MinimalnaKolicina = 5,
                ProdajnaCena = null,
                NabavnaCena = 40m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            });
            db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = 27,
                DatumProdaje = new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });
            db.ProdajaStavke.Add(new ProdajaStavka { Id = 127, IdProdaja = 27, IdArtikal = 204, Kolicina = 10, Cena = 100m });
            db.SaveChanges();
        }

        var root = await GetJsonAsync(factory,
            "/api/analytics/advanced/reorder-plan?fromDate=2026-01-07&toDate=2026-01-07");
        var item = root.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("artikalId").GetInt32() == 204);
        var summary = root.GetProperty("summary");

        Assert.True(item.GetProperty("needsReorder").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("potentialRevenueRsd").ValueKind);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("potentialRevenueRsd").ValueKind);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("totalReorderValue").ValueKind);
        Assert.Equal("unavailable", summary.GetProperty("reorderValueBasis").GetString());
    }

    private static async Task<JsonElement> GetJsonAsync(WebApplicationFactory<global::Program> factory, string url)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<JsonElement[]> GetJsonArrayAsync(WebApplicationFactory<global::Program> factory, string url)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(body).RootElement.EnumerateArray().Select(x => x.Clone()).ToArray();
    }

    private static WebApplicationFactory<global::Program> CreateFactory()
    {
        var factory = new LegacyInsightStudioFactory();
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
                ProdajnaCena = 300m,
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
                ProdajnaCena = 250m,
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
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 11, IdProdaja = 1, IdArtikal = 101, Kolicina = 2, Cena = 100m },
            new ProdajaStavka { Id = 12, IdProdaja = 1, IdArtikal = 102, Kolicina = 1, Cena = 600m },
            new ProdajaStavka { Id = 13, IdProdaja = 2, IdArtikal = 101, Kolicina = 3, Cena = 100m });

        db.SaveChanges();
    }

    private sealed class LegacyInsightStudioFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = $"legacy-insight-studio-{Guid.NewGuid():N}";

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
