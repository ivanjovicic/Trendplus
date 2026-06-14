using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsSalesReadinessRegressionTests
{
    [Fact]
    public void Scorecard_30d_NoRows_DoesNotPretend180dAndKeepsInsufficientMeta()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = CreateFilters(fromUtc, toUtc);
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset([], 0, 0, toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.Equal(fromUtc, report.PeriodFrom);
        Assert.Equal(toUtc, report.PeriodTo);
        Assert.True(report.Meta?.Success);
        Assert.Equal("insufficient_data", report.Meta?.DataQualityStatus);
        Assert.Equal("no_data_in_period", report.Meta?.EmptyReason);
    }

    [Fact]
    public void UsedFallback_True_AlwaysDisablesRecommendation()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = CreateFilters(fromUtc, toUtc);
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "EXPAND", 80m, 82m, 200000m, 400m, fromUtc, toUtc)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.UsedFallback);
        Assert.False(report.RecommendationAllowed);
    }

    [Fact]
    public void EmptyDataset_ReturnsSuccessTrueWithEmptyReason()
    {
        var meta = AnalyticsResponseMetaFactory.Empty("no_rows_for_period", "Nema podataka.");
        Assert.True(meta.Success);
        Assert.Equal("no_rows_for_period", meta.EmptyReason);
    }

    [Fact]
    public void SqlOrMissingView_ReturnsErrorMeta_NotFakeZero()
    {
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionErrorReportResponse(
            CreateFilters(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)),
            "supplier_decision_sql_error",
            "mv_supplier_decision_summary nije dostupna.",
            "corr-sql-1");

        Assert.False(report.Meta?.Success);
        Assert.Equal("supplier_decision_sql_error", report.Meta?.ErrorCode);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void DashboardBootstrap_PartialData_ReturnsWarningMeta()
    {
        var dto = new AnalyticsDashboardBootstrapDto
        {
            Errors = ["Dashboard bootstrap partial"],
            Meta = AnalyticsResponseMetaFactory.Warning("ANALYTICS_PARTIAL_DATA", "Delimični podaci.")
        };

        Assert.True(dto.Meta.Success);
        Assert.True(dto.Meta.IsPartial);
        Assert.Equal("ANALYTICS_PARTIAL_DATA", dto.Meta.WarningCode);
    }

    [Fact]
    public void ProductDecision_NoRows_ReturnsEmptyMeta()
    {
        var dto = new ProductDecisionCenterResponseDto
        {
            Rows = [],
            TotalRows = 0,
            Meta = AnalyticsResponseMetaFactory.Empty("no_rows_for_period", "Nema kandidata.")
        };

        Assert.True(dto.Meta.Success);
        Assert.Equal("no_rows_for_period", dto.Meta.EmptyReason);
        Assert.Empty(dto.Rows);
    }

    [Fact]
    public void InventoryInsights_ExceptionPath_UsesErrorMeta()
    {
        var response = new InventoryInsightsDto(
            0,
            0m,
            [],
            [],
            [],
            [],
            AnalyticsResponseMetaFactory.Error("inventory_cached_insights_error", "Inventory uvidi nisu dostupni.", "corr-inv-1"));

        Assert.False(response.Meta?.Success);
        Assert.Equal("inventory_cached_insights_error", response.Meta?.ErrorCode);
    }

    [Fact]
    public void DataQualityTopOffenders_Empty_ReturnsEmptyMeta()
    {
        var response = new DataQualityEndpoints.DataQualityTopOffendersResponse(
            "missingSupplier",
            10,
            0,
            [],
            AnalyticsResponseMetaFactory.Empty("no_top_offenders", "Nema top offender zapisa."));

        Assert.True(response.Meta?.Success);
        Assert.Equal("no_top_offenders", response.Meta?.EmptyReason);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task ErrorRecord_LongMessage_DoesNotThrow()
    {
        var tests = new DbErrorStoreTests();
        await tests.SaveAsync_TruncatesLongFieldsToSchemaLimits();
    }

    [Fact]
    public async Task WorkerRefresh_NoHistory_ReturnsUnknown()
    {
        var tests = new AnalyticsRefreshStatusServiceTests();
        await tests.GetStatus_ReturnsUnknown_WhenNoRefreshHistoryExists();
    }

    private static SupplierDecisionHubEndpoints.SupplierDecisionHubFilters CreateFilters(DateTime fromUtc, DateTime toUtc)
        => new(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");

    private static SupplierDecisionHubEndpoints.SupplierScoreRow CreateSupplierRow(
        int supplierId,
        string supplierName,
        string recommendationCode,
        decimal confidenceScore,
        decimal reliabilityPct,
        decimal revenue,
        decimal units,
        DateTime periodFrom,
        DateTime periodTo)
    {
        return new SupplierDecisionHubEndpoints.SupplierScoreRow(
            supplierId,
            supplierName,
            periodFrom,
            periodTo,
            revenue,
            units,
            0.64m,
            0.57m,
            0.36m,
            0.31m,
            0.18m,
            145000m,
            0.44m,
            28m,
            24m,
            0.05m,
            62m,
            78m,
            "Stabilan signal dobavljača.",
            "Marža",
            "Sell-through",
            "Markdown",
            81m,
            recommendationCode,
            confidenceScore,
            false,
            reliabilityPct,
            "good",
            "Signal podržava prikazanu preporuku.",
            ["stable_margin"]);
    }
}
