using Application.Analytics.Queries.GetDataQualityIssues;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DataQualityPostgresIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DataQualityPostgresIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IssuesHandler_ExecutesRealSqlAndHonorsSearchScopeAndRevenueThreshold()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        await SeedQualityDatasetAsync(db);
        var handler = new GetDataQualityIssuesHandler(db);

        var result = await handler.Handle(
            new GetDataQualityIssuesQuery(
                Type: DataQualityIssueTypes.MissingSupplier,
                Page: 1,
                PageSize: 20,
                Query: "DQ-EXISTING-HIGH",
                SortBy: "sales30d",
                SortDir: "desc",
                DataScope: "existing",
                MinSalesRsd: 1_000m),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("DQ-EXISTING-HIGH", item.Sku);
        Assert.Equal(DataQualityIssueTypes.MissingSupplier, item.IssueType);
        Assert.Equal(5_000m, item.Sales30d);
        Assert.Equal(4, item.Stock);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task IssuesHandler_PaginatesAndUsesStableRevenueOrdering()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        await SeedQualityDatasetAsync(db);
        var handler = new GetDataQualityIssuesHandler(db);

        var firstPage = await handler.Handle(
            new GetDataQualityIssuesQuery(
                Type: DataQualityIssueTypes.MissingSupplier,
                Page: 1,
                PageSize: 1,
                Query: "DQ-EXISTING",
                SortBy: "sales30d",
                SortDir: "desc",
                DataScope: "existing",
                MinSalesRsd: 0m),
            CancellationToken.None);
        var secondPage = await handler.Handle(
            new GetDataQualityIssuesQuery(
                Type: DataQualityIssueTypes.MissingSupplier,
                Page: 2,
                PageSize: 1,
                Query: "DQ-EXISTING",
                SortBy: "sales30d",
                SortDir: "desc",
                DataScope: "existing",
                MinSalesRsd: 0m),
            CancellationToken.None);

        Assert.Equal(2, firstPage.Total);
        Assert.Equal(2, secondPage.Total);
        Assert.Equal("DQ-EXISTING-HIGH", Assert.Single(firstPage.Items).Sku);
        Assert.Equal("DQ-EXISTING-LOW", Assert.Single(secondPage.Items).Sku);
        Assert.NotEqual(firstPage.Items[0].ProductId, secondPage.Items[0].ProductId);
    }

    [Fact]
    public async Task IssuesHandler_ClassifiesMissingShoeTypeAndInvalidNameAfterSupplierValidation()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        await SeedQualityDatasetAsync(db);
        var handler = new GetDataQualityIssuesHandler(db);

        var missingType = await handler.Handle(
            new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.MissingShoeType,
                Query: "DQ-MISSING-TYPE",
                MinSalesRsd: 0m),
            CancellationToken.None);
        var invalidName = await handler.Handle(
            new GetDataQualityIssuesQuery(
                DataQualityIssueTypes.InvalidName,
                Query: "DQ-INVALID-NAME",
                MinSalesRsd: 0m),
            CancellationToken.None);

        var typeItem = Assert.Single(missingType.Items);
        Assert.Equal("Valid Supplier", typeItem.SupplierName);
        Assert.Null(typeItem.ShoeTypeName);
        Assert.Equal(DataQualityIssueTypes.MissingShoeType, typeItem.IssueType);

        var nameItem = Assert.Single(invalidName.Items);
        Assert.Null(nameItem.Name);
        Assert.Equal("Patike", nameItem.ShoeTypeName);
        Assert.Equal(DataQualityIssueTypes.InvalidName, nameItem.IssueType);
    }

    [Fact]
    public async Task TopOffenders_ComputesRevenueImpactPercentOrderingAndActionUrl()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        await SeedQualityDatasetAsync(db);
        var service = new AnalyticsDataQualityHealthService(db);

        var items = await service.GetTopOffendersAsync(
            DataQualityIssueTypes.MissingSupplier,
            limit: 10,
            minSalesRsd: 1_000m,
            dataScope: "existing",
            CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Equal("DQ-EXISTING-HIGH", items[0].Sku);
        Assert.Equal(5_000m, items[0].RevenueImpactRsd);
        Assert.Equal(62.5d, items[0].RevenueImpactPct);
        Assert.Equal("DQ-EXISTING-MEDIUM", items[1].Sku);
        Assert.Equal(3_000m, items[1].RevenueImpactRsd);
        Assert.Equal(37.5d, items[1].RevenueImpactPct);
        Assert.Equal(100d, items.Sum(item => item.RevenueImpactPct), precision: 2);
        Assert.Equal($"/artikli/{items[0].ProductId}/edit", items[0].ActionUrl);
    }

    [Fact]
    public async Task TopOffenders_ImportedScopeDoesNotLeakExistingRowsAndHonorsLimit()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        await SeedQualityDatasetAsync(db);
        var service = new AnalyticsDataQualityHealthService(db);

        var items = await service.GetTopOffendersAsync(
            DataQualityIssueTypes.MissingSupplier,
            limit: 1,
            minSalesRsd: 0m,
            dataScope: "imported",
            CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("DQ-IMPORTED", item.Sku);
        Assert.Equal(7_000m, item.Sales30d);
        Assert.Equal(100d, item.RevenueImpactPct);
    }

    [Fact]
    public async Task TopOffenders_ExistingScopeExcludesImportedHeaderSalesOnExistingArticle()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.TipoviObuce.Add(new TipObuce { Id = 1, Naziv = "Patike", DataOrigin = "existing" });
        db.Artikli.Add(Article(
            id: 201,
            sku: "DQ-MIXED-ORIGIN",
            name: "Existing article with mixed sales",
            supplierId: null,
            shoeTypeId: 1,
            stock: 2,
            origin: "existing"));
        db.ProdajaZaglavlja.AddRange(
            Sale(401, now.AddDays(-1), "existing"),
            Sale(402, now.AddDays(-1), "access"));
        db.ProdajaStavke.AddRange(
            Line(501, 401, 201, 1, 1_000m),
            Line(502, 402, 201, 1, 9_000m));
        await db.SaveChangesAsync();

        var service = new AnalyticsDataQualityHealthService(db);
        var items = await service.GetTopOffendersAsync(
            DataQualityIssueTypes.MissingSupplier,
            limit: 10,
            minSalesRsd: 0m,
            dataScope: "existing",
            CancellationToken.None);

        var item = Assert.Single(items, row => row.Sku == "DQ-MIXED-ORIGIN");
        Assert.Equal(1_000m, item.RevenueImpactRsd);
        Assert.Equal(1_000m, item.Sales30d);
    }

    [Fact]
    public async Task TopOffenders_AllScopeStillIncludesCrossOriginSalesTotals()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.TipoviObuce.Add(new TipObuce { Id = 1, Naziv = "Patike", DataOrigin = "existing" });
        db.Artikli.Add(Article(
            id: 201,
            sku: "DQ-MIXED-ORIGIN",
            name: "Existing article with mixed sales",
            supplierId: null,
            shoeTypeId: 1,
            stock: 2,
            origin: "existing"));
        db.ProdajaZaglavlja.AddRange(
            Sale(401, now.AddDays(-1), "existing"),
            Sale(402, now.AddDays(-1), "access"));
        db.ProdajaStavke.AddRange(
            Line(501, 401, 201, 1, 1_000m),
            Line(502, 402, 201, 1, 9_000m));
        await db.SaveChangesAsync();

        var service = new AnalyticsDataQualityHealthService(db);
        var items = await service.GetTopOffendersAsync(
            DataQualityIssueTypes.MissingSupplier,
            limit: 10,
            minSalesRsd: 0m,
            dataScope: "all",
            CancellationToken.None);

        var item = Assert.Single(items, row => row.Sku == "DQ-MIXED-ORIGIN");
        Assert.Equal(10_000m, item.RevenueImpactRsd);
    }

    [Fact]
    public async Task TopOffenders_MissingCost_ReturnsProductsWithoutPurchaseCost_EvenWithSupplier()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Dobavljaci.Add(new Dobavljac { Id = 1, Naziv = "Valid Supplier", DataOrigin = "existing" });
        db.TipoviObuce.Add(new TipObuce { Id = 1, Naziv = "Patike", DataOrigin = "existing" });
        db.Artikli.AddRange(
            new Artikli
            {
                Id = 301,
                PLU = "DQ-NO-COST",
                Naziv = "Missing cost with supplier",
                IDDobavljac = 1,
                IDTipObuce = 1,
                Kolicina = 3,
                NabavnaCena = null,
                DataOrigin = "existing",
                UpdatedAt = now
            },
            new Artikli
            {
                Id = 302,
                PLU = "DQ-HAS-COST",
                Naziv = "Has cost",
                IDDobavljac = 1,
                IDTipObuce = 1,
                Kolicina = 3,
                NabavnaCena = 50m,
                DataOrigin = "existing",
                UpdatedAt = now
            });
        db.ProdajaZaglavlja.AddRange(
            Sale(601, now.AddDays(-1), "existing"),
            Sale(602, now.AddDays(-1), "existing"));
        db.ProdajaStavke.AddRange(
            Line(701, 601, 301, 2, 2_000m),
            Line(702, 602, 302, 2, 8_000m));
        await db.SaveChangesAsync();

        var service = new AnalyticsDataQualityHealthService(db);
        var items = await service.GetTopOffendersAsync(
            DataQualityIssueTypes.MissingCost,
            limit: 10,
            minSalesRsd: 0m,
            dataScope: "existing",
            CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("DQ-NO-COST", item.Sku);
        Assert.Equal(4_000m, item.RevenueImpactRsd);
        Assert.DoesNotContain(items, row => row.Sku == "DQ-HAS-COST");
    }

    [Fact]
    public async Task TopOffenders_UnknownIssueType_ThrowsInsteadOfSilentSupplierFallback()
    {
        await using var db = await CreateDatabaseAsync();
        if (db is null)
        {
            return;
        }

        var service = new AnalyticsDataQualityHealthService(db);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetTopOffendersAsync(
                "notARealIssue",
                limit: 5,
                minSalesRsd: 0m,
                dataScope: "all",
                CancellationToken.None));
    }

    private async Task<TrendplusDbContext?> CreateDatabaseAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return null;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_dq_{Guid.NewGuid():N}");
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

    private static async Task SeedQualityDatasetAsync(TrendplusDbContext db)
    {
        var now = DateTime.UtcNow;
        db.Dobavljaci.Add(new Dobavljac
        {
            Id = 1,
            Naziv = "Valid Supplier",
            DataOrigin = "existing"
        });
        db.TipoviObuce.Add(new TipObuce
        {
            Id = 1,
            Naziv = "Patike",
            DataOrigin = "existing"
        });

        db.Artikli.AddRange(
            Article(101, "DQ-EXISTING-HIGH", "High impact", supplierId: null, shoeTypeId: 1, stock: 4, origin: "existing"),
            Article(102, "DQ-EXISTING-MEDIUM", "Medium impact", supplierId: null, shoeTypeId: 1, stock: 3, origin: "existing"),
            Article(103, "DQ-EXISTING-LOW", "Low impact", supplierId: null, shoeTypeId: 1, stock: 2, origin: "existing"),
            Article(104, "DQ-IMPORTED", "Imported impact", supplierId: null, shoeTypeId: 1, stock: 6, origin: "access"),
            Article(105, "DQ-MISSING-TYPE", "Missing type", supplierId: 1, shoeTypeId: null, stock: 2, origin: "existing"),
            Article(106, "DQ-INVALID-NAME", "   ", supplierId: 1, shoeTypeId: 1, stock: 1, origin: "existing"));

        db.ProdajaZaglavlja.AddRange(
            Sale(201, now.AddDays(-2), "existing"),
            Sale(202, now.AddDays(-2), "existing"),
            Sale(203, now.AddDays(-2), "existing"),
            Sale(204, now.AddDays(-2), "access"),
            Sale(205, now.AddDays(-2), "existing"),
            Sale(206, now.AddDays(-2), "existing"));

        db.ProdajaStavke.AddRange(
            Line(301, 201, 101, 2, 2_500m),
            Line(302, 202, 102, 2, 1_500m),
            Line(303, 203, 103, 1, 200m),
            Line(304, 204, 104, 2, 3_500m),
            Line(305, 205, 105, 1, 1_200m),
            Line(306, 206, 106, 1, 900m));

        await db.SaveChangesAsync();
    }

    private static Artikli Article(
        int id,
        string sku,
        string name,
        int? supplierId,
        int? shoeTypeId,
        int stock,
        string origin) =>
        new()
        {
            Id = id,
            PLU = sku,
            Naziv = name,
            IDDobavljac = supplierId,
            IDTipObuce = shoeTypeId,
            Kolicina = stock,
            NabavnaCena = 100m,
            DataOrigin = origin,
            UpdatedAt = DateTime.UtcNow
        };

    private static ProdajaZaglavlje Sale(int id, DateTime date, string origin) =>
        new()
        {
            Id = id,
            BrojRacuna = $"DQ-{id}",
            DatumProdaje = date,
            DataOrigin = origin
        };

    private static ProdajaStavka Line(int id, int saleId, int articleId, int quantity, decimal price) =>
        new()
        {
            Id = id,
            IdProdaja = saleId,
            IdArtikal = articleId,
            Kolicina = quantity,
            Cena = price,
            NabavnaCena = 100m
        };
}
