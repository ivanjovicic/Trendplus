using Api.Services;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Api.Tests;

public sealed class DnevnikPromenaReadServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ReturnsTrendplusDetail_WhenRecordExists()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId();
        var artikalId = uniqueId;
        var movementId = uniqueId + 1;

        try
        {
            trendDb.Artikli.Add(new Artikli
            {
                Id = artikalId,
                Naziv = "Test Artikal Detail",
                ProdajnaCena = 1450m,
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "existing"
            });

            trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = movementId,
                TipPromene = "Nivelacija",
                Datum = DateTime.UtcNow,
                ArtikalId = artikalId,
                Kolicina = 3,
                StaraProdajnaCena = 1200m,
                NovaProdajnaCena = 1450m,
                Iznos = 4350m,
                BrojRacuna = "DET-001",
                KorisnikIme = "tester",
                Komentar = "Detalj test",
                DataOrigin = "existing"
            });

            await trendDb.SaveChangesAsync();

            var service = new DnevnikPromenaReadService(trendDb, analyticsDb, NullLogger<DnevnikPromenaReadService>.Instance);
            var detail = await service.GetByIdAsync(movementId);

            Assert.NotNull(detail);
            Assert.Equal(movementId, detail!.Id);
            Assert.Equal(movementId, detail.SourceId);
            Assert.Equal("Test Artikal Detail", detail.NazivArtikla);
            Assert.Equal(3, detail.Kolicina);
            Assert.Equal(1200m, detail.StaraCena);
            Assert.Equal(1450m, detail.NovaCena);
            Assert.Equal("DET-001", detail.BrojRacuna);
        }
        finally
        {
            await trendDb.DnevnikPromena.Where(x => x.Id == movementId).ExecuteDeleteAsync();
            await trendDb.Artikli.Where(x => x.Id == artikalId).ExecuteDeleteAsync();
            await DisposeAsync(trendDb, analyticsDb);
        }
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsProjectedItems_FromTrendplus()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId() + 2_000;
        var artikalId = uniqueId;
        var movementId = uniqueId + 1;

        try
        {
            trendDb.Artikli.Add(new Artikli
            {
                Id = artikalId,
                Naziv = "Paged Test Artikal",
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "existing"
            });

            trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = movementId,
                TipPromene = "Prodaja",
                Datum = DateTime.UtcNow,
                ArtikalId = artikalId,
                Iznos = 2500m,
                BrojRacuna = "PAGE-001",
                KorisnikIme = "paged-user",
                DataOrigin = "existing"
            });

            await trendDb.SaveChangesAsync();

            var service = new DnevnikPromenaReadService(trendDb, analyticsDb, NullLogger<DnevnikPromenaReadService>.Instance);
            var result = await service.GetPagedAsync(new Api.Models.DnevnikPromenaListQuery
            {
                PageNumber = 1,
                PageSize = 10,
                BrojRacuna = "PAGE-001",
                SortBy = "datum",
                SortDir = "desc",
                DataScope = "all"
            });

            var item = Assert.Single(result.Items);
            Assert.Equal(movementId, item.Id);
            Assert.Equal("Paged Test Artikal", item.ArtikalNaziv);
            Assert.Equal("PAGE-001", item.BrojRacuna);
        }
        finally
        {
            await trendDb.DnevnikPromena.Where(x => x.Id == movementId).ExecuteDeleteAsync();
            await trendDb.Artikli.Where(x => x.Id == artikalId).ExecuteDeleteAsync();
            await DisposeAsync(trendDb, analyticsDb);
        }
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAnalyticsFallback_WhenTrendplusRecordMissing()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId() + 1000;
        var artikalId = uniqueId;
        var sourceId = uniqueId + 1;

        try
        {
            analyticsDb.ProductsDim.Add(new ProductsDim
            {
                ProductId = artikalId,
                ProductName = "Fallback Product",
                Category = "Test",
                SubCategory = "Test",
                Brand = "Trendplus",
                Timestamp = DateTime.UtcNow,
                DataOrigin = "access"
            });

            analyticsDb.InventoryMovementFacts.Add(new InventoryMovementFact
            {
                SourceId = sourceId,
                TipPromene = "Unos robe",
                Datum = DateTime.UtcNow,
                ArtikalId = artikalId,
                Kolicina = 7,
                StaraProdajnaCena = null,
                NovaProdajnaCena = 1999m,
                Iznos = 13993m,
                BrojDokumenta = "FB-001",
                KorisnikIme = "fallback-user",
                DataOrigin = "access"
            });

            await analyticsDb.SaveChangesAsync();

            var service = new DnevnikPromenaReadService(trendDb, analyticsDb, NullLogger<DnevnikPromenaReadService>.Instance);
            var detail = await service.GetByIdAsync(sourceId);

            Assert.NotNull(detail);
            Assert.Equal(sourceId, detail!.Id);
            Assert.Equal(sourceId, detail.SourceId);
            Assert.Equal("Fallback Product", detail.NazivArtikla);
            Assert.Equal("FB-001", detail.BrojRacuna);
            Assert.Equal("fallback-user", detail.KorisnikIme);
            Assert.Null(detail.Komentar);
        }
        finally
        {
            await analyticsDb.InventoryMovementFacts.Where(x => x.SourceId == sourceId).ExecuteDeleteAsync();
            await analyticsDb.ProductsDim.Where(x => x.ProductId == artikalId).ExecuteDeleteAsync();
            await DisposeAsync(trendDb, analyticsDb);
        }
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenRecordDoesNotExist()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        try
        {
            var service = new DnevnikPromenaReadService(trendDb, analyticsDb, NullLogger<DnevnikPromenaReadService>.Instance);
            var detail = await service.GetByIdAsync(2_000_000_001);
            Assert.Null(detail);
        }
        finally
        {
            await DisposeAsync(trendDb, analyticsDb);
        }
    }

    private static bool TryCreateContexts(
        [NotNullWhen(true)] out TrendplusDbContext? trendDb,
        [NotNullWhen(true)] out AnalyticsDbContext? analyticsDb)
    {
        trendDb = null;
        analyticsDb = null;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        if (!IntegrationDbGuard.TryResolveConnectionString(
                configuration.GetConnectionString("DefaultConnection"),
                out var trendConnection))
        {
            return false;
        }

        if (!IntegrationDbGuard.TryResolveConnectionString(
                configuration.GetConnectionString("AnalyticsConnection") ?? trendConnection,
                out var analyticsConnection))
        {
            return false;
        }

        if (!IntegrationDbGuard.TryEnsureAvailable(
            ("DefaultConnection", trendConnection),
            ("AnalyticsConnection", analyticsConnection)))
        {
            return false;
        }

        var trendOptions = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(trendConnection)
            .Options;

        var analyticsOptions = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql(analyticsConnection)
            .Options;

        trendDb = new TrendplusDbContext(trendOptions);
        analyticsDb = new AnalyticsDbContext(analyticsOptions);
        return true;
    }

    private static int CreateUniqueId()
        => 900_000_000 + Random.Shared.Next(1, 10_000);

    private static async Task DisposeAsync(params IAsyncDisposable[] disposables)
    {
        foreach (var disposable in disposables)
        {
            await disposable.DisposeAsync();
        }
    }
}
