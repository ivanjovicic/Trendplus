using Api.Services;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

public sealed class ObservedSalesHorizonResolverTests
{
    [Fact]
    public async Task ResolveAsync_UsesLatestIncludedSaleForStoreAndDataScope()
    {
        await using var db = CreateDb();
        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje { Id = 1, DatumProdaje = Day(5), IDObjekat = 7, DataOrigin = "access" },
            new ProdajaZaglavlje { Id = 2, DatumProdaje = Day(8), IDObjekat = 7, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 3, DatumProdaje = Day(12), IDObjekat = 8, DataOrigin = "access" });
        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 1, IdProdaja = 1, IdArtikal = 10 },
            new ProdajaStavka { Id = 2, IdProdaja = 2, IdArtikal = 11 },
            new ProdajaStavka { Id = 3, IdProdaja = 3, IdArtikal = 12 });
        await db.SaveChangesAsync();

        var horizon = await ObservedSalesHorizonResolver.ResolveAsync(db, 7, null, "imported", CancellationToken.None);

        Assert.Equal(Day(5), horizon);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNullWhenScopedSalesDoNotExist()
    {
        await using var db = CreateDb();
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 1,
            DatumProdaje = Day(5),
            IDObjekat = 7,
            DataOrigin = "access"
        });
        db.ProdajaStavke.Add(new ProdajaStavka { Id = 1, IdProdaja = 1, IdArtikal = 10 });
        await db.SaveChangesAsync();

        var horizon = await ObservedSalesHorizonResolver.ResolveAsync(db, 99, null, "imported", CancellationToken.None);

        Assert.Null(horizon);
    }

    [Fact]
    public async Task ResolveDefaultPeriodAsync_UsesThirtyCalendarDaysEndingAtObservedSale()
    {
        await using var db = CreateDb();
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje { Id = 1, DatumProdaje = Day(5), IDObjekat = 7, DataOrigin = "access" });
        db.ProdajaStavke.Add(new ProdajaStavka { Id = 1, IdProdaja = 1, IdArtikal = 10 });
        await db.SaveChangesAsync();

        var period = await ObservedSalesHorizonResolver.ResolveDefaultPeriodAsync(
            db, 7, null, "imported", CancellationToken.None);

        Assert.NotNull(period);
        Assert.Equal(new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc), period.FromUtc);
        Assert.Equal(new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc), period.ToUtc);
        Assert.Equal(Day(5), period.HorizonUtc);
    }

    private static TrendplusDbContext CreateDb() => new(
        new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase($"observed-horizon-{Guid.NewGuid():N}")
            .Options);

    private static DateTime Day(int day) => new(2026, 8, day, 0, 0, 0, DateTimeKind.Utc);
}
