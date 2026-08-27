using Application.Analytics.Queries.GetDataQualityIssues;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Api.Tests;

public sealed class DataQualityIssuesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMissingSupplier_Items()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId();
        var tipId = uniqueId + 100;
        var productId = uniqueId + 1;
        var saleId = uniqueId + 2;
        var saleItemId = uniqueId + 3;
        var sku = $"DQ-MS-{productId}";

        try
        {
            trendDb.TipoviObuce.Add(new TipObuce { Id = tipId, Naziv = "Patike", DataOrigin = "existing" });
            trendDb.Artikli.Add(new Artikli
            {
                Id = productId,
                PLU = sku,
                Naziv = "Artikal bez dobavljaca",
                IDTipObuce = tipId,
                IDDobavljac = null,
                Kolicina = 8,
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "existing"
            });
            trendDb.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = saleId,
                BrojRacuna = $"DQ-MS-{saleId}",
                DatumProdaje = DateTime.UtcNow,
                DataOrigin = "existing"
            });
            trendDb.ProdajaStavke.Add(new ProdajaStavka
            {
                Id = saleItemId,
                IdProdaja = saleId,
                IdArtikal = productId,
                Kolicina = 2,
                Cena = 2500m
            });

            await trendDb.SaveChangesAsync();

            var handler = new GetDataQualityIssuesHandler(trendDb);
            var result = await handler.Handle(new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.MissingSupplier,
                Query: sku), CancellationToken.None);

            var item = Assert.Single(result.Items, x => x.ProductId == productId.ToString());
            Assert.Equal(DataQualityIssueTypes.MissingSupplier, item.IssueType);
            Assert.Equal(5000m, item.Sales30d);
        }
        finally
        {
            await CleanupAsync(trendDb, analyticsDb, productId, saleId, saleItemId, supplierId: null, shoeTypeId: tipId);
        }
    }

    [Fact]
    public async Task Handle_ReturnsMissingShoeType_Items()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId() + 10_000;
        var supplierId = uniqueId + 100;
        var productId = uniqueId + 1;
        var sku = $"DQ-MT-{productId}";

        try
        {
            trendDb.Dobavljaci.Add(new Dobavljac { Id = supplierId, Naziv = "Valid Supplier", DataOrigin = "existing" });
            trendDb.Artikli.Add(new Artikli
            {
                Id = productId,
                PLU = sku,
                Naziv = "Artikal bez tipa",
                IDTipObuce = null,
                IDDobavljac = supplierId,
                Kolicina = 3,
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "existing"
            });

            await trendDb.SaveChangesAsync();

            var handler = new GetDataQualityIssuesHandler(trendDb);
            var result = await handler.Handle(new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.MissingShoeType,
                Query: sku), CancellationToken.None);

            var item = Assert.Single(result.Items, x => x.ProductId == productId.ToString());
            Assert.Equal(DataQualityIssueTypes.MissingShoeType, item.IssueType);
            Assert.Equal("Valid Supplier", item.SupplierName);
        }
        finally
        {
            await CleanupAsync(trendDb, analyticsDb, productId, saleId: null, saleItemId: null, supplierId, shoeTypeId: null);
        }
    }

    [Fact]
    public async Task Handle_ReturnsInvalidName_Items()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId() + 20_000;
        var supplierId = uniqueId + 100;
        var tipId = uniqueId + 101;
        var productId = uniqueId + 1;
        var sku = $"DQ-IN-{productId}";

        try
        {
            trendDb.Dobavljaci.Add(new Dobavljac { Id = supplierId, Naziv = "Dobavljac validan", DataOrigin = "existing" });
            trendDb.TipoviObuce.Add(new TipObuce { Id = tipId, Naziv = "Cipele", DataOrigin = "existing" });
            trendDb.Artikli.Add(new Artikli
            {
                Id = productId,
                PLU = sku,
                Naziv = "   ",
                IDTipObuce = tipId,
                IDDobavljac = supplierId,
                Kolicina = 1,
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "existing"
            });
            trendDb.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = uniqueId + 201,
                BrojRacuna = $"DQ-IN-{uniqueId + 201}",
                DatumProdaje = DateTime.UtcNow,
                DataOrigin = "existing"
            });
            trendDb.ProdajaStavke.Add(new ProdajaStavka
            {
                Id = uniqueId + 202,
                IdProdaja = uniqueId + 201,
                IdArtikal = productId,
                Kolicina = 1,
                Cena = 1500m
            });

            await trendDb.SaveChangesAsync();

            var handler = new GetDataQualityIssuesHandler(trendDb);
            var result = await handler.Handle(new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.InvalidName,
                Query: sku), CancellationToken.None);

            var item = Assert.Single(result.Items, x => x.ProductId == productId.ToString());
            Assert.Equal(DataQualityIssueTypes.InvalidName, item.IssueType);
            Assert.Equal("Dobavljac validan", item.SupplierName);
        }
        finally
        {
            await CleanupAsync(trendDb, analyticsDb, productId, saleId: uniqueId + 201, saleItemId: uniqueId + 202, supplierId, shoeTypeId: tipId);
        }
    }

    [Fact]
    public async Task Handle_FiltersLowRevenueNoise_WhenMinSalesSpecified()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId() + 40_000;
        var tipId = uniqueId + 100;
        var productId = uniqueId + 1;
        var saleId = uniqueId + 2;
        var saleItemId = uniqueId + 3;
        var sku = $"DQ-MIN-{productId}";

        try
        {
            trendDb.TipoviObuce.Add(new TipObuce { Id = tipId, Naziv = "Patike", DataOrigin = "existing" });
            trendDb.Artikli.Add(new Artikli
            {
                Id = productId,
                PLU = sku,
                Naziv = "Niskorelevantan artikal",
                IDTipObuce = tipId,
                IDDobavljac = null,
                Kolicina = 2,
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "existing"
            });
            trendDb.ProdajaZaglavlja.Add(new ProdajaZaglavlje
            {
                Id = saleId,
                BrojRacuna = $"DQ-MIN-{saleId}",
                DatumProdaje = DateTime.UtcNow,
                DataOrigin = "existing"
            });
            trendDb.ProdajaStavke.Add(new ProdajaStavka
            {
                Id = saleItemId,
                IdProdaja = saleId,
                IdArtikal = productId,
                Kolicina = 1,
                Cena = 500m
            });

            await trendDb.SaveChangesAsync();

            var handler = new GetDataQualityIssuesHandler(trendDb);
            var result = await handler.Handle(new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.MissingSupplier,
                Query: sku,
                MinSalesRsd: 1000m), CancellationToken.None);

            Assert.Empty(result.Items);
        }
        finally
        {
            await CleanupAsync(trendDb, analyticsDb, productId, saleId, saleItemId, supplierId: null, shoeTypeId: tipId);
        }
    }

    [Fact]
    public async Task Handle_ScopesSales30dByDataScope()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId() + 50_000;
        var importedProductId = uniqueId + 1;
        var existingProductId = uniqueId + 2;
        var importedSku = $"DQ-SCOPE-I-{importedProductId}";
        var existingSku = $"DQ-SCOPE-E-{existingProductId}";

        var importedSaleAccessId = uniqueId + 10;
        var importedSaleExistingId = uniqueId + 11;
        var existingSaleAccessId = uniqueId + 12;
        var existingSaleExistingId = uniqueId + 13;

        try
        {
            trendDb.Artikli.AddRange(
                new Artikli
                {
                    Id = importedProductId,
                    PLU = importedSku,
                    Naziv = "Imported scope issue",
                    IDTipObuce = null,
                    IDDobavljac = null,
                    Kolicina = 4,
                    UpdatedAt = DateTime.UtcNow,
                    DataOrigin = "access"
                },
                new Artikli
                {
                    Id = existingProductId,
                    PLU = existingSku,
                    Naziv = "Existing scope issue",
                    IDTipObuce = null,
                    IDDobavljac = null,
                    Kolicina = 6,
                    UpdatedAt = DateTime.UtcNow,
                    DataOrigin = "existing"
                });

            trendDb.ProdajaZaglavlja.AddRange(
                new ProdajaZaglavlje
                {
                    Id = importedSaleAccessId,
                    BrojRacuna = $"DQ-SCOPE-{importedSaleAccessId}",
                    DatumProdaje = DateTime.UtcNow,
                    DataOrigin = "access"
                },
                new ProdajaZaglavlje
                {
                    Id = importedSaleExistingId,
                    BrojRacuna = $"DQ-SCOPE-{importedSaleExistingId}",
                    DatumProdaje = DateTime.UtcNow,
                    DataOrigin = "existing"
                },
                new ProdajaZaglavlje
                {
                    Id = existingSaleAccessId,
                    BrojRacuna = $"DQ-SCOPE-{existingSaleAccessId}",
                    DatumProdaje = DateTime.UtcNow,
                    DataOrigin = "access"
                },
                new ProdajaZaglavlje
                {
                    Id = existingSaleExistingId,
                    BrojRacuna = $"DQ-SCOPE-{existingSaleExistingId}",
                    DatumProdaje = DateTime.UtcNow,
                    DataOrigin = "existing"
                });

            trendDb.ProdajaStavke.AddRange(
                new ProdajaStavka
                {
                    Id = uniqueId + 20,
                    IdProdaja = importedSaleAccessId,
                    IdArtikal = importedProductId,
                    Kolicina = 1,
                    Cena = 1000m
                },
                new ProdajaStavka
                {
                    Id = uniqueId + 21,
                    IdProdaja = importedSaleExistingId,
                    IdArtikal = importedProductId,
                    Kolicina = 1,
                    Cena = 700m
                },
                new ProdajaStavka
                {
                    Id = uniqueId + 22,
                    IdProdaja = existingSaleAccessId,
                    IdArtikal = existingProductId,
                    Kolicina = 1,
                    Cena = 300m
                },
                new ProdajaStavka
                {
                    Id = uniqueId + 23,
                    IdProdaja = existingSaleExistingId,
                    IdArtikal = existingProductId,
                    Kolicina = 1,
                    Cena = 2000m
                });

            await trendDb.SaveChangesAsync();

            var handler = new GetDataQualityIssuesHandler(trendDb);

            var importedResult = await handler.Handle(new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.MissingSupplier,
                Query: importedSku,
                DataScope: "imported"), CancellationToken.None);

            var importedItem = Assert.Single(importedResult.Items, x => x.ProductId == importedProductId.ToString());
            Assert.Equal(1000m, importedItem.Sales30d);

            var existingResult = await handler.Handle(new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.MissingSupplier,
                Query: existingSku,
                DataScope: "existing"), CancellationToken.None);

            var existingItem = Assert.Single(existingResult.Items, x => x.ProductId == existingProductId.ToString());
            Assert.Equal(2000m, existingItem.Sales30d);
        }
        finally
        {
            await trendDb.ProdajaStavke.Where(x =>
                x.Id == uniqueId + 20 ||
                x.Id == uniqueId + 21 ||
                x.Id == uniqueId + 22 ||
                x.Id == uniqueId + 23).ExecuteDeleteAsync();

            await trendDb.ProdajaZaglavlja.Where(x =>
                x.Id == importedSaleAccessId ||
                x.Id == importedSaleExistingId ||
                x.Id == existingSaleAccessId ||
                x.Id == existingSaleExistingId).ExecuteDeleteAsync();

            await trendDb.Artikli.Where(x =>
                x.Id == importedProductId ||
                x.Id == existingProductId).ExecuteDeleteAsync();

            await trendDb.DisposeAsync();
            await analyticsDb.DisposeAsync();
        }
    }

    [Fact]
    public async Task Handle_SupportsPagination_AndSorting()
    {
        if (!TryCreateContexts(out var trendDb, out var analyticsDb))
        {
            return;
        }

        var uniqueId = CreateUniqueId() + 30_000;
        var tipId = uniqueId + 100;
        var firstProductId = uniqueId + 1;
        var secondProductId = uniqueId + 2;
        var skuPrefix = $"DQ-PAG-{uniqueId}";

        try
        {
            trendDb.TipoviObuce.Add(new TipObuce { Id = tipId, Naziv = "Sandale", DataOrigin = "existing" });
            trendDb.Artikli.AddRange(
                new Artikli
                {
                    Id = firstProductId,
                    PLU = $"{skuPrefix}-A",
                    Naziv = "A artikal",
                    IDTipObuce = tipId,
                    IDDobavljac = null,
                    Kolicina = 2,
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                    DataOrigin = "existing"
                },
                new Artikli
                {
                    Id = secondProductId,
                    PLU = $"{skuPrefix}-B",
                    Naziv = "B artikal",
                    IDTipObuce = tipId,
                    IDDobavljac = null,
                    Kolicina = 5,
                    UpdatedAt = DateTime.UtcNow,
                    DataOrigin = "existing"
                });

            await trendDb.SaveChangesAsync();

            var handler = new GetDataQualityIssuesHandler(trendDb);
            var result = await handler.Handle(new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.MissingSupplier,
                Page: 1,
                PageSize: 1,
                Query: skuPrefix,
                SortBy: "name",
                SortDir: "asc"), CancellationToken.None);

            Assert.Equal(2, result.Total);
            var item = Assert.Single(result.Items);
            Assert.Equal(firstProductId.ToString(), item.ProductId);
        }
        finally
        {
            await trendDb.Artikli.Where(x => x.Id == firstProductId || x.Id == secondProductId).ExecuteDeleteAsync();
            await trendDb.TipoviObuce.Where(x => x.Id == tipId).ExecuteDeleteAsync();
            await trendDb.DisposeAsync();
            await analyticsDb.DisposeAsync();
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
        => 910_000_000 + Random.Shared.Next(1, 10_000);

    private static async Task CleanupAsync(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        int productId,
        int? saleId,
        int? saleItemId,
        int? supplierId,
        int? shoeTypeId)
    {
        if (saleItemId.HasValue)
        {
            await trendDb.ProdajaStavke.Where(x => x.Id == saleItemId.Value).ExecuteDeleteAsync();
        }

        if (saleId.HasValue)
        {
            await trendDb.ProdajaZaglavlja.Where(x => x.Id == saleId.Value).ExecuteDeleteAsync();
        }

        await trendDb.Artikli.Where(x => x.Id == productId).ExecuteDeleteAsync();

        if (supplierId.HasValue)
        {
            await trendDb.Dobavljaci.Where(x => x.Id == supplierId.Value).ExecuteDeleteAsync();
        }

        if (shoeTypeId.HasValue)
        {
            await trendDb.TipoviObuce.Where(x => x.Id == shoeTypeId.Value).ExecuteDeleteAsync();
        }

        await trendDb.DisposeAsync();
        await analyticsDb.DisposeAsync();
    }
}
