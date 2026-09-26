using System.Reflection;
using Api.Config;
using Api.Models;
using Api.Services;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportReceiptDiagnosticsTests
{
    private static readonly DateTime SaleDate = new(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CoincidentUnrelatedIdsDoNotProduceMismatchWarning()
    {
        await using var trendDb = CreateTrendDb();
        await using var analyticsDb = CreateAnalyticsDb();
        SeedArticle(trendDb);
        AddReceipt(trendDb, saleId: 14, receiptNumber: "R-14", amount: 200m);
        AddDnevnik(trendDb, id: 14, receiptNumber: "R-other", amount: 180m);
        await trendDb.SaveChangesAsync();

        var service = CreateService(trendDb, analyticsDb);
        MarkImportedSale(service, 14);
        var result = new AccessImportRunResponse();

        await InvokeAppendImportedSalesDiagnosticsAsync(service, result);

        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Contains("neusklađenost", StringComparison.OrdinalIgnoreCase)
                       || warning.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RealIdentityMismatchIsReportedWithDiacritics()
    {
        await using var trendDb = CreateTrendDb();
        await using var analyticsDb = CreateAnalyticsDb();
        SeedArticle(trendDb);
        AddReceipt(trendDb, saleId: 15, receiptNumber: "R-15", amount: 200m);
        AddDnevnik(trendDb, id: 915, receiptNumber: "R-15", amount: 180m);
        await trendDb.SaveChangesAsync();

        var service = CreateService(trendDb, analyticsDb);
        MarkImportedSale(service, 15);
        var result = new AccessImportRunResponse();

        await InvokeAppendImportedSalesDiagnosticsAsync(service, result);

        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("neusklađenost", StringComparison.Ordinal)
                       && warning.Contains("računa", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignedReturnAmountsDoNotFalseMismatch()
    {
        await using var trendDb = CreateTrendDb();
        await using var analyticsDb = CreateAnalyticsDb();
        SeedArticle(trendDb);
        AddReceipt(trendDb, saleId: 16, receiptNumber: "R-16", amount: -40m);
        AddDnevnik(trendDb, id: 916, receiptNumber: "R-16", amount: -40m);
        await trendDb.SaveChangesAsync();

        var service = CreateService(trendDb, analyticsDb);
        MarkImportedSale(service, 16);
        var result = new AccessImportRunResponse();

        await InvokeAppendImportedSalesDiagnosticsAsync(service, result);

        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Contains("neusklađenost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NonStandardWithoutLinesUsesIdentityNotCoincidentId()
    {
        await using var trendDb = CreateTrendDb();
        await using var analyticsDb = CreateAnalyticsDb();
        SeedArticle(trendDb);
        trendDb.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 50,
            BrojRacuna = "ABC-50",
            DatumProdaje = SaleDate,
            IDObjekat = 1,
            DataOrigin = "access"
        });
        AddDnevnik(trendDb, id: 50, receiptNumber: "OTHER-50", amount: 999m);
        AddDnevnik(trendDb, id: 51, receiptNumber: "ABC-50", amount: 125m);
        await trendDb.SaveChangesAsync();

        var service = CreateService(trendDb, analyticsDb);
        MarkImportedSale(service, 50);
        var result = new AccessImportRunResponse();

        await InvokeAppendImportedSalesDiagnosticsAsync(service, result);

        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("Poznat promet: 125", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Contains("999", StringComparison.Ordinal));
    }

    private static AccessImportService CreateService(TrendplusDbContext trendDb, AnalyticsDbContext analyticsDb)
        => new(
            trendDb: trendDb,
            analyticsDb: analyticsDb,
            logger: NullLogger<AccessImportService>.Instance,
            options: Options.Create(new AccessImportOptions()));

    private static void MarkImportedSale(AccessImportService service, int saleId)
    {
        var field = typeof(AccessImportService).GetField(
            "_analyticsDeltaSaleIds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var set = Assert.IsAssignableFrom<HashSet<int>>(field!.GetValue(service));
        set.Add(saleId);
    }

    private static async Task InvokeAppendImportedSalesDiagnosticsAsync(
        AccessImportService service,
        AccessImportRunResponse result)
    {
        var method = typeof(AccessImportService).GetMethod(
            "AppendImportedSalesDiagnosticsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task?)method!.Invoke(service, [result, CancellationToken.None]);
        Assert.NotNull(task);
        await task!;
    }

    private static TrendplusDbContext CreateTrendDb()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TrendplusDbContext(options);
    }

    private static AnalyticsDbContext CreateAnalyticsDb()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AnalyticsDbContext(options);
    }

    private static void SeedArticle(TrendplusDbContext db)
    {
        db.Dobavljaci.Add(new Dobavljac { Id = 1, Naziv = "Dobavljac A", DataOrigin = "existing" });
        db.Artikli.Add(new Artikli
        {
            Id = 101,
            Naziv = "A1",
            IDDobavljac = 1,
            DataOrigin = "existing",
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static void AddReceipt(TrendplusDbContext db, int saleId, string receiptNumber, decimal amount)
    {
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = saleId,
            BrojRacuna = receiptNumber,
            DatumProdaje = SaleDate,
            IDObjekat = 1,
            DataOrigin = "access"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = saleId + 1000,
            IdProdaja = saleId,
            IdArtikal = 101,
            Kolicina = 1,
            Cena = amount
        });
    }

    private static void AddDnevnik(TrendplusDbContext db, int id, string? receiptNumber, decimal amount)
    {
        db.DnevnikPromena.Add(new DnevnikPromena
        {
            Id = id,
            TipPromene = TipPromeneConstants.Prodaja,
            Datum = SaleDate.AddHours(1),
            Iznos = amount,
            BrojRacuna = receiptNumber,
            IDObjekat = 1,
            DataOrigin = "access"
        });
    }
}
