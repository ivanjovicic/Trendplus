using Application.Analytics;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Proves the shared RQ456 EF predicate used by Daily/Supplier/Shoe/Color and detail paths.
/// </summary>
public sealed class RetailSalesReceiptPopulationQueryTests
{
    [Fact]
    public async Task EfExclusionPredicate_DropsMixedCaseAndWhitespaceDugKorekcija_KeepsSignedReturns()
    {
        await using var db = CreateDb();

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 1,
                BrojRacuna = "1001",
                DatumProdaje = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 2,
                BrojRacuna = " Dug ",
                DatumProdaje = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 3,
                BrojRacuna = " KoReKcIjA ",
                DatumProdaje = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "access"
            },
            new ProdajaZaglavlje
            {
                Id = 4,
                BrojRacuna = "1002",
                DatumProdaje = new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 11, IdProdaja = 1, IdArtikal = 1, Kolicina = 2, Cena = 100m },
            new ProdajaStavka { Id = 12, IdProdaja = 2, IdArtikal = 1, Kolicina = 5, Cena = 100m },
            new ProdajaStavka { Id = 13, IdProdaja = 3, IdArtikal = 1, Kolicina = 1, Cena = 400m },
            // Signed retail return remains in the retail population.
            new ProdajaStavka { Id = 14, IdProdaja = 4, IdArtikal = 1, Kolicina = -1, Cena = 50m });

        await db.SaveChangesAsync();

        var excluded = RetailSalesReceiptPopulation.ExcludedCanonicalReceiptNumbers;
        var included = await (
            from ps in db.ProdajaStavke.AsNoTracking()
            join pz in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            where !excluded.Contains((pz.BrojRacuna ?? string.Empty).Trim().ToUpper())
            select new { ps.Kolicina, Revenue = ps.Kolicina * ps.Cena })
            .ToListAsync();

        Assert.Equal(2, included.Count);
        Assert.Equal(1, included.Sum(x => x.Kolicina));
        Assert.Equal(150m, included.Sum(x => x.Revenue));

        var excludedRows = await (
            from ps in db.ProdajaStavke.AsNoTracking()
            join pz in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            where excluded.Contains((pz.BrojRacuna ?? string.Empty).Trim().ToUpper())
            select ps.Id)
            .ToListAsync();

        Assert.Equal(2, excludedRows.Count);
    }

    private static TrendplusDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase($"retail-receipt-population-{Guid.NewGuid():N}")
            .Options;
        return new TrendplusDbContext(options);
    }
}
