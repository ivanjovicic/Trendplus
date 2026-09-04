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
        Assert.Equal(7, dayOne.FirstShiftTotalItems);
        Assert.Equal(3, dayOne.SecondShiftTotalItems);
        Assert.Equal(1300m, dayOne.TotalRevenue);
        Assert.Equal(10, dayOne.TotalItemsSold);
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
        Assert.True(result.Metadata.UnknownSupplierPct.HasValue);
        Assert.True(result.Metadata.UnknownSupplierPct.Value > 20m);
        Assert.Contains(result.Metadata.Warnings, x => x.Contains("van smena", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("warning", result.Meta.DataQualityStatus);
        Assert.True(result.Meta.IsPartial);
        Assert.Equal("DAILY_SALES_WARNINGS", result.Meta.WarningCode);
        Assert.Null(result.Meta.LastRefreshAtUtc);
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
        Assert.Equal("warning", imported.Meta.DataQualityStatus);
        Assert.True(imported.Meta.IsPartial);
        Assert.Null(imported.Meta.LastRefreshAtUtc);
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

    [Fact]
    public async Task GetDailySalesAsync_WhenAllHoursAreOffShiftButNotMidnight_MapsRowsToFirstShift()
    {
        await using var db = CreateDbContext();
        SeedSuppliersAndArticles(db);

        // Simulate production scenario where DatumProdaje = DateTime.UtcNow at 2 AM UTC
        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 60,
                DatumProdaje = new DateTime(2026, 3, 10, 2, 47, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "access"
            },
            new ProdajaZaglavlje
            {
                Id = 61,
                DatumProdaje = new DateTime(2026, 3, 11, 3, 15, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "access"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 70, IdProdaja = 60, IdArtikal = 101, Kolicina = 5, Cena = 100m },
            new ProdajaStavka { Id = 71, IdProdaja = 61, IdArtikal = 102, Kolicina = 3, Cena = 50m });

        await db.SaveChangesAsync();

        var service = new DailySalesStatsService(db, NullLogger<DailySalesStatsService>.Instance);
        var result = await service.GetDailySalesAsync(
            requestedFromUtc: new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            requestedToUtc: new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc),
            storeId: 1,
            topN: 5,
            dataScope: "all",
            ct: CancellationToken.None);

        // Both days should have data mapped to first shift
        Assert.Equal(2, result.DateRows.Count);
        Assert.Equal(8, result.DateRows.Sum(r => r.FirstShiftTotalItems));
        Assert.Equal(0, result.Metadata.OffShiftItems);
        Assert.Contains(result.Metadata.Warnings, x => x.Contains("Satnica prodaje nije dostupna", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetDailySalesAsync_WhenNoDataInRange_ReturnsEmptyTrustMeta()
    {
        await using var db = CreateDbContext();

        var service = new DailySalesStatsService(db, NullLogger<DailySalesStatsService>.Instance);
        var result = await service.GetDailySalesAsync(
            requestedFromUtc: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            requestedToUtc: new DateTime(2026, 4, 7, 0, 0, 0, DateTimeKind.Utc),
            storeId: 1,
            topN: 5,
            dataScope: "all",
            ct: CancellationToken.None);

        Assert.Equal(7, result.DateRows.Count);
        Assert.All(result.DateRows, row =>
        {
            Assert.Equal(0, row.TotalItemsSold);
            Assert.Equal(0m, row.TotalRevenue);
        });
        Assert.Equal("insufficient_data", result.Meta.DataQualityStatus);
        Assert.Equal("no_data_in_period", result.Meta.EmptyReason);
        Assert.Null(result.Metadata.UnknownSupplierPct);
        Assert.Null(result.Meta.LastRefreshAtUtc);
        Assert.Null(result.Meta.WarningCode);
        Assert.False(result.Meta.IsPartial);
    }

    [Fact]
    public async Task GetDailySalesAsync_CountsRepeatedReceiptLines_WithoutFalseDuplicateWarning()
    {
        await using var db = CreateDbContext();
        SeedSuppliersAndArticles(db);

        db.ProdajaZaglavlja.Add(
            new ProdajaZaglavlje
            {
                Id = 80,
                BrojRacuna = "312",
                DatumProdaje = new DateTime(2026, 3, 26, 9, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "access"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 81, IdProdaja = 80, IdArtikal = 101, Kolicina = 1, Cena = 100m },
            new ProdajaStavka { Id = 82, IdProdaja = 80, IdArtikal = 101, Kolicina = 1, Cena = 100m },
            new ProdajaStavka { Id = 83, IdProdaja = 80, IdArtikal = 101, Kolicina = 1, Cena = 100m });

        await db.SaveChangesAsync();

        var service = new DailySalesStatsService(db, NullLogger<DailySalesStatsService>.Instance);
        var result = await service.GetDailySalesAsync(
            requestedFromUtc: new DateTime(2026, 3, 26, 0, 0, 0, DateTimeKind.Utc),
            requestedToUtc: new DateTime(2026, 3, 26, 0, 0, 0, DateTimeKind.Utc),
            storeId: 1,
            topN: 5,
            dataScope: "all",
            ct: CancellationToken.None);

        var row = Assert.Single(result.DateRows);
        Assert.Equal(3, row.TotalItemsSold);
        Assert.Equal(3, row.FirstShiftTotalItems);
        Assert.Equal(300m, row.TotalRevenue);
        Assert.Equal(0, result.Metadata.NonStandardReceiptCount);
        Assert.Equal(0, result.Metadata.DebtReceiptCount);
        Assert.DoesNotContain(result.Metadata.Warnings, x => x.Contains("dupliranih stavki prodaje", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetDailySalesAsync_ExcludesDugAndKorekcijaFromDailyTotals()
    {
        await using var db = CreateDbContext();
        SeedSuppliersAndArticles(db);

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 90,
                BrojRacuna = "313",
                DatumProdaje = new DateTime(2026, 3, 23, 9, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "access"
            },
            new ProdajaZaglavlje
            {
                Id = 91,
                BrojRacuna = "DUG",
                DatumProdaje = new DateTime(2026, 3, 23, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "access"
            },
            new ProdajaZaglavlje
            {
                Id = 92,
                BrojRacuna = "korekcija",
                DatumProdaje = new DateTime(2026, 3, 23, 11, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "access"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 90, IdProdaja = 90, IdArtikal = 101, Kolicina = 2, Cena = 100m },
            new ProdajaStavka { Id = 91, IdProdaja = 91, IdArtikal = 101, Kolicina = 3, Cena = 50m },
            new ProdajaStavka { Id = 92, IdProdaja = 92, IdArtikal = 102, Kolicina = 1, Cena = 250m });

        await db.SaveChangesAsync();

        var service = new DailySalesStatsService(db, NullLogger<DailySalesStatsService>.Instance);
        var result = await service.GetDailySalesAsync(
            requestedFromUtc: new DateTime(2026, 3, 23, 0, 0, 0, DateTimeKind.Utc),
            requestedToUtc: new DateTime(2026, 3, 23, 0, 0, 0, DateTimeKind.Utc),
            storeId: 1,
            topN: 5,
            dataScope: "all",
            ct: CancellationToken.None);

        var row = Assert.Single(result.DateRows);
        Assert.Equal(2, row.TotalItemsSold);
        Assert.Equal(2, row.FirstShiftTotalItems);
        Assert.Equal(200m, row.TotalRevenue);
        Assert.Equal(1, result.Metadata.DebtReceiptCount);
        Assert.Equal(150m, result.Metadata.DebtReceiptRevenue);
        Assert.Contains(result.Metadata.Warnings, x => x.Contains("iskljucena", StringComparison.OrdinalIgnoreCase));
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
