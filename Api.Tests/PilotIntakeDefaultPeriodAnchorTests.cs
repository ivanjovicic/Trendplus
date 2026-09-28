using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Trendplus2.Endpoints;

namespace Api.Tests;

public sealed class PilotIntakeDefaultPeriodAnchorTests
{
    [Fact]
    public async Task MissingScopedSales_UsesActualImportedBusinessDate_ForFallbackProvenance()
    {
        await using var db = new TrendplusDbContext(
            new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseInMemoryDatabase($"pilot-intake-anchor-{Guid.NewGuid():N}")
                .Options);

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 1,
                BrojRacuna = "EXISTING-1",
                DatumProdaje = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 2,
                BrojRacuna = "IMPORT-1",
                DatumProdaje = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                IDObjekat = 1,
                DataOrigin = "access"
            });
        await db.SaveChangesAsync();

        var anchor = await DataQualityEndpoints.ResolvePilotDefaultPeriodAnchorAsync(
            db,
            storeId: 99,
            supplierId: null,
            dataScope: "existing",
            CancellationToken.None);

        Assert.Equal(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), anchor.Date);
        Assert.Equal("import_business_date_fallback", anchor.Code);
        Assert.Contains("uvezenom skupu podataka", anchor.Message, StringComparison.OrdinalIgnoreCase);
    }
}
