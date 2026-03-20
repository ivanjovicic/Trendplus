using Application.Analytics.Queries.GetDataQualityIssues;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests;

public sealed class DataQualityIssuesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMissingSupplier_Items()
    {
        var (trendDb, analyticsDb) = CreateContexts();
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
        var (trendDb, analyticsDb) = CreateContexts();
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
        var (trendDb, analyticsDb) = CreateContexts();
        var uniqueId = CreateUniqueId() + 20_000;
        var supplierId = uniqueId + 100;
        var tipId = uniqueId + 101;
        var productId = uniqueId + 1;
        var sku = $"DQ-IN-{productId}";

        try
        {
            trendDb.Dobavljaci.Add(new Dobavljac { Id = supplierId, Naziv = "   ", DataOrigin = "existing" });
            trendDb.TipoviObuce.Add(new TipObuce { Id = tipId, Naziv = "Cipele", DataOrigin = "existing" });
            trendDb.Artikli.Add(new Artikli
            {
                Id = productId,
                PLU = sku,
                Naziv = "Artikal sa praznim dobavljacem",
                IDTipObuce = tipId,
                IDDobavljac = supplierId,
                Kolicina = 1,
                UpdatedAt = DateTime.UtcNow,
                DataOrigin = "existing"
            });

            await trendDb.SaveChangesAsync();

            var handler = new GetDataQualityIssuesHandler(trendDb);
            var result = await handler.Handle(new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.InvalidName,
                Query: sku), CancellationToken.None);

            var item = Assert.Single(result.Items, x => x.ProductId == productId.ToString());
            Assert.Equal(DataQualityIssueTypes.InvalidName, item.IssueType);
            Assert.Null(item.SupplierName);
        }
        finally
        {
            await CleanupAsync(trendDb, analyticsDb, productId, saleId: null, saleItemId: null, supplierId, shoeTypeId: tipId);
        }
    }

    [Fact]
    public async Task Handle_SupportsPagination_AndSorting()
    {
        var (trendDb, analyticsDb) = CreateContexts();
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

    private static (TrendplusDbContext trendDb, AnalyticsDbContext analyticsDb) CreateContexts()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var trendConnection = configuration.GetConnectionString("DefaultConnection");
        var analyticsConnection = configuration.GetConnectionString("AnalyticsConnection") ?? trendConnection;

        Assert.False(string.IsNullOrWhiteSpace(trendConnection));
        Assert.False(string.IsNullOrWhiteSpace(analyticsConnection));

        var trendOptions = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(trendConnection)
            .Options;

        var analyticsOptions = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql(analyticsConnection)
            .Options;

        return (new TrendplusDbContext(trendOptions), new AnalyticsDbContext(analyticsOptions));
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
