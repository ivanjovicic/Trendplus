using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class LostSalesValidationScopePostgresIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public LostSalesValidationScopePostgresIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BuildLostSalesValidation_UsesRequestDataScopeInFallbackPath()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        await SeedScopeSplitDatasetAsync(db);

        var imported = await CachedAnalyticsEndpoints.BuildLostSalesValidationAsync(db, CancellationToken.None, "imported");
        var existing = await CachedAnalyticsEndpoints.BuildLostSalesValidationAsync(db, CancellationToken.None, "existing");

        Assert.Equal("warning", imported.Status);
        Assert.Equal("warning", existing.Status);
        Assert.Equal(LostSalesSourceStatus.Fallback, imported.SourceStatus);
        Assert.Equal(LostSalesSourceStatus.Fallback, existing.SourceStatus);
        Assert.Equal(1, imported.AffectedSku);
        Assert.Equal(1, existing.AffectedSku);
        Assert.Equal(300m, imported.LostSalesEstimate);
        Assert.Equal(200m, existing.LostSalesEstimate);
        Assert.NotEqual(imported.LostSalesEstimate, existing.LostSalesEstimate);
    }

    [Fact]
    public async Task BuildAdvancedDashboard_UsesSnakeCaseSalesOriginColumnForScopedFallbacks()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        await SeedScopeSplitDatasetAsync(db);
        var fromUtc = DateTime.UtcNow.Date.AddDays(-5);
        var toUtc = DateTime.UtcNow.Date.AddDays(1);

        var snapshot = await CachedAnalyticsEndpoints.BuildAdvancedDashboardSnapshotAsync(
            db,
            fromUtc,
            toUtc,
            storeId: 1,
            supplierId: null,
            ct: CancellationToken.None,
            dataScope: "imported");

        var velocity = Assert.Single(snapshot.Cards, card => card.Key == "velocity");
        Assert.Equal(3m, velocity.Value);
        var pareto = Assert.Single(snapshot.Cards, card => card.Key == "pareto");
        Assert.Equal(100m, pareto.Value);
    }

    private async Task<TrendplusDbContext?> CreateDatabaseAsync()
    {
        var databaseName = $"lost-sales-scope-{Guid.NewGuid():N}";
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(databaseName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var db = new TrendplusDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task SeedScopeSplitDatasetAsync(TrendplusDbContext db)
    {
        db.Artikli.AddRange(
            new Artikli
            {
                Id = 501,
                Naziv = "LostSales Imported",
                PLU = "LS-IMP-501",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 0,
                MinimalnaKolicina = 2,
                NabavnaCena = 50m,
                Kategorija = "Patike",
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "access"
            },
            new Artikli
            {
                Id = 502,
                Naziv = "LostSales Existing",
                PLU = "LS-EX-502",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 0,
                MinimalnaKolicina = 2,
                NabavnaCena = 50m,
                Kategorija = "Patike",
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "existing"
            });

        var recentImported = DateTime.UtcNow.AddDays(-3);
        var recentExisting = DateTime.UtcNow.AddDays(-2);

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 601,
                DatumProdaje = recentImported,
                IDObjekat = 1,
                DataOrigin = "access"
            },
            new ProdajaZaglavlje
            {
                Id = 602,
                DatumProdaje = recentExisting,
                IDObjekat = 1,
                DataOrigin = "existing"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka
            {
                Id = 701,
                IdProdaja = 601,
                IdArtikal = 501,
                Kolicina = 3,
                Cena = 100m
            },
            new ProdajaStavka
            {
                Id = 702,
                IdProdaja = 602,
                IdArtikal = 502,
                Kolicina = 4,
                Cena = 50m
            });

        await db.SaveChangesAsync();
    }
}
