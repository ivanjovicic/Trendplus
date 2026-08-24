using System.Linq;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

public sealed class PilotAnalyticsSeedPackTests
{
    [Fact]
    public void SeedPack_DeclaresCanonicalFamiliesAndBases()
    {
        Assert.Equal("pilot-analytics-proof-pack-v1", PilotAnalyticsSeedPack.PackId);
        Assert.Equal(7, PilotAnalyticsSeedPack.Families.Count);
        Assert.Equal(
            new[]
            {
                "dashboard",
                "product-decision-center",
                "supplier-decision-sales",
                "inventory",
                "analytics-actions",
                "decision-board",
                "pilot-intake-readiness"
            },
            PilotAnalyticsSeedPack.Families.Select(family => family.Family).ToArray());
        Assert.All(PilotAnalyticsSeedPack.Families, family =>
        {
            Assert.False(string.IsNullOrWhiteSpace(family.CanonicalBasis));
            Assert.NotEmpty(family.ExpectedOutputs);
            Assert.NotEmpty(family.AllowedStates);
            Assert.NotEmpty(family.ProofFiles);
        });
    }

    [Fact]
    public async Task SeedPack_SeedsProductDecisionAndInventoryDeterministically()
    {
        await using var pdcDb = CreateDb("pilot-pack-product-decision");
        PilotAnalyticsSeedPack.SeedProductDecisionCenter(
            pdcDb,
            PilotAnalyticsSeedPack.ProductDecisionFromUtc,
            PilotAnalyticsSeedPack.ProductDecisionToUtc);
        await pdcDb.SaveChangesAsync();

        var replenish = await pdcDb.Artikli.SingleAsync(article => article.Id == 101);
        var fixData = await pdcDb.Artikli.SingleAsync(article => article.Id == 102);

        Assert.Equal("SKU-101", replenish.PLU);
        Assert.Equal("Model za dopunu", replenish.Naziv);
        Assert.Equal(0, replenish.Kolicina);
        Assert.Equal(5, replenish.MinimalnaKolicina);

        Assert.Equal("SKU-102", fixData.PLU);
        Assert.Null(fixData.IDDobavljac);
        Assert.Null(fixData.NabavnaCena);

        Assert.Equal(3, await pdcDb.ProdajaZaglavlja.CountAsync());
        Assert.Equal(3, await pdcDb.ProdajaStavke.CountAsync());

        await using var inventoryDb = CreateDb("pilot-pack-inventory");
        PilotAnalyticsSeedPack.SeedInventory(inventoryDb);
        await inventoryDb.SaveChangesAsync();

        var oos = await inventoryDb.Artikli.SingleAsync(article => article.Id == 101);
        var empty = await inventoryDb.Artikli.SingleAsync(article => article.Id == 104);

        Assert.Equal("OOS-101", oos.PLU);
        Assert.Equal("EMPTY-104", empty.PLU);
        Assert.Equal(2, await inventoryDb.Dobavljaci.CountAsync());
        Assert.Equal(4, await inventoryDb.Artikli.CountAsync());
        Assert.Equal(2, await inventoryDb.ProdajaZaglavlja.CountAsync());
        Assert.Equal(3, await inventoryDb.ProdajaStavke.CountAsync());
        Assert.Equal(2, await inventoryDb.DnevnikPromena.CountAsync());
    }

    private static TrendplusDbContext CreateDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new TrendplusDbContext(options);
    }
}
