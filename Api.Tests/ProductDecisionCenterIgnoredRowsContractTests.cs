using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class ProductDecisionCenterIgnoredRowsContractTests
{
    [Fact]
    public void RowWindow_IgnoredCount_IsHiddenByTopLimit_NotBadData()
    {
        var window = CachedAnalyticsEndpoints.BuildProductDecisionCenterRowWindow(
            analyzedRowCount: 5,
            returnedRowCount: 2);

        Assert.Equal(3, window.IgnoredRowsCount);
        Assert.Equal(ProductDecisionDenominatorScope.HiddenByTopLimit, window.IgnoredRowsMeaning);
        Assert.NotEqual("invalid_data", window.IgnoredRowsMeaning, StringComparer.Ordinal);
        Assert.NotEqual("bad_data", window.IgnoredRowsMeaning, StringComparer.Ordinal);
    }

    [Fact]
    public void Summary_BadDataCount_And_IgnoredRows_UseDifferentDenominators()
    {
        var returnedRows = new[]
        {
            new ProductDecisionCenterRowDto
            {
                ProductId = 1,
                RecommendationStatus = "FIX_DATA",
                LostSalesEstimate = 100m,
                SlowStockCapital = 50m
            },
            new ProductDecisionCenterRowDto
            {
                ProductId = 2,
                RecommendationStatus = "REPLENISH",
                LostSalesEstimate = 200m,
                SlowStockCapital = 0m
            }
        };

        var summary = CachedAnalyticsEndpoints.BuildProductDecisionCenterSummary(
            returnedRows,
            analyzedLostSalesEstimate: 900m,
            analyzedSlowStockCapital: 300m);

        Assert.Equal(1, summary.BadDataCount);
        Assert.Equal(900m, summary.LostSalesEstimate);
        Assert.Equal(300m, summary.SlowStockCapital);
        Assert.Equal(ProductDecisionDenominatorScope.ReturnedRows, summary.CountDenominatorScope);
        Assert.Equal(ProductDecisionDenominatorScope.AnalyzedRows, summary.MoneyDenominatorScope);
    }
}

[Trait("Category", "Integration")]
public sealed class ProductDecisionCenterIgnoredRowsIntegrationTests
{
    [Fact]
    public async Task BuildProductDecisionCenter_TopLimitHiddenRows_AreNotBadDataCount()
    {
        var databaseName = $"pdc-ignored-contract-{Guid.NewGuid():N}";
        await using var db = CreateDbContext(databaseName);
        var toDate = DateTime.UtcNow.Date;
        var fromDate = toDate.AddDays(-29);
        await SeedThreeProductDecisionRowsAsync(db, fromDate, toDate);

        var response = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
            db,
            fromDate,
            toDate,
            storeId: 1,
            supplierId: null,
            top: 2,
            dataScope: "all",
            CancellationToken.None);

        Assert.Equal(3, response.AnalyzedRows);
        Assert.Equal(2, response.TotalRows);
        Assert.Equal(2, response.Rows.Count);
        Assert.Equal(1, response.IgnoredRowsCount);
        Assert.Equal(ProductDecisionDenominatorScope.HiddenByTopLimit, response.IgnoredRowsMeaning);

        Assert.Equal(1, response.Summary.BadDataCount);
        Assert.Equal(1, response.Rows.Count(row => row.RecommendationStatus == "FIX_DATA"));
        Assert.Equal(1, response.Rows.Count(row => row.RecommendationStatus != "FIX_DATA"));
        Assert.DoesNotContain(response.Rows, row => row.ProductId == 103);
        Assert.Equal(102, Assert.Single(response.Rows, row => row.RecommendationStatus == "FIX_DATA").ProductId);

        Assert.Equal(ProductDecisionDenominatorScope.ReturnedRows, response.Summary.CountDenominatorScope);
        Assert.Equal(ProductDecisionDenominatorScope.AnalyzedRows, response.Summary.MoneyDenominatorScope);
        Assert.True(response.Summary.LostSalesEstimate > 0m);
    }

    private static TrendplusDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new TrendplusDbContext(options);
    }

    private static async Task SeedThreeProductDecisionRowsAsync(
        TrendplusDbContext db,
        DateTime fromDate,
        DateTime toDate)
    {
        db.Dobavljaci.Add(new Dobavljac { Id = 1, Naziv = "Dobavljač", DataOrigin = "existing" });

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 101,
                PLU = "SKU-101",
                Naziv = "Dopuna A",
                IDDobavljac = 1,
                IDObjekat = 1,
                Kolicina = 0,
                MinimalnaKolicina = 5,
                NabavnaCena = 50m,
                Kategorija = "Patike",
                Boja = "Crna",
                Velicina = "42",
                DataOrigin = "existing",
                UpdatedAt = toDate
            },
            new Artikli
            {
                Id = 102,
                PLU = "SKU-102",
                Naziv = "Loši podaci",
                IDDobavljac = null,
                IDObjekat = 1,
                Kolicina = 1,
                MinimalnaKolicina = 3,
                NabavnaCena = null,
                DataOrigin = "existing",
                UpdatedAt = toDate
            },
            new Artikli
            {
                Id = 103,
                PLU = "SKU-103",
                Naziv = "Dopuna B",
                IDDobavljac = 1,
                IDObjekat = 1,
                Kolicina = 0,
                MinimalnaKolicina = 4,
                NabavnaCena = 40m,
                Kategorija = "Patike",
                Boja = "Bela",
                Velicina = "40",
                DataOrigin = "existing",
                UpdatedAt = toDate
            });

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje { Id = 1, DatumProdaje = toDate.AddHours(9), IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 2, DatumProdaje = toDate.AddHours(10), IDObjekat = 1, DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 3, DatumProdaje = toDate.AddHours(11), IDObjekat = 1, DataOrigin = "existing" });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 11, IdProdaja = 1, IdArtikal = 101, Kolicina = 20, Cena = 100m, NabavnaCena = 50m },
            new ProdajaStavka { Id = 12, IdProdaja = 2, IdArtikal = 102, Kolicina = 2, Cena = 200m, NabavnaCena = null },
            new ProdajaStavka { Id = 13, IdProdaja = 3, IdArtikal = 103, Kolicina = 15, Cena = 90m, NabavnaCena = 40m });

        await db.SaveChangesAsync();
    }
}
