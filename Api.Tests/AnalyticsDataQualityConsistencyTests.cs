using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Domain.Model;
using Domain.Model.Prodaja;
using Microsoft.EntityFrameworkCore;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class AnalyticsDataQualityConsistencyTests
{
    [Fact]
    public void HealthScore_NoRevenueEvidence_IsInsufficientAndNotGreen()
    {
        var result = DataQualityEndpoints.BuildScore(
            new AnalyticsDataQualityHealthSnapshot
            {
                TotalRevenue = 0m,
                HasRevenueEvidence = false,
                MissingCostRevenueSharePct = null,
                UnknownSupplierRevenueSharePct = null,
            },
            new AnalyticsDataQualityHealthOptions());

        Assert.Equal(0, result.Value);
        Assert.Equal("insufficient_data", result.Status);
        Assert.Contains("Nema dovoljno", result.Summary, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void MissingCostRule_TreatsNullAndNonPositiveValuesAsMissing()
    {
        Assert.True(AnalyticsDataQualityHealthService.IsMissingCost(null));
        Assert.True(AnalyticsDataQualityHealthService.IsMissingCost(0m));
        Assert.True(AnalyticsDataQualityHealthService.IsMissingCost(-1m));
        Assert.False(AnalyticsDataQualityHealthService.IsMissingCost(12.50m));
    }

    [Theory]
    [InlineData(null, false, true)]
    [InlineData(0, true, true)]
    [InlineData(42, false, true)]
    [InlineData(42, true, false)]
    public void MissingSupplierRule_TreatsNullZeroAndBrokenReferencesAsMissing(int? supplierId, bool supplierExists, bool expected)
    {
        Assert.Equal(expected, AnalyticsDataQualityHealthService.IsMissingSupplier(supplierId, supplierExists));
    }

    [Fact]
    public async Task CaptureAsync_TreatsNonPositiveCostAndMissingSupplierAsRevenueRisk()
    {
        await using var db = new TrendplusDbContext(new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase($"analytics-data-quality-consistency-{Guid.NewGuid():N}")
            .Options);

        db.Artikli.Add(new Artikli
        {
            Id = 1,
            Naziv = "Artikal sa nulom cenom",
            IDDobavljac = null,
            NabavnaCena = 0m,
            DataOrigin = "existing",
            UpdatedAt = DateTime.UtcNow
        });
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 1,
            DatumProdaje = DateTime.UtcNow,
            DataOrigin = "existing"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 1,
            IdProdaja = 1,
            IdArtikal = 1,
            Kolicina = 1,
            Cena = 100m,
            NabavnaCena = null
        });
        await db.SaveChangesAsync();

        var snapshot = await new AnalyticsDataQualityHealthService(db)
            .CaptureAsync(30, "all", CancellationToken.None);

        Assert.Equal(100m, snapshot.TotalRevenue);
        Assert.Equal(100m, snapshot.MissingCostRevenue);
        Assert.Equal(100d, snapshot.MissingCostRevenueSharePct);
        Assert.Equal(100m, snapshot.UnknownSupplierRevenue);
        Assert.Equal(100d, snapshot.UnknownSupplierRevenueSharePct);
    }

    [Fact]
    public void IntakeScore_CannotStayGreen_WhenSignalIsInsufficientAndImportIsCritical()
    {
        var health = new AnalyticsDataQualityHealthSnapshot
        {
            HasRevenueEvidence = true,
            TotalRevenue = 100_000m,
            MissingCostRevenueSharePct = 0d,
            UnknownSupplierRevenueSharePct = 0d,
            OrphanArticleCount = 0
        };

        var score = DataQualityEndpoints.CalculateIntakeScore(
            totalArticles: 1_000,
            missingSupplierCount: 0,
            missingCostCount: 0,
            missingCategoryCount: 0,
            missingSizeCount: 0,
            missingColorCount: 0,
            missingSupplierNameCount: 0,
            duplicateSkuCount: 0,
            saleWithoutArticleCount: 0,
            zeroOrNegativePriceCount: 0,
            ignoredRows: 0,
            rowsRead: 1_000,
            insufficientSignalCount: 900,
            freshnessStatus: "critical",
            health);

        Assert.InRange(score, 0, 69);
        Assert.Equal("critical", DataQualityEndpoints.ResolveReadiness(score, "critical", 900, 1_000).Code);
    }

    [Fact]
    public void DashboardQualityStatus_UsesWorstCompletenessOrFreshnessState()
    {
        var response = new AnalyticsDashboardBootstrapDto
        {
            ValidationCompleteness = new DashboardValidationEndpointDto
            {
                Status = "critical",
                Message = "Nizak completeness.",
                Score = 0m,
                TotalSku = 100,
                AffectedSku = 100
            },
            ValidationFreshness = new DashboardValidationEndpointDto
            {
                Status = "good",
                Message = "Podaci su svezi."
            }
        };

        Assert.Equal("critical", CachedAnalyticsEndpoints.ResolveDashboardDataQualityStatus(response));
    }

    [Fact]
    public void HealthScoreSummary_DoesNotClaimUniversalDecisionReliability()
    {
        var result = DataQualityEndpoints.BuildScore(
            new AnalyticsDataQualityHealthSnapshot
            {
                HasRevenueEvidence = true,
                TotalRevenue = 100_000m,
                MissingCostRevenueSharePct = 0d,
                UnknownSupplierRevenueSharePct = 0d,
                OrphanArticleCount = 0
            },
            new AnalyticsDataQualityHealthOptions());

        Assert.DoesNotContain("pouzdan za odluke", result.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
