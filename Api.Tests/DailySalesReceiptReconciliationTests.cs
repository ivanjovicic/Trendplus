using Api.Models;
using Api.Services;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class DailySalesReceiptReconciliationTests
{
    private static readonly DateTime SaleDate = new(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MatchingReceiptIdentityProducesNoMismatch()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddReceipt(db, 10, " R-10 ", 200m);
        AddDnevnik(db, 900, "r-10", 200m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("verified", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.MatchedReceiptCount);
        Assert.Equal(0, result.Metadata.ReceiptReconciliation.MismatchCount);
        Assert.Equal(0, result.Metadata.ReceiptAmountMismatchCount);
        Assert.Equal(0m, result.Metadata.ReceiptAmountMismatchRevenue);
    }

    [Fact]
    public async Task MatchingReceiptIdentityReportsSignedAmountDifference()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddReceipt(db, 11, "R-11", 200m);
        AddDnevnik(db, 901, "R-11", 180m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("verified", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.MatchedReceiptCount);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.MismatchCount);
        Assert.Equal(20m, result.Metadata.ReceiptReconciliation.MismatchAmount);
        Assert.Equal(1, result.Metadata.ReceiptAmountMismatchCount);
        Assert.Contains(result.Metadata.Warnings, warning => warning.Contains("ne odgovara dnevniku", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DnevnikWithoutReceiptIdentityKeepsVerifiedCoverageOverIdentityBearingRows()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddReceipt(db, 12, "R-12", 200m);
        AddDnevnik(db, 902, null, 200m);
        AddDnevnik(db, 912, "R-12", 200m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("verified", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal("partial_dnevnik_identity_coverage", result.Metadata.ReceiptReconciliation.ReasonCode);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.MatchedReceiptCount);
        Assert.Equal(0, result.Metadata.ReceiptReconciliation.MismatchCount);
        Assert.True(result.Metadata.ReceiptReconciliation.UnmatchedDnevnikReceiptCount >= 1);
    }

    [Fact]
    public async Task OnlyIdentityLessDnevnikRowsMakeReconciliationUnavailable()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddReceipt(db, 12, "R-12", 200m);
        AddDnevnik(db, 902, null, 200m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("unavailable", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal("dnevnik_receipt_identity_missing", result.Metadata.ReceiptReconciliation.ReasonCode);
        Assert.Null(result.Metadata.ReceiptReconciliation.MismatchCount);
        Assert.Null(result.Metadata.ReceiptAmountMismatchCount);
        Assert.Null(result.Metadata.ReceiptAmountMismatchRevenue);
        Assert.Contains(result.Metadata.Warnings, warning => warning.Contains("nije dostupna", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReceiptWithoutDnevnikIsCountedAsUnmatchedNotAsMismatch()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddReceipt(db, 13, "R-13", 200m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("verified", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal(0, result.Metadata.ReceiptReconciliation.MatchedReceiptCount);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.UnmatchedReceiptCount);
        Assert.Equal(0, result.Metadata.ReceiptReconciliation.MismatchCount);
    }

    [Fact]
    public async Task CoincidentUnrelatedIdsAreNotJoined()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddReceipt(db, 14, "R-14", 200m);
        AddDnevnik(db, 14, "R-other", 180m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("verified", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal(0, result.Metadata.ReceiptReconciliation.MatchedReceiptCount);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.UnmatchedReceiptCount);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.UnmatchedDnevnikReceiptCount);
        Assert.Equal(0, result.Metadata.ReceiptReconciliation.MismatchCount);
        Assert.DoesNotContain(result.Metadata.Warnings, warning => warning.Contains("neusklađen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BlankDnevnikReceiptNumberAloneIsUnavailable()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddDnevnik(db, 903, "   ", 50m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("unavailable", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal("dnevnik_receipt_identity_missing", result.Metadata.ReceiptReconciliation.ReasonCode);
        Assert.Null(result.Metadata.ReceiptReconciliation.MismatchAmount);
    }

    [Fact]
    public async Task NonStandardRevenueUsesIdentityNotCoincidentIds()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        // Sale header id 50 with non-standard receipt; dnevnik id 50 is a different receipt.
        AddReceipt(db, 50, "ABC-50", 125m);
        AddDnevnik(db, 50, "OTHER-50", 999m);
        AddDnevnik(db, 51, "ABC-50", 125m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal(1, result.Metadata.NonStandardReceiptCount);
        Assert.Equal(125m, result.Metadata.NonStandardReceiptRevenue);
    }

    [Fact]
    public async Task SignedJournalAmountReconcilesWithoutFalseMismatch()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddReceipt(db, 60, "R-60", -40m);
        AddDnevnik(db, 960, "R-60", -40m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("verified", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.MatchedReceiptCount);
        Assert.Equal(0, result.Metadata.ReceiptReconciliation.MismatchCount);
    }

    [Fact]
    public async Task OppositeSignedJournalVersusReceiptIsMismatchNotAbsEqual()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        AddReceipt(db, 61, "R-61", 100m);
        AddDnevnik(db, 961, "R-61", -100m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal("verified", result.Metadata.ReceiptReconciliation.Status);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.MatchedReceiptCount);
        Assert.Equal(1, result.Metadata.ReceiptReconciliation.MismatchCount);
        Assert.Equal(200m, result.Metadata.ReceiptReconciliation.MismatchAmount);
    }

    [Fact]
    public async Task NonStandardWithoutLineTotalsUsesIdentityFallback()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 70,
            BrojRacuna = "ABC-70",
            DatumProdaje = SaleDate,
            IDObjekat = 1,
            DataOrigin = "existing"
        });
        // Coincident dnevnik id must not contribute; matching identity does.
        AddDnevnik(db, 70, "OTHER-70", 999m);
        AddDnevnik(db, 971, "ABC-70", 55m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal(1, result.Metadata.NonStandardReceiptCount);
        Assert.Equal(55m, result.Metadata.NonStandardReceiptRevenue);
        Assert.Equal(0, result.Metadata.DebtReceiptCount);
    }

    [Fact]
    public async Task NonStandardWithoutLineTotalsOrIdentityIsUnavailableNotZero()
    {
        await using var db = CreateDbContext();
        SeedArticle(db);
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 71,
            BrojRacuna = "XYZ-71",
            DatumProdaje = SaleDate,
            IDObjekat = 1,
            DataOrigin = "existing"
        });
        AddDnevnik(db, 71, "OTHER-71", 999m);
        await db.SaveChangesAsync();

        var result = await RunAsync(db);

        Assert.Equal(1, result.Metadata.NonStandardReceiptCount);
        Assert.Equal(0m, result.Metadata.NonStandardReceiptRevenue);
        Assert.Contains(
            result.Metadata.Warnings,
            warning => warning.Contains("promet nije dostupan", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<DailySalesTableResponse> RunAsync(TrendplusDbContext db)
    {
        var service = new DailySalesStatsService(db, NullLogger<DailySalesStatsService>.Instance);
        return await service.GetDailySalesAsync(
            requestedFromUtc: SaleDate.Date,
            requestedToUtc: SaleDate.Date,
            storeId: 1,
            topN: 5,
            dataScope: "all",
            ct: CancellationToken.None);
    }

    private static void AddReceipt(TrendplusDbContext db, int id, string receiptNumber, decimal amount)
    {
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = id,
            BrojRacuna = receiptNumber,
            DatumProdaje = SaleDate,
            IDObjekat = 1,
            DataOrigin = "existing"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = id + 1000,
            IdProdaja = id,
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
            DataOrigin = "existing"
        });
    }

    private static TrendplusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TrendplusDbContext(options);
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
}
