using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsReportsContractTests
{
    [Fact]
    public void SupplierDecisionReportBuilder_ProducesRichSuccessReportForValidDataset()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
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
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            new[]
            {
                CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m),
                CreateSupplierRow(2, "Beta", "EXPAND_SELECTIVELY", 74m, 76m, 410000m, 1100m),
                CreateSupplierRow(3, "Gamma", "PRICE_NEGOTIATE", 63m, 68m, 280000m, 980m)
            },
            0,
            0,
            new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.Meta?.Success);
        Assert.False(report.UsedFallback);
        Assert.True(report.RecommendationAllowed);
        Assert.NotEmpty(report.Kpis);
        Assert.NotEmpty(report.Sections);
        Assert.NotEmpty(report.Rows);
        Assert.Equal("supplier_decision", report.Type);
        Assert.Equal("Trendplus izveštaj dobavljača", report.Title);
        Assert.StartsWith("/analytics/supplier/report?", report.StableQueryUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void SupplierDecisionReportBuilder_ProducesFallbackWarningFor30dRequest()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
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
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            new[]
            {
                CreateSupplierRow(1, "Alpha", "EXPAND", 80m, 82m, 200000m, 400m, fromUtc, toUtc),
                CreateSupplierRow(2, "Beta", "HOLD", 70m, 71m, 150000m, 320m, fromUtc, toUtc),
                CreateSupplierRow(3, "Gamma", "PRICE_NEGOTIATE", 66m, 69m, 110000m, 250m, fromUtc, toUtc)
            },
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.UsedFallback);
        Assert.False(report.RecommendationAllowed);
        Assert.True(report.Meta?.IsPartial);
        Assert.Equal("FALLBACK_DATASET_USED", report.Meta?.WarningCode);
    }

    [Fact]
    public void SupplierDecisionReportBuilder_UsesEmptyMetaWithoutFakeZeroKpisWhenDatasetIsEmpty()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
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
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(Array.Empty<SupplierDecisionHubEndpoints.SupplierScoreRow>(), 0, 0, toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.Meta?.Success);
        Assert.Equal("no_data_in_period", report.Meta?.EmptyReason);
        Assert.Equal("insufficient_data", report.DataQualityStatus);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void SupplierDecisionErrorReport_UsesExplicitErrorMeta()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
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

        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionErrorReportResponse(filters, "SQL_TIMEOUT", "Timed out.", "corr-123");

        Assert.False(report.Meta?.Success);
        Assert.Equal("SQL_TIMEOUT", report.Meta?.ErrorCode);
        Assert.Empty(report.Kpis);
        Assert.Equal("insufficient_data", report.DataQualityStatus);
    }

    [Fact]
    public void PilotIntakeReportBuilder_ProducesRichSuccessReportForReadyDataset()
    {
        var intake = CreatePilotIntakeReport(85, AnalyticsResponseMetaFactory.Success("good"));
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        Assert.True(report.Meta?.Success);
        Assert.True(report.RecommendationAllowed);
        Assert.NotEmpty(report.Kpis);
        Assert.NotEmpty(report.Sections);
        Assert.Equal("pilot_intake", report.Type);
        Assert.StartsWith("/analytics/reports/pilot-intake?", report.StableQueryUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void PilotIntakeReportBuilder_UsesEmptyMetaWithoutFakeZeroKpisWhenNoImportExists()
    {
        var intake = CreatePilotIntakeReport(
            25,
            AnalyticsResponseMetaFactory.Empty("no_import", "Pilot intake izvestaj nema import batch u periodu.", "insufficient_data"));
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        Assert.True(report.Meta?.Success);
        Assert.Equal("no_import", report.Meta?.EmptyReason);
        Assert.False(report.RecommendationAllowed);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void PilotIntakeErrorReport_UsesExplicitErrorMeta()
    {
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeErrorReportResponse(period, null, null, "all", "corr-456");

        Assert.False(report.Meta?.Success);
        Assert.Equal("pilot_intake_report_error", report.Meta?.ErrorCode);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void ReportCacheKeysDifferentiateCriticalDimensions()
    {
        var fromUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var supplierBase = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, false, false, null, null, "all");
        var supplierScoped = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, false, false, 42, 7, "imported");
        var pilotBase = AnalyticsCacheKeys.PilotIntakeReport(fromUtc, toUtc, null, null, "all");
        var pilotScoped = AnalyticsCacheKeys.PilotIntakeReport(fromUtc, toUtc, 7, 42, "imported");

        Assert.Contains("analytics-report:supplier-decision:v1:", supplierBase);
        Assert.Contains("analytics-report:pilot-intake:v1:", pilotBase);
        Assert.NotEqual(supplierBase, supplierScoped);
        Assert.NotEqual(pilotBase, pilotScoped);
        Assert.Contains(":rv:1:", supplierBase);
        Assert.Contains(":rv:1:", pilotBase);

        var supplierCategory = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, "patike", null, null, null, false, false, null, null, "all");
        var supplierGender = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, "women", null, null, false, false, null, null, "all");
        var supplierSeason = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, 3, null, false, false, null, null, "all");
        var supplierRevenue = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, 1000m, false, false, null, null, "all");
        var supplierConfidence = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, true, false, null, null, "all");
        var supplierOos = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, false, true, null, null, "all");

        Assert.NotEqual(supplierBase, supplierCategory);
        Assert.NotEqual(supplierBase, supplierGender);
        Assert.NotEqual(supplierBase, supplierSeason);
        Assert.NotEqual(supplierBase, supplierRevenue);
        Assert.NotEqual(supplierBase, supplierConfidence);
        Assert.NotEqual(supplierBase, supplierOos);

        var supplierVersion2 = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, false, false, null, null, "all", reportCacheVersion: 2);
        var pilotVersion2 = AnalyticsCacheKeys.PilotIntakeReport(fromUtc, toUtc, null, null, "all", reportCacheVersion: 2);
        Assert.NotEqual(supplierBase, supplierVersion2);
        Assert.NotEqual(pilotBase, pilotVersion2);
    }

    [Fact(DisplayName = "SupplierDecisionReport_CacheHit_ReturnsCachedReportAndCorrelationMeta")]
    public async Task SupplierDecisionReport_CacheHit_ReturnsCachedReportAndCorrelationMeta()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
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
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m)],
            0,
            0,
            new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));
        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var cachedReport = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        var cache = new StubAnalyticsCacheService { CachedValue = cachedReport };
        var cacheAdmin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);
        var configuration = CreateTestConfiguration();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-supplier-report-cache-hit";

        var result = await SupplierDecisionHubEndpoints.HandleSupplierDecisionReportAsync(
            httpContext,
            configuration,
            cache,
            cacheAdmin,
            NullLoggerFactory.Instance,
            refreshStatusService: null!,
            fromDate: fromUtc,
            toDate: toUtc,
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<AnalyticsReportResponseDto>>(result);
        var report = Assert.IsType<AnalyticsReportResponseDto>(ok.Value);
        Assert.True(report.Meta?.Success);
        Assert.Equal("corr-supplier-report-cache-hit", report.Meta?.CorrelationId);
        Assert.Equal("supplier_decision", report.Type);
    }

    [Fact(DisplayName = "PilotIntakeReport_CacheHit_ReturnsCachedReportAndCorrelationMeta")]
    public async Task PilotIntakeReport_CacheHit_ReturnsCachedReportAndCorrelationMeta()
    {
        var period = (
            FromUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            ToExclusiveUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var intake = CreatePilotIntakeReport(85, AnalyticsResponseMetaFactory.Success("good"));
        var cachedReport = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        var cache = new StubAnalyticsCacheService { CachedValue = cachedReport };
        var cacheAdmin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-pilot-report-cache-hit";

        var result = await DataQualityEndpoints.HandlePilotIntakeReportAsync(
            httpContext,
            trendDb: null!,
            analyticsDb: null!,
            cache,
            cacheAdmin,
            NullLoggerFactory.Instance,
            healthService: null!,
            refreshStatusService: null!,
            fromDate: period.FromUtc.ToString("yyyy-MM-dd"),
            toDate: period.ToUtc.ToString("yyyy-MM-dd"),
            storeId: null,
            supplierId: null,
            scope: "all",
            dataScope: null,
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<AnalyticsReportResponseDto>>(result);
        var report = Assert.IsType<AnalyticsReportResponseDto>(ok.Value);
        Assert.True(report.Meta?.Success);
        Assert.Equal("corr-pilot-report-cache-hit", report.Meta?.CorrelationId);
        Assert.Equal("pilot_intake", report.Type);
    }

    [Fact(DisplayName = "PilotIntakeReport_CacheFailure_ReturnsErrorMetaWithoutFakeZeroPayload")]
    public async Task PilotIntakeReport_CacheFailure_ReturnsErrorMetaWithoutFakeZeroPayload()
    {
        var cache = new StubAnalyticsCacheService { GetException = new InvalidOperationException("cache unavailable") };
        var cacheAdmin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-pilot-cache-fail";

        var result = await DataQualityEndpoints.HandlePilotIntakeReportAsync(
            httpContext,
            trendDb: null!,
            analyticsDb: null!,
            cache,
            cacheAdmin,
            NullLoggerFactory.Instance,
            healthService: null!,
            refreshStatusService: null!,
            fromDate: "2026-06-01",
            toDate: "2026-06-30",
            storeId: null,
            supplierId: null,
            scope: "all",
            dataScope: null,
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<AnalyticsReportResponseDto>>(result);
        var report = Assert.IsType<AnalyticsReportResponseDto>(ok.Value);
        Assert.False(report.Meta?.Success);
        Assert.Equal("pilot_intake_report_error", report.Meta?.ErrorCode);
        Assert.Equal("corr-pilot-cache-fail", report.Meta?.CorrelationId);
        Assert.Empty(report.Kpis);
    }

    private static IConfiguration CreateTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DOTNET_ENVIRONMENT"] = "Development",
                ["ConnectionStrings:AnalyticsConnection"] = "Host=127.0.0.1;Port=5432;Database=analytics;Username=test;Password=test",
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=5432;Database=defaultdb;Username=test;Password=test"
            })
            .Build();

    private sealed class StubAnalyticsCacheService : IAnalyticsCacheService
    {
        public object? CachedValue { get; set; }
        public Exception? GetException { get; set; }

        public bool IsRedisAvailable => false;
        public bool IsRedisEnabled => false;

        public void SetRedisEnabled(bool enabled)
        {
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        {
            if (GetException is not null)
            {
                throw GetException;
            }

            return Task.FromResult(CachedValue as T);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        {
            CachedValue = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        {
            if (GetException is not null)
            {
                throw GetException;
            }

            if (CachedValue is T typed)
            {
                return typed;
            }

            var value = await factory();
            CachedValue = value;
            return value;
        }
    }

    private static SupplierDecisionHubEndpoints.SupplierScoreRow CreateSupplierRow(
        int supplierId,
        string supplierName,
        string recommendationCode,
        decimal confidenceScore,
        decimal reliabilityPct,
        decimal revenue,
        decimal units,
        DateTime? periodFrom = null,
        DateTime? periodTo = null)
    {
        var fromUtc = periodFrom ?? new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = periodTo ?? new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);

        return new SupplierDecisionHubEndpoints.SupplierScoreRow(
            supplierId,
            supplierName,
            fromUtc,
            toUtc,
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
            ["stable_margin", "repeat_winner"]);
    }

    private static DataQualityEndpoints.PilotDataQualityIntakeReportDto CreatePilotIntakeReport(
        int readinessScore,
        AnalyticsResponseMetaDto meta)
    {
        return new DataQualityEndpoints.PilotDataQualityIntakeReportDto(
            new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            "all",
            null,
            null,
            new DateTime(2026, 6, 29, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 4, 0, 0, DateTimeKind.Utc),
            "fresh",
            readinessScore,
            readinessScore >= 90 ? "excellent" : readinessScore >= 70 ? "good" : readinessScore >= 40 ? "warning" : "critical",
            readinessScore >= 90 ? "Spremno" : readinessScore >= 70 ? "Upotrebljivo uz upozorenja" : "Ograničeno",
            new DataQualityEndpoints.PilotDataQualityIntakeLoadedDataDto(1200, 45000, 9300, 48, 12, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)),
            new DataQualityEndpoints.PilotDataQualityIntakeIssuesDto(4, 8, 15, 9, 11, 2, 1, 3, 1),
            new DataQualityEndpoints.PilotDataQualityIntakeImpactDto(0.04d, 0.01d, 12, 5, 18),
            ["Povezi dobavljace", "Pokreni osvezavanje analitike"],
            meta);
    }
}
