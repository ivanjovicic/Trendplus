using Application.Analytics;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trendplus2.Tests;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class InventoryOperationsIntegrityProbeIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _postgres;

    public InventoryOperationsIntegrityProbeIntegrationTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    [OperationsIntegrationFact(DisplayName = "Inventory integrity probe bounds scope and detects stock, sell-through and store-grain drift")]
    public async Task Probe_UsesScopedBoundedFactsAndPreservesUnknownAndNegativeStores()
    {
        Assert.True(_postgres.IsAvailable, "The Inventory integrity proof requires a disposable PostgreSQL Testcontainer.");
        var connectionString = await _postgres.TryCreateDatabaseConnectionStringAsync($"rq563_inventory_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var trendOptions = new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options;
        var analyticsOptions = new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(connectionString).Options;
        await using var trendDb = new TrendplusDbContext(trendOptions);
        await using var analyticsDb = new AnalyticsDbContext(analyticsOptions);
        await trendDb.Database.MigrateAsync();
        await analyticsDb.Database.MigrateAsync();
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "ImagePath" text;
                ALTER TABLE "ProductsDim" ADD COLUMN IF NOT EXISTS "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing';
                ALTER TABLE "SalesFacts" ADD COLUMN IF NOT EXISTS "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing';
                ALTER TABLE "SalesLineFacts" ADD COLUMN IF NOT EXISTS "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing';
                """, connection);
            await command.ExecuteNonQueryAsync();
        }

        var importedArticle = new Artikli
        {
            PLU = "RQ563-IMPORTED",
            Naziv = "Imported inventory test article",
            Kolicina = 10,
            MinimalnaKolicina = 2,
            NabavnaCena = 50m,
            IDObjekat = -1,
            DataOrigin = "access",
            UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var existingArticle = new Artikli
        {
            PLU = "RQ563-EXISTING",
            Naziv = "Existing inventory test article",
            Kolicina = 5,
            MinimalnaKolicina = 1,
            NabavnaCena = 25m,
            IDObjekat = null,
            DataOrigin = "existing",
            UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        trendDb.Artikli.AddRange(importedArticle, existingArticle);
        await trendDb.SaveChangesAsync();

        var sale = new ProdajaZaglavlje
        {
            BrojRacuna = "RQ563-SALE",
            DatumProdaje = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc),
            IDObjekat = -1,
            DataOrigin = "access"
        };
        trendDb.ProdajaZaglavlja.Add(sale);
        await trendDb.SaveChangesAsync();
        trendDb.ProdajaStavke.Add(new ProdajaStavka
        {
            IdProdaja = sale.Id,
            IdArtikal = importedArticle.Id,
            Kolicina = 3,
            Cena = 100m
        });
        await trendDb.SaveChangesAsync();

        var importedDimension = new ProductsDim
        {
            ProductId = importedArticle.Id,
            ProductName = importedArticle.Naziv,
            Kolicina = 10,
            Timestamp = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc),
            DataOrigin = "access"
        };
        var existingDimension = new ProductsDim
        {
            ProductId = existingArticle.Id,
            ProductName = existingArticle.Naziv,
            Kolicina = 5,
            Timestamp = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc),
            DataOrigin = "existing"
        };
        var saleFact = new SalesFact
        {
            SaleId = sale.Id,
            BrojRacuna = sale.BrojRacuna!,
            SaleTimestampUtc = sale.DatumProdaje,
            StoreId = -1,
            PaymentType = "cash",
            TotalAmount = 300m,
            TotalUnits = 3,
            TotalLines = 1,
            DataOrigin = "access"
        };
        var lineFact = new SalesLineFact
        {
            SaleId = sale.Id,
            ProductId = importedArticle.Id,
            Qty = 3,
            UnitPrice = 100m,
            LineTotal = 300m,
            DataOrigin = "access"
        };
        analyticsDb.ProductsDim.AddRange(importedDimension, existingDimension);
        analyticsDb.SalesFacts.Add(saleFact);
        analyticsDb.SalesLineFacts.Add(lineFact);
        await analyticsDb.SaveChangesAsync();

        var probe = new InventoryOperationsIntegrityProbe(trendDb, analyticsDb);
        var request = new OperationsAnalyticsIntegrityProbeRequest(
            OperationsAnalyticsIntegrityFamilies.DefinitionFor(OperationsAnalyticsIntegrityFamilies.Inventory),
            "test-context",
            "test-generation",
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            "imported",
            "integration-test",
            100,
            CancellationToken.None);

        var verified = await probe.ProbeAsync(request);
        Assert.Equal(OperationsAnalyticsIntegrityStates.Verified, verified.Status);
        Assert.Equal(4, verified.ProbeRowCount);

        analyticsDb.ProductsDim.Remove(importedDimension);
        await analyticsDb.SaveChangesAsync();
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, (await probe.ProbeAsync(request)).Status);
        analyticsDb.ProductsDim.Add(importedDimension);
        await analyticsDb.SaveChangesAsync();

        importedArticle.Kolicina = null;
        await trendDb.SaveChangesAsync();
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, (await probe.ProbeAsync(request)).Status);
        importedArticle.Kolicina = 10;
        await trendDb.SaveChangesAsync();

        trendDb.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            BrojRacuna = "RQ563-UNKNOWN-STORE",
            DatumProdaje = new DateTime(2026, 9, 16, 9, 0, 0, DateTimeKind.Utc),
            IDObjekat = null,
            DataOrigin = "access"
        });
        await trendDb.SaveChangesAsync();
        var unknownStoreSale = await trendDb.ProdajaZaglavlja.SingleAsync(header => header.BrojRacuna == "RQ563-UNKNOWN-STORE");
        trendDb.ProdajaStavke.Add(new ProdajaStavka
        {
            IdProdaja = unknownStoreSale.Id,
            IdArtikal = importedArticle.Id,
            Kolicina = 2,
            Cena = 100m
        });
        await trendDb.SaveChangesAsync();
        analyticsDb.SalesFacts.Add(new SalesFact
        {
            SaleId = unknownStoreSale.Id,
            BrojRacuna = unknownStoreSale.BrojRacuna!,
            SaleTimestampUtc = unknownStoreSale.DatumProdaje,
            StoreId = 1,
            PaymentType = "cash",
            TotalAmount = 200m,
            TotalUnits = 2,
            TotalLines = 1,
            DataOrigin = "access"
        });
        analyticsDb.SalesLineFacts.Add(new SalesLineFact
        {
            SaleId = unknownStoreSale.Id,
            ProductId = importedArticle.Id,
            Qty = 2,
            UnitPrice = 100m,
            LineTotal = 200m,
            DataOrigin = "access"
        });
        await analyticsDb.SaveChangesAsync();

        var storeDrift = await probe.ProbeAsync(request);
        Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, storeDrift.Status);
        Assert.Contains(storeDrift.Deltas, delta => delta.Dimension == $"inventory_sell_through:product:{importedArticle.Id}|store:unknown");
        Assert.Contains(storeDrift.Deltas, delta => delta.Dimension == $"inventory_sell_through:product:{importedArticle.Id}|store:1");
        Assert.DoesNotContain(storeDrift.Deltas, delta => delta.Dimension.EndsWith("store:-1", StringComparison.Ordinal));

        importedDimension.Kolicina = 9;
        await analyticsDb.SaveChangesAsync();
        var stockDrift = await probe.ProbeAsync(request);
        Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, stockDrift.Status);
        Assert.Contains(stockDrift.Deltas, delta => delta.Dimension == $"inventory_stock:product:{importedArticle.Id}");

        lineFact.Qty = 4;
        await analyticsDb.SaveChangesAsync();
        var sellThroughDrift = await probe.ProbeAsync(request);
        Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, sellThroughDrift.Status);
        Assert.Contains(sellThroughDrift.Deltas, delta => delta.Dimension.StartsWith("inventory_sell_through:", StringComparison.Ordinal));

        var tooSmallWindow = request with { ToUtc = request.FromUtc.AddDays(32) };
        Assert.Equal(OperationsAnalyticsIntegrityStates.Degraded, (await probe.ProbeAsync(tooSmallWindow)).Status);
        var tooManyRows = request with { MaxRows = 1 };
        Assert.Equal(OperationsAnalyticsIntegrityStates.Degraded, (await probe.ProbeAsync(tooManyRows)).Status);
        var emptySignalWindow = request with
        {
            FromUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, (await probe.ProbeAsync(emptySignalWindow)).Status);
    }
}
