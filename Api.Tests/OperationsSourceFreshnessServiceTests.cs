using Api.Services;
using Application.Analytics;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Trendplus2.Dtos;
using Xunit;

namespace Api.Tests;

public sealed class OperationsSourceFreshnessServiceTests
{
    [Fact]
    public async Task ApplyIfRegisteredAsync_FailsClosedWhenFreshnessServiceIsUnavailable()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var meta = CreateMeta();
        meta.LastRefreshAtUtc = DateTime.UtcNow;

        await OperationsSourceFreshnessService.ApplyIfRegisteredAsync(
            services, meta, "imported", null, "recommendation");

        Assert.Equal("unknown", meta.DataFreshnessStatus);
        Assert.Equal("source_freshness_service_unavailable", meta.DataFreshnessReasonCode);
        Assert.Null(meta.LastRefreshAtUtc);
        Assert.NotEqual(AnalyticsDecisionReadinessStates.DecisionReady, meta.DecisionReadiness?.State);
    }

    [Fact]
    public async Task ApplyAsync_UsesDurableImportEvidenceAndObservedSalesHorizon()
    {
        await using var db = CreateDb();
        var saleAt = DateTime.UtcNow.Date.AddDays(-20);
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje { Id = 1, DatumProdaje = saleAt, IDObjekat = 1, DataOrigin = "access" });
        db.ProdajaStavke.Add(new ProdajaStavka { Id = 1, IdProdaja = 1, IdArtikal = 10 });
        db.DataImportBatches.Add(CompletedImport(DateTime.UtcNow.AddHours(-2)));
        await db.SaveChangesAsync();

        var meta = CreateMeta();
        await CreateService(db).ApplyAsync(meta, "imported", null, "signal");

        Assert.Equal("fresh", meta.DataFreshnessStatus);
        Assert.Equal("source_import_recent_success", meta.DataFreshnessReasonCode);
        Assert.StartsWith("access-import-batch:", meta.DataFreshnessEvidenceId);
        Assert.Equal(saleAt, meta.ObservedPeriodFromUtc);
        Assert.Equal(saleAt, meta.ObservedPeriodToUtc);
        Assert.NotNull(meta.DataFreshnessContextFingerprint);
        Assert.Equal(meta.DataFreshnessEvidenceAtUtc, meta.LastRefreshAtUtc);
    }

    [Fact]
    public async Task ApplyAsync_ExcludesSaleAtHalfOpenUpperBoundary()
    {
        await using var db = CreateDb();
        var fromUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var toExclusiveUtc = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);
        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje { Id = 1, DatumProdaje = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc), IDObjekat = 1, DataOrigin = "access" },
            new ProdajaZaglavlje { Id = 2, DatumProdaje = toExclusiveUtc, IDObjekat = 1, DataOrigin = "access" });
        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 1, IdProdaja = 1, IdArtikal = 10 },
            new ProdajaStavka { Id = 2, IdProdaja = 2, IdArtikal = 10 });
        db.DataImportBatches.Add(CompletedImport(DateTime.UtcNow.AddHours(-2)));
        await db.SaveChangesAsync();

        var meta = CreateMeta();
        meta.RequestedPeriodFromUtc = fromUtc;
        meta.RequestedPeriodToUtc = toExclusiveUtc;

        await CreateService(db).ApplyAsync(meta, "imported", null, "signal");

        Assert.Equal(new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc), meta.ObservedPeriodFromUtc);
        Assert.Equal(new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc), meta.ObservedPeriodToUtc);
    }

    [Fact]
    public async Task ApplyAsync_DoesNotUseGlobalImportToCertifyStoreFilteredRows()
    {
        await using var db = CreateDb();
        var saleAt = DateTime.UtcNow.Date.AddDays(-2);
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje { Id = 1, DatumProdaje = saleAt, IDObjekat = 7, DataOrigin = "access" });
        db.ProdajaStavke.Add(new ProdajaStavka { Id = 1, IdProdaja = 1, IdArtikal = 10 });
        db.DataImportBatches.Add(CompletedImport(DateTime.UtcNow.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var meta = CreateMeta();
        await CreateService(db).ApplyAsync(meta, "imported", 7, "recommendation");

        Assert.Equal("unknown", meta.DataFreshnessStatus);
        Assert.Equal("source_import_not_store_scoped", meta.DataFreshnessReasonCode);
        Assert.Null(meta.DataFreshnessEvidenceId);
        Assert.Null(meta.LastRefreshAtUtc);
        Assert.NotEqual(AnalyticsDecisionReadinessStates.DecisionReady, meta.DecisionReadiness?.State);
        Assert.Equal(saleAt, meta.ObservedPeriodFromUtc);
    }

    private static TrendplusDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TrendplusDbContext(options);
    }

    private static OperationsSourceFreshnessService CreateService(TrendplusDbContext db) =>
        new(db, NullLogger<OperationsSourceFreshnessService>.Instance);

    private static AnalyticsResponseMetaDto CreateMeta() => new()
    {
        Success = true,
        DataQualityStatus = "good",
        OperationsIntegrityFamily = OperationsAnalyticsIntegrityFamilies.SalesDashboard,
        RequestedPeriodFromUtc = DateTime.UtcNow.Date.AddDays(-30),
        RequestedPeriodToUtc = DateTime.UtcNow.Date,
        RequestedDataScope = "imported",
        DataScopeSource = SalesDataScopePolicy.Source
    };

    private static DataImportBatch CompletedImport(DateTime completedAtUtc) => new()
    {
        SourceSystem = "access",
        Status = "completed",
        IncludeAnalytics = true,
        StartedAtUtc = completedAtUtc.AddMinutes(-5),
        CompletedAtUtc = completedAtUtc,
        TotalErrors = 0,
        RowsRejected = 0
    };
}
