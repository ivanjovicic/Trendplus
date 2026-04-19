using Api.Services;
using Domain.Model;
using Domain.Model.Analytics;
using Domain.Model.Prodaja;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Trendplus2.Tests;

public class AnalyticsCostSnapshotServiceTests
{
    [Fact]
    public async Task GetHealthAsync_ReturnsFlagsStalenessAndFallbackBreakdown()
    {
        await using var db = CreateDbContext(nameof(GetHealthAsync_ReturnsFlagsStalenessAndFallbackBreakdown));

        var generatedAtUtc = DateTime.UtcNow.AddHours(-80);
        db.AnalyticsCostSnapshotBatches.Add(new AnalyticsCostSnapshotBatch
        {
            Id = 101,
            Scope = "access_origin",
            Status = "active",
            CreatedAtUtc = generatedAtUtc.AddMinutes(-10),
            GeneratedAtUtc = generatedAtUtc,
            ActivatedAtUtc = generatedAtUtc.AddMinutes(5),
            RowCount = 4321,
            CoveragePct = 72.5,
            NoCostPct = 4.5,
            GenerationDurationMs = 1400,
            DryRun = false,
            CreatedBy = "tester"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new AnalyticsSnapshotOptions
        {
            UseSnapshotCost = true,
            SnapshotAdminEnabled = true,
            ActiveBatchStaleAfterHours = 72
        });

        var health = await service.GetHealthAsync(CancellationToken.None);

        Assert.True(health.FeatureFlagEnabled);
        Assert.True(health.AdminEnabled);
        Assert.True(health.HasActiveBatch);
        Assert.Equal(101, health.ActiveBatchId);
        Assert.Equal("active", health.ActiveBatchStatus);
        Assert.False(health.ActiveBatchDryRun);
        Assert.Equal(4321, health.RowCount);
        Assert.Equal(72.5, health.CoveragePct);
        Assert.Equal(4.5, health.NoCostPct);
        Assert.Equal(23.0, health.RemainingLiveFallbackPct);
        Assert.True(health.IsStale);
        Assert.Equal(72, health.StaleAfterHours);
        Assert.Contains("premasio je prag", health.Warning ?? string.Empty);
    }

    [Fact]
    public async Task CompareSupplierAnalyticsAsync_ReturnsLegacyVsSnapshotDeltasAndRankShift()
    {
        await using var db = CreateDbContext(nameof(CompareSupplierAnalyticsAsync_ReturnsLegacyVsSnapshotDeltasAndRankShift));
        SeedComparisonFixture(db, batchId: 202);
        await db.SaveChangesAsync();

        var service = CreateService(db, new AnalyticsSnapshotOptions
        {
            UseSnapshotCost = false,
            SnapshotAdminEnabled = true,
            ActiveBatchStaleAfterHours = 72
        });

        var result = await service.CompareSupplierAnalyticsAsync(
            new AnalyticsCostSnapshotService.SnapshotAnalyticsComparisonRequest(
                BatchId: 202,
                SezonaId: null,
                FromDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                ToDate: new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc),
                StoreId: null,
                DataScope: "imported",
                Top: 10),
            CancellationToken.None);

        Assert.Equal("supplier-sales-stats", result.ReportKey);
        Assert.Equal(202, result.Batch.BatchId);
        Assert.Equal(2, result.EntityCount);
        Assert.Equal(2, result.ChangedEntityCount);
        Assert.Equal(170m, result.Legacy.MarginContribution);
        Assert.Equal(140m, result.Snapshot.MarginContribution);
        Assert.Equal(-30m, result.Delta.MarginContribution);
        Assert.Equal(100d, result.Legacy.LiveFallbackPct);
        Assert.Equal(100d, result.Snapshot.SnapshotCoveragePct);

        var supplierA = result.LargestDeltas.Single(x => x.EntityName == "Supplier A");
        var supplierB = result.LargestDeltas.Single(x => x.EntityName == "Supplier B");

        Assert.Equal(-50m, supplierA.MarginContributionDelta);
        Assert.Equal(1, supplierA.LegacyMarginContributionRank);
        Assert.Equal(2, supplierA.SnapshotMarginContributionRank);
        Assert.Equal(-1, supplierA.MarginContributionRankDelta);

        Assert.Equal(20m, supplierB.MarginContributionDelta);
        Assert.Equal(2, supplierB.LegacyMarginContributionRank);
        Assert.Equal(1, supplierB.SnapshotMarginContributionRank);
        Assert.Equal(1, supplierB.MarginContributionRankDelta);
    }

    [Fact]
    public async Task CompareShoeTypeAnalyticsAsync_UsesSameSnapshotBatchAndReturnsSnapshotCoverage()
    {
        await using var db = CreateDbContext(nameof(CompareShoeTypeAnalyticsAsync_UsesSameSnapshotBatchAndReturnsSnapshotCoverage));
        SeedComparisonFixture(db, batchId: 303);
        await db.SaveChangesAsync();

        var service = CreateService(db, new AnalyticsSnapshotOptions
        {
            UseSnapshotCost = true,
            SnapshotAdminEnabled = true,
            ActiveBatchStaleAfterHours = 72
        });

        var result = await service.CompareShoeTypeAnalyticsAsync(
            new AnalyticsCostSnapshotService.SnapshotAnalyticsComparisonRequest(
                BatchId: 303,
                SezonaId: null,
                FromDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                ToDate: new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc),
                StoreId: null,
                DataScope: "imported",
                Top: 10),
            CancellationToken.None);

        Assert.Equal("shoe-type-sales-stats", result.ReportKey);
        Assert.Equal(2, result.EntityCount);
        Assert.Equal(100d, result.Snapshot.SnapshotCoveragePct);
        Assert.Equal(0d, result.Snapshot.LiveFallbackPct);
        Assert.Contains(result.LargestDeltas, x => x.EntityName == "Patike");
        Assert.Contains(result.LargestDeltas, x => x.EntityName == "Sandale");
    }

    private static TrendplusDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new TrendplusDbContext(options);
    }

    private static AnalyticsCostSnapshotService CreateService(TrendplusDbContext db, AnalyticsSnapshotOptions options)
        => new(db, NullLogger<AnalyticsCostSnapshotService>.Instance, new TestOptionsMonitor<AnalyticsSnapshotOptions>(options));

    private static void SeedComparisonFixture(TrendplusDbContext db, long batchId)
    {
        db.Dobavljaci.AddRange(
            new Dobavljac { Id = 1, Naziv = "Supplier A", DataOrigin = "access" },
            new Dobavljac { Id = 2, Naziv = "Supplier B", DataOrigin = "access" });

        db.TipoviObuce.AddRange(
            new TipObuce { Id = 1, Naziv = "Patike", DataOrigin = "access" },
            new TipObuce { Id = 2, Naziv = "Sandale", DataOrigin = "access" });

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 11,
                Naziv = "A1",
                IDDobavljac = 1,
                IDTipObuce = 1,
                NabavnaCenaDin = 11m,
                DataOrigin = "access",
                UpdatedAt = DateTime.UtcNow
            },
            new Artikli
            {
                Id = 22,
                Naziv = "B1",
                IDDobavljac = 2,
                IDTipObuce = 2,
                NabavnaCenaDin = 7m,
                DataOrigin = "access",
                UpdatedAt = DateTime.UtcNow
            });

        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 1,
            DatumProdaje = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc),
            DataOrigin = "access"
        });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka
            {
                Id = 1001,
                IdProdaja = 1,
                IdArtikal = 11,
                Kolicina = 10,
                Cena = 20m,
                NabavnaCena = null
            },
            new ProdajaStavka
            {
                Id = 1002,
                IdProdaja = 1,
                IdArtikal = 22,
                Kolicina = 10,
                Cena = 15m,
                NabavnaCena = null
            });

        db.AnalyticsCostSnapshotBatches.Add(new AnalyticsCostSnapshotBatch
        {
            Id = batchId,
            Scope = "access_origin",
            Status = "ready",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            GeneratedAtUtc = DateTime.UtcNow.AddHours(-1),
            RowCount = 2,
            CoveragePct = 100,
            NoCostPct = 0,
            DryRun = false,
            CreatedBy = "tester"
        });

        db.AnalyticsSaleLineCostSnapshots.AddRange(
            new AnalyticsSaleLineCostSnapshot
            {
                Id = batchId * 10 + 1,
                BatchId = batchId,
                ProdajaStavkaId = 1001,
                ArtikalId = 11,
                ResolvedUnitCost = 16m,
                CostSource = (short)Application.Analytics.MarginCostSource.ProductFallbackRsd
            },
            new AnalyticsSaleLineCostSnapshot
            {
                Id = batchId * 10 + 2,
                BatchId = batchId,
                ProdajaStavkaId = 1002,
                ArtikalId = 22,
                ResolvedUnitCost = 5m,
                CostSource = (short)Application.Analytics.MarginCostSource.ProductFallbackRsd
            });
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => NoopDisposable.Instance;

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}