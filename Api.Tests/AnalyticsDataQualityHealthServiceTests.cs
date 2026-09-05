using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class AnalyticsDataQualityHealthServiceTests
{
    [Fact]
    public async Task CaptureAsync_ComputesRevenueWeightedQualityImpactExactly()
    {
        await using var db = CreateContext();
        await SeedMixedQualityDataAsync(db);
        var service = new AnalyticsDataQualityHealthService(db);

        var snapshot = await service.CaptureAsync(lookbackDays: 30, dataScope: "all", CancellationToken.None);

        Assert.Equal(1, snapshot.OrphanArticleCount);
        Assert.Equal(1_000m, snapshot.TotalRevenue);
        Assert.Equal(300m, snapshot.MissingCostRevenue);
        Assert.Equal(30d, snapshot.MissingCostRevenueSharePct);
        Assert.Equal(800m, snapshot.UnknownSupplierRevenue);
        Assert.Equal(80d, snapshot.UnknownSupplierRevenueSharePct);
        Assert.Equal(30, snapshot.LookbackDays);
        Assert.True(snapshot.WindowFromUtc <= snapshot.WindowToUtc);
        Assert.InRange(snapshot.GeneratedAtUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task CaptureAsync_ExistingScopeExcludesImportedProblems()
    {
        await using var db = CreateContext();
        await SeedMixedQualityDataAsync(db);
        var service = new AnalyticsDataQualityHealthService(db);

        var snapshot = await service.CaptureAsync(30, " existing ", CancellationToken.None);

        Assert.Equal(0, snapshot.OrphanArticleCount);
        Assert.Equal(500m, snapshot.TotalRevenue);
        Assert.Equal(300m, snapshot.MissingCostRevenue);
        Assert.Equal(60d, snapshot.MissingCostRevenueSharePct);
        Assert.Equal(300m, snapshot.UnknownSupplierRevenue);
        Assert.Equal(60d, snapshot.UnknownSupplierRevenueSharePct);
    }

    [Fact]
    public async Task CaptureAsync_ImportedScopeIsolatedFromExistingData()
    {
        await using var db = CreateContext();
        await SeedMixedQualityDataAsync(db);
        var service = new AnalyticsDataQualityHealthService(db);

        var snapshot = await service.CaptureAsync(30, "IMPORTED", CancellationToken.None);

        Assert.Equal(1, snapshot.OrphanArticleCount);
        Assert.Equal(500m, snapshot.TotalRevenue);
        Assert.Equal(0m, snapshot.MissingCostRevenue);
        Assert.Equal(0d, snapshot.MissingCostRevenueSharePct);
        Assert.Equal(500m, snapshot.UnknownSupplierRevenue);
        Assert.Equal(100d, snapshot.UnknownSupplierRevenueSharePct);
    }

    [Fact]
    public async Task CaptureAsync_UnknownScopeFallsBackToAllData()
    {
        await using var db = CreateContext();
        await SeedMixedQualityDataAsync(db);
        var service = new AnalyticsDataQualityHealthService(db);

        var snapshot = await service.CaptureAsync(30, "unsupported-scope", CancellationToken.None);

        Assert.Equal(1_000m, snapshot.TotalRevenue);
        Assert.Equal(1, snapshot.OrphanArticleCount);
    }

    [Fact]
    public async Task CaptureAsync_ClampsLookbackToOneDayAndExcludesOldSales()
    {
        await using var db = CreateContext();
        db.Artikli.Add(new Artikli
        {
            Id = 1,
            Naziv = "Artikal bez cene",
            Kolicina = 1,
            NabavnaCena = null,
            DataOrigin = "existing",
            UpdatedAt = DateTime.UtcNow
        });
        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 1,
                DatumProdaje = DateTime.UtcNow.Date.AddHours(10),
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 2,
                DatumProdaje = DateTime.UtcNow.Date.AddDays(-2).AddHours(10),
                DataOrigin = "existing"
            });
        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 1, IdProdaja = 1, IdArtikal = 1, Kolicina = 1, Cena = 100m },
            new ProdajaStavka { Id = 2, IdProdaja = 2, IdArtikal = 1, Kolicina = 1, Cena = 900m });
        await db.SaveChangesAsync();

        var service = new AnalyticsDataQualityHealthService(db);
        var snapshot = await service.CaptureAsync(lookbackDays: 0, dataScope: "all", CancellationToken.None);

        Assert.Equal(1, snapshot.LookbackDays);
        Assert.Equal(100m, snapshot.TotalRevenue);
        Assert.Equal(100m, snapshot.MissingCostRevenue);
        Assert.Equal(100d, snapshot.MissingCostRevenueSharePct);
    }

    [Fact]
    public async Task CaptureAsync_NoSalesReturnsUnknownSharesInsteadOfFakeZeroOrNaN()
    {
        await using var db = CreateContext();
        db.Artikli.Add(new Artikli
        {
            Id = 1,
            Naziv = "Artikal bez prodaje",
            Kolicina = 5,
            NabavnaCena = null,
            DataOrigin = "existing",
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new AnalyticsDataQualityHealthService(db);
        var snapshot = await service.CaptureAsync(30, "all", CancellationToken.None);

        Assert.Equal(0m, snapshot.TotalRevenue);
        Assert.False(snapshot.HasRevenueEvidence);
        Assert.Equal(0m, snapshot.MissingCostRevenue);
        Assert.Equal(0m, snapshot.UnknownSupplierRevenue);
        Assert.Null(snapshot.MissingCostRevenueSharePct);
        Assert.Null(snapshot.UnknownSupplierRevenueSharePct);
    }

    [Fact]
    public async Task CaptureAsync_WithRevenue_SetsHasRevenueEvidence()
    {
        await using var db = CreateContext();
        await SeedMixedQualityDataAsync(db);
        var service = new AnalyticsDataQualityHealthService(db);

        var snapshot = await service.CaptureAsync(30, "all", CancellationToken.None);

        Assert.True(snapshot.TotalRevenue > 0m);
        Assert.True(snapshot.HasRevenueEvidence);
    }

    private static TrendplusDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase($"analytics-data-quality-health-{Guid.NewGuid():N}")
            .Options;
        return new TrendplusDbContext(options);
    }

    private static async Task SeedMixedQualityDataAsync(TrendplusDbContext db)
    {
        db.Dobavljaci.Add(new Dobavljac
        {
            Id = 1,
            Naziv = "Pouzdan dobavljač",
            DataOrigin = "existing"
        });

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 1,
                Naziv = "Ispravan artikal",
                IDDobavljac = 1,
                Kolicina = 10,
                NabavnaCena = 50m,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 2,
                Naziv = "Bez dobavljača i cene",
                IDDobavljac = null,
                Kolicina = 3,
                NabavnaCena = null,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 3,
                Naziv = "Orphan dobavljač",
                IDDobavljac = 999,
                Kolicina = 2,
                NabavnaCena = 20m,
                DataOrigin = "access",
                UpdatedAt = DateTime.UtcNow
            });

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 1,
                DatumProdaje = DateTime.UtcNow.Date.AddHours(9),
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 2,
                DatumProdaje = DateTime.UtcNow.Date.AddHours(10),
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 3,
                DatumProdaje = DateTime.UtcNow.Date.AddHours(11),
                DataOrigin = "access"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka
            {
                Id = 1,
                IdProdaja = 1,
                IdArtikal = 1,
                Kolicina = 2,
                Cena = 100m,
                NabavnaCena = 50m
            },
            new ProdajaStavka
            {
                Id = 2,
                IdProdaja = 2,
                IdArtikal = 2,
                Kolicina = 1,
                Cena = 300m,
                NabavnaCena = null
            },
            new ProdajaStavka
            {
                Id = 3,
                IdProdaja = 3,
                IdArtikal = 3,
                Kolicina = 1,
                Cena = 500m,
                NabavnaCena = 20m
            });

        await db.SaveChangesAsync();
    }
}
