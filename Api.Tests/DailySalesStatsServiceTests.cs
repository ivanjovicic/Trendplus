using Api.Services;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class DailySalesStatsServiceTests
{
    [Fact]
    public async Task GetDailySalesAsync_IncludesZeroDays_AndReconcilesTotals()
    {
        await using var db = CreateDbContext();
        SeedSuppliersAndArticles(db);

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 1,
                DatumProdaje = new DateTime(2026, 1, 1, 7, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 2,
                DatumProdaje = new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 3,
                DatumProdaje = new DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 4,
                DatumProdaje = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 11, IdProdaja = 1, IdArtikal = 101, Kolicina = 5, Cena = 100m },
            new ProdajaStavka { Id = 12, IdProdaja = 2, IdArtikal = 102, Kolicina = 3, Cena = 200m },
            new ProdajaStavka { Id = 13, IdProdaja = 3, IdArtikal = 101, Kolicina = 2, Cena = 100m },
            new ProdajaStavka { Id = 14, IdProdaja = 4, IdArtikal = 103, Kolicina = 4, Cena = 150m });

        await db.SaveChangesAsync();

        var service = new DailySalesStatsService(db, NullLogger<DailySalesStatsService>.Instance);
        var result = await service.GetDailySalesAsync(
            requestedFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            requestedToUtc: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            storeId: 1,
            topN: 2,
            dataScope: "all",
            ct: CancellationToken.None);

        Assert.Equal(3, result.DateRows.Count);
        Assert.Equal(2, result.TopSuppliersOrder.Count);

        var dayOne = Assert.Single(result.DateRows, x => x.Date.Date == new DateTime(2026, 1, 1).Date);
        Assert.Equal(5, dayOne.FirstShiftTotalItems);
        Assert.Equal(3, dayOne.SecondShiftTotalItems);
        Assert.Equal(1100m, dayOne.TotalRevenue);
        Assert.Equal(8, dayOne.TotalItemsSold);
        Assert.Equal(dayOne.TotalItemsSold, dayOne.OthersCount + dayOne.TopSupplierCounts.Sum());

        var dayTwo = Assert.Single(result.DateRows, x => x.Date.Date == new DateTime(2026, 1, 2).Date);
        Assert.Equal(4, dayTwo.FirstShiftTotalItems);
        Assert.Equal(0, dayTwo.SecondShiftTotalItems);
        Assert.Equal(4, dayTwo.TotalItemsSold);
        Assert.Equal(dayTwo.TotalItemsSold, dayTwo.OthersCount + dayTwo.TopSupplierCounts.Sum());

        var dayThree = Assert.Single(result.DateRows, x => x.Date.Date == new DateTime(2026, 1, 3).Date);
        Assert.Equal(0, dayThree.TotalItemsSold);
        Assert.Equal(0m, dayThree.TotalRevenue);

        Assert.Equal(2, result.Metadata.OffShiftItems);
        Assert.True(result.Metadata.UnknownSupplierPct > 30m);
        Assert.Contains(result.Metadata.Warnings, x => x.Contains("van smena", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetDailySalesAsync_RespectsDataScopeImported()
    {
        await using var db = CreateDbContext();
        SeedSuppliersAndArticles(db);

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 20,
                DatumProdaje = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 21,
                DatumProdaje = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 30, IdProdaja = 20, IdArtikal = 101, Kolicina = 2, Cena = 100m }, // existing artikal
            new ProdajaStavka { Id = 31, IdProdaja = 21, IdArtikal = 104, Kolicina = 7, Cena = 50m }); // imported artikal

        await db.SaveChangesAsync();

        var service = new DailySalesStatsService(db, NullLogger<DailySalesStatsService>.Instance);
        var imported = await service.GetDailySalesAsync(
            requestedFromUtc: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            requestedToUtc: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            storeId: 1,
            topN: 3,
            dataScope: "imported",
            ct: CancellationToken.None);

        var row = Assert.Single(imported.DateRows);
        Assert.Equal(7, row.TotalItemsSold);
        Assert.Equal(350m, row.TotalRevenue);
        Assert.Equal("imported", imported.DataScope);
    }

    [Fact]
    public async Task GetDailySalesAsync_WhenTimestampsAreMidnight_MapsRowsToFirstShiftWithWarning()
    {
        await using var db = CreateDbContext();
        SeedSuppliersAndArticles(db);

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 40,
                DatumProdaje = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 41,
                DatumProdaje = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 50, IdProdaja = 40, IdArtikal = 101, Kolicina = 6, Cena = 100m },
            new ProdajaStavka { Id = 51, IdProdaja = 41, IdArtikal = 102, Kolicina = 4, Cena = 50m });

        await db.SaveChangesAsync();

        var service = new DailySalesStatsService(db, NullLogger<DailySalesStatsService>.Instance);
        var result = await service.GetDailySalesAsync(
            requestedFromUtc: new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            requestedToUtc: new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            storeId: 1,
            topN: 5,
            dataScope: "all",
            ct: CancellationToken.None);

        var row = Assert.Single(result.DateRows);
        Assert.Equal(10, row.FirstShiftTotalItems);
        Assert.Equal(0, row.SecondShiftTotalItems);
        Assert.Equal(10, row.TotalItemsSold);
        Assert.Equal(0, result.Metadata.OffShiftItems);
        Assert.Contains(result.Metadata.Warnings, x => x.Contains("Satnica prodaje nije dostupna", StringComparison.OrdinalIgnoreCase));
    }

    private static TrendplusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TrendplusDbContext(options);
    }

    private static void SeedSuppliersAndArticles(TrendplusDbContext db)
    {
        db.Dobavljaci.AddRange(
            new Dobavljac { Id = 1, Naziv = "Dobavljac A", DataOrigin = "existing" },
            new Dobavljac { Id = 2, Naziv = "Dobavljac B", DataOrigin = "existing" },
            new Dobavljac { Id = 3, Naziv = "Dobavljac C", DataOrigin = "existing" });

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 101,
                Naziv = "A1",
                IDDobavljac = 1,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 102,
                Naziv = "B1",
                IDDobavljac = 2,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 103,
                Naziv = "Unknown",
                IDDobavljac = null,
                DataOrigin = "existing",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 104,
                Naziv = "Imported",
                IDDobavljac = 3,
                DataOrigin = "access",
                UpdatedAt = DateTime.UtcNow
            });
    }
}
