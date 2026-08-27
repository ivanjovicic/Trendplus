using Application.Analytics;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Infrastructure.Services.Analytics;
using Microsoft.EntityFrameworkCore;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class ProductDecisionCenterBuilderIntegrationTests
{
    [Fact]
    public async Task BuildProductDecisionCenter_ComputesDecisionFinancialAndConfidenceContracts()
    {
        var databaseName = $"product-decision-builder-{Guid.NewGuid():N}";
        await using var db = CreateDbContext(databaseName);
        var fromDate = PilotAnalyticsSeedPack.ProductDecisionFromUtc;
        var toDate = PilotAnalyticsSeedPack.ProductDecisionToUtc;
        PilotAnalyticsSeedPack.SeedProductDecisionCenter(db, fromDate, toDate);
        await db.SaveChangesAsync();

        var response = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
            db,
            fromDate,
            toDate,
            storeId: 1,
            supplierId: null,
            top: 50,
            dataScope: "all",
            CancellationToken.None);

        Assert.Equal(2, response.AnalyzedRows);
        Assert.Equal(2, response.TotalRows);
        Assert.Equal(2, response.Rows.Count);
        Assert.Equal(0, response.IgnoredRowsCount);

        var replenish = Assert.Single(response.Rows.Where(row => row.ProductId == 101));
        Assert.Equal("REPLENISH", replenish.RecommendationStatus);
        Assert.Equal("Dopuni", replenish.RecommendationLabel);
        Assert.NotEqual(replenish.RecommendationStatus, replenish.RecommendationLabel);
        Assert.Equal("Aktiviraj dopunu prema minimalnoj zalihi.", replenish.RecommendedAction);
        Assert.DoesNotContain("REPLENISH", replenish.RecommendedAction, StringComparison.Ordinal);
        Assert.Equal(3_000m, replenish.Revenue);
        Assert.Equal(30, replenish.UnitsSold);
        Assert.Equal(1m, replenish.VelocityUnitsPerDay);
        Assert.Equal(1_500m, replenish.MarginContribution);
        Assert.Equal(50m, replenish.MarginPct);
        Assert.Equal(100m, replenish.MarginCoveragePct);
        Assert.Equal(0, replenish.CurrentStock);
        Assert.Equal(5, replenish.StockGap);
        Assert.Equal(500m, replenish.LostSalesEstimate);
        Assert.Equal(0m, replenish.TrendPct);
        Assert.Equal("good", replenish.DataQualityStatus);
        Assert.Equal("product", replenish.SourceType);
        Assert.Equal("product:101", replenish.SourceKey);
        Assert.Equal("REPLENISH", replenish.RecommendationType);
        Assert.NotNull(replenish.RecommendationId);
        Assert.NotNull(replenish.WhyPanel);
        Assert.Equal("REPLENISH", replenish.WhyPanel.RecommendationStatus);
        Assert.Equal("Dopuni", replenish.WhyPanel.RecommendationLabel);
        Assert.Equal("recommendation_reason", replenish.WhyPanel.SummarySource);
        Assert.False(replenish.WhyPanel.SummaryFallbackUsed);
        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Issued, replenish.LifecycleState);
        Assert.False(replenish.LearningEligible);
        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Issued, replenish.WhyPanel.LifecycleState);
        Assert.False(replenish.WhyPanel.LearningEligible);
        Assert.Contains("acceptance_is_not_success", replenish.LearningEligibilityReasonCodes);
        Assert.NotEmpty(replenish.WhyPanel.ConfidenceBreakdown);

        var timelineFilter = AnalyticsActionTimelineFilterProjection.Filter(
            Array.Empty<Domain.Model.Analytics.AnalyticsActionItem>(),
            new DecisionTimelineFilterQuery(
                SourceType: replenish.SourceType,
                SourceKey: replenish.SourceKey,
                ProductId: replenish.ProductId,
                RecommendationType: replenish.RecommendationType,
                PeriodFromUtc: fromDate,
                PeriodToUtc: toDate));
        Assert.Equal(AnalyticsActionTimelineFilterProjection.EmptyReasonNoEvents, timelineFilter.EmptyReason);
        Assert.Contains(replenish.SourceKey!, timelineFilter.Scope.ScopeExplanation);
        Assert.Equal(replenish.RecommendationType, timelineFilter.Scope.RecommendationType);
        Assert.Contains(ProductDecisionReasoningHelper.RecommendationLabel(replenish.RecommendationType), timelineFilter.Scope.ScopeExplanation);
        Assert.DoesNotContain("Porodica: REPLENISH", timelineFilter.Scope.ScopeExplanation, StringComparison.Ordinal);
        Assert.NotEqual(replenish.RecommendationStatus, timelineFilter.Scope.ScopeExplanation);
        Assert.NotEmpty(replenish.WhyPanel.AlternativeRecommendations);
        Assert.NotEmpty(replenish.WhyPanel.DecisionTree);
        Assert.Contains(replenish.WhyPanel.DecisionTree, node => node.Code == "selected_branch" && node.IsSelected);
        Assert.NotNull(replenish.ConfidenceScore);
        Assert.InRange(replenish.ConfidenceScore!.Value, 60, 99);
        Assert.Contains("sales_velocity", replenish.PrimaryDrivers);
        Assert.Equal("stale", replenish.InputFreshnessStatus);
        Assert.NotEmpty(replenish.ConfidenceBreakdown);
        Assert.Contains(replenish.ConfidenceBreakdown, node => node.Code == "confidence_score");
        Assert.Contains(replenish.ConfidenceBreakdown, node => node.Code == "evidence_coverage");
        Assert.Contains(replenish.ConfidenceBreakdown, node => node.Code == "reliability_signal");
        Assert.Contains(replenish.ConfidenceBreakdown, node => node.Code == "freshness_signal");
        Assert.Contains(replenish.ConfidenceBreakdown, node => node.Code == "data_quality_signal");
        Assert.NotEmpty(replenish.EvidenceChain);
        Assert.Contains(replenish.EvidenceChain, node => node.Code == "selected_recommendation");
        Assert.Contains(replenish.EvidenceChain, node => node.Code == "sales_signal");
        Assert.Contains(replenish.EvidenceChain, node => node.Code == "expected_impact");
        Assert.Equal("absent", replenish.EvidenceSnapshotStatus);
        Assert.NotNull(replenish.EvidenceSnapshotPreview);
        Assert.Equal(1, replenish.EvidenceSnapshotPreview!.SchemaVersion);
        Assert.Equal(replenish.RecommendationId, replenish.EvidenceSnapshotPreview.RecommendationId);
        Assert.Equal(replenish.RecommendationType, replenish.EvidenceSnapshotPreview.RecommendationType);
        Assert.Equal(replenish.ConfidenceLevel, replenish.EvidenceSnapshotPreview.ConfidenceLevel);
        Assert.Equal(replenish.EvidenceChain.Count, replenish.EvidenceSnapshotPreview.EvidenceChain.Count);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.ReplenishNeeded, replenish.ReasonCodes);
        Assert.Equal(500m, replenish.ExpectedImpactRsd);
        Assert.Equal(14, replenish.ImpactWindowDays);
        Assert.False(string.IsNullOrWhiteSpace(replenish.ExplainabilityText));
        Assert.NotEmpty(replenish.AlternativeRecommendations);
        Assert.All(replenish.AlternativeRecommendations, node =>
        {
            Assert.NotEqual(replenish.RecommendationStatus, node.RecommendationStatus);
            Assert.False(string.IsNullOrWhiteSpace(node.Reason));
            Assert.False(string.IsNullOrWhiteSpace(node.WhyLowerRanked));
            Assert.NotEmpty(node.ReasonCodes);
        });

        var fixData = Assert.Single(response.Rows.Where(row => row.ProductId == 102));
        Assert.Equal("FIX_DATA", fixData.RecommendationStatus);
        Assert.Equal("critical", fixData.DataQualityStatus);
        Assert.InRange(fixData.ConfidencePct, 5, 35);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.MissingSupplier, fixData.ReasonCodes);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.MissingCost, fixData.ReasonCodes);
        Assert.Contains(ProductDecisionReasoningHelper.ReasonCodes.DataQualityBlocker, fixData.ReasonCodes);
        Assert.Contains("missing_cost", fixData.WarningCodes);
        Assert.Equal("critical", fixData.InputFreshnessStatus);
        Assert.Equal(400m, fixData.LostSalesEstimate);
        Assert.Null(fixData.ExpectedImpactRsd);
        Assert.Equal("Proveri podatke", fixData.RecommendationLabel);
        Assert.NotEqual(fixData.RecommendationStatus, fixData.RecommendationLabel);
        Assert.DoesNotContain("FIX_DATA", fixData.RecommendedAction, StringComparison.Ordinal);
        Assert.NotEmpty(fixData.ConfidenceBreakdown);
        Assert.Contains(fixData.ConfidenceBreakdown, node => node.Code == "confidence_score" && node.IsMissing);
        Assert.Contains(fixData.ConfidenceBreakdown, node => node.Code == "evidence_coverage" && node.ValueText == "Nedovoljna");
        Assert.Contains(fixData.ConfidenceBreakdown, node => node.Code == "data_quality_signal" && node.ValueText == "kritičan");
        Assert.Contains(fixData.EvidenceChain, node => node.Code == "warning:missing_cost");
        Assert.Contains(fixData.EvidenceChain, node => node.Code == "warning:expected_impact_denominator_missing");
        Assert.Contains(fixData.EvidenceChain, node => node.Code == "expected_impact" && node.IsMissing);
        Assert.NotNull(fixData.WhyPanel);
        Assert.Equal("FIX_DATA", fixData.WhyPanel.RecommendationStatus);
        Assert.Equal("Proveri podatke", fixData.WhyPanel.RecommendationLabel);
        Assert.Equal("recommendation_reason", fixData.WhyPanel.SummarySource);
        Assert.False(fixData.WhyPanel.SummaryFallbackUsed);
        Assert.NotEmpty(fixData.WhyPanel.EvidenceChain);
        Assert.NotEmpty(fixData.WhyPanel.AlternativeRecommendations);
        Assert.NotEmpty(fixData.WhyPanel.DecisionTree);
        Assert.Contains(fixData.WhyPanel.DecisionTree, node => node.Code == "data_quality_gate" && !node.IsSelected);
        Assert.NotEmpty(fixData.AlternativeRecommendations);
        Assert.Contains(fixData.AlternativeRecommendations, node => node.RecommendationStatus == "WATCH");
        Assert.All(fixData.AlternativeRecommendations, node =>
        {
            Assert.False(string.IsNullOrWhiteSpace(node.Reason));
            Assert.False(string.IsNullOrWhiteSpace(node.WhyLowerRanked));
        });

        Assert.Equal(1, response.Summary.ReplenishCount);
        Assert.Equal(1, response.Summary.BadDataCount);
        Assert.Equal(900m, response.Summary.LostSalesEstimate);
        Assert.Equal(0m, response.Summary.SlowStockCapital);
        Assert.Equal(ProductDecisionDenominatorScope.ReturnedRows, response.Summary.CountDenominatorScope);
        Assert.Equal(ProductDecisionDenominatorScope.AnalyzedRows, response.Summary.MoneyDenominatorScope);
        Assert.Equal(ProductDecisionDenominatorScope.HiddenByTopLimit, response.IgnoredRowsMeaning);
        Assert.NotNull(response.Meta);
        Assert.True(response.Meta!.Success);
        Assert.Equal("critical", response.Meta.DataQualityStatus);
        Assert.Null(response.Meta.ErrorCode);
    }

    [Fact]
    public async Task BuildProductDecisionCenter_TopLimitReportsAnalyzedAndIgnoredRowsHonestly()
    {
        var databaseName = $"product-decision-top-{Guid.NewGuid():N}";
        await using var db = CreateDbContext(databaseName);
        var fromDate = PilotAnalyticsSeedPack.ProductDecisionFromUtc;
        var toDate = PilotAnalyticsSeedPack.ProductDecisionToUtc;
        PilotAnalyticsSeedPack.SeedProductDecisionCenter(db, fromDate, toDate);
        await db.SaveChangesAsync();

        var response = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
            db,
            fromDate,
            toDate,
            storeId: 1,
            supplierId: null,
            top: 1,
            dataScope: "all",
            CancellationToken.None);

        Assert.Equal(2, response.AnalyzedRows);
        Assert.Equal(1, response.TotalRows);
        Assert.Single(response.Rows);
        Assert.Equal(1, response.IgnoredRowsCount);
        Assert.Equal(ProductDecisionDenominatorScope.HiddenByTopLimit, response.IgnoredRowsMeaning);
        Assert.Equal(ProductDecisionDenominatorScope.ReturnedRows, response.Summary.CountDenominatorScope);
        Assert.Equal(ProductDecisionDenominatorScope.AnalyzedRows, response.Summary.MoneyDenominatorScope);
        // FIX_DATA outranks REPLENISH, so top=1 returns FIX_DATA while money still includes all analyzed lost sales.
        Assert.Equal("FIX_DATA", response.Rows[0].RecommendationStatus);
        Assert.Equal(0, response.Summary.ReplenishCount);
        Assert.Equal(1, response.Summary.BadDataCount);
        Assert.Equal(900m, response.Summary.LostSalesEstimate);
        Assert.Equal(0m, response.Summary.SlowStockCapital);    }

    [Fact]
    public async Task BuildProductDecisionCenter_UnknownStoreReturnsExplicitEmptySuccessMeta()
    {
        var databaseName = $"product-decision-empty-{Guid.NewGuid():N}";
        await using var db = CreateDbContext(databaseName);
        var fromDate = PilotAnalyticsSeedPack.ProductDecisionFromUtc;
        var toDate = PilotAnalyticsSeedPack.ProductDecisionToUtc;
        PilotAnalyticsSeedPack.SeedProductDecisionCenter(db, fromDate, toDate);
        await db.SaveChangesAsync();

        var response = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
            db,
            fromDate,
            toDate,
            storeId: 9999,
            supplierId: null,
            top: 50,
            dataScope: "all",
            CancellationToken.None);

        Assert.Empty(response.Rows);
        Assert.Equal(0, response.TotalRows);
        Assert.Equal(0, response.AnalyzedRows);
        Assert.Equal(0, response.IgnoredRowsCount);
        Assert.NotNull(response.Meta);
        Assert.True(response.Meta!.Success);
        Assert.Equal("insufficient_data", response.Meta.DataQualityStatus);
        Assert.Equal("no_rows_for_period", response.Meta.EmptyReason);
        Assert.Null(response.Meta.ErrorCode);
        Assert.Equal(0, response.Summary.ReplenishCount);
        Assert.Equal(0m, response.Summary.LostSalesEstimate);
        Assert.DoesNotContain(response.Rows, row => row.RecommendationStatus == "REPLENISH");
        Assert.DoesNotContain(response.Rows, row => row.ExpectedImpactRsd == 0m);
    }

    [Fact]
    public async Task BuildProductDecisionCenter_DataScopeSeparatesImportedAndExistingProducts()
    {
        var databaseName = $"product-decision-scope-{Guid.NewGuid():N}";
        await using var db = CreateDbContext(databaseName);
        var fromDate = PilotAnalyticsSeedPack.ProductDecisionFromUtc;
        var toDate = PilotAnalyticsSeedPack.ProductDecisionToUtc;
        PilotAnalyticsSeedPack.SeedProductDecisionCenter(db, fromDate, toDate);
        await db.SaveChangesAsync();

        db.Artikli.Add(new Artikli
        {
            Id = 103,
            PLU = "IMP-103",
            Naziv = "Imported Model",
            IDDobavljac = 1,
            IDObjekat = 1,
            Kolicina = 3,
            MinimalnaKolicina = 1,
            NabavnaCena = 70m,
            Kategorija = "Patike",
            Boja = "Plava",
            Velicina = "41",
            DataOrigin = "access",
            UpdatedAt = toDate
        });
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 30,
            DatumProdaje = toDate.AddHours(12),
            IDObjekat = 1,
            DataOrigin = "access"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 31,
            IdProdaja = 30,
            IdArtikal = 103,
            Kolicina = 5,
            Cena = 140m,
            NabavnaCena = 70m
        });
        await db.SaveChangesAsync();

        var imported = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
            db,
            fromDate,
            toDate,
            storeId: 1,
            supplierId: null,
            top: 50,
            dataScope: "imported",
            CancellationToken.None);
        var existing = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
            db,
            fromDate,
            toDate,
            storeId: 1,
            supplierId: null,
            top: 50,
            dataScope: "existing",
            CancellationToken.None);

        var importedRow = Assert.Single(imported.Rows);
        Assert.Equal(103, importedRow.ProductId);
        Assert.Equal("imported", imported.RequestedDataScope);
        Assert.Equal("both", imported.ScopeAuthority);
        Assert.Contains("article_origin=Artikli.DataOrigin", imported.ScopeBreakdown);
        Assert.Contains("sale_origin=ProdajaZaglavlje.DataOrigin", imported.ScopeBreakdown);
        Assert.DoesNotContain(existing.Rows, row => row.ProductId == 103);
        Assert.Equal(2, existing.Rows.Count);
        Assert.Equal("existing", existing.RequestedDataScope);
        Assert.Equal("both", existing.ScopeAuthority);
    }

    private static TrendplusDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new TrendplusDbContext(options);
    }

}
