using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class SupplierDecisionHubContractTests
{
    private static readonly DateTime GeneratedAtUtc = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryCreateFilters_RejectsInvertedDateRange()
    {
        var valid = SupplierDecisionHubEndpoints.TryCreateFilters(
            fromDate: new DateTime(2026, 7, 2),
            toDate: new DateTime(2026, 7, 1),
            category: null,
            gender: null,
            seasonId: null,
            minRevenue: null,
            onlyHighConfidence: false,
            excludeOosBeforeMarkdown: false,
            supplierId: null,
            storeId: null,
            dataScope: "all",
            out var filters,
            out var validationErrors);

        Assert.False(valid);
        Assert.Null(filters);
        Assert.NotNull(validationErrors);
        Assert.Contains("fromDate", validationErrors!.Keys);
        Assert.Contains("earlier than or equal", validationErrors["fromDate"].Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreateFilters_RejectsNegativeMinimumRevenue()
    {
        var valid = SupplierDecisionHubEndpoints.TryCreateFilters(
            fromDate: null,
            toDate: null,
            category: null,
            gender: null,
            seasonId: null,
            minRevenue: -0.01m,
            onlyHighConfidence: false,
            excludeOosBeforeMarkdown: false,
            supplierId: null,
            storeId: null,
            dataScope: "all",
            out var filters,
            out var validationErrors);

        Assert.False(valid);
        Assert.Null(filters);
        Assert.NotNull(validationErrors);
        Assert.Contains("minRevenue", validationErrors!.Keys);
    }

    [Fact]
    public void TryCreateFilters_NormalizesDatesWhitespaceAndUnknownScope()
    {
        var valid = SupplierDecisionHubEndpoints.TryCreateFilters(
            fromDate: new DateTime(2026, 1, 2, 14, 30, 0, DateTimeKind.Unspecified),
            toDate: new DateTime(2026, 3, 31, 22, 15, 0, DateTimeKind.Utc),
            category: "  Patike  ",
            gender: " Ženski ",
            seasonId: 4,
            minRevenue: 1_000m,
            onlyHighConfidence: true,
            excludeOosBeforeMarkdown: true,
            supplierId: 17,
            storeId: 2,
            dataScope: "not-supported",
            out var filters,
            out var validationErrors);

        Assert.True(valid);
        Assert.Null(validationErrors);
        Assert.NotNull(filters);
        Assert.Equal(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), filters!.FromDate);
        Assert.Equal(new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), filters.ToDate);
        Assert.True(filters.HasExplicitDateRange);
        Assert.Equal("Patike", filters.Category);
        Assert.Equal("Ženski", filters.Gender);
        Assert.Equal("all", filters.DataScope);
        Assert.True(filters.OnlyHighConfidence);
        Assert.True(filters.ExcludeOosBeforeMarkdown);
    }

    [Fact]
    public void BuildSummaryResponse_UsesRevenueAndUnitWeightedMetrics()
    {
        var filters = Filters90Days();
        var dataset = Dataset(
            Row(1, "Grow", revenue: 1_000m, units: 100m, fullPriceRevenueShare: 0.80m, fullPriceSellthrough: 0.60m,
                markdownRevenueShare: 0.20m, preMarkdownMarginPct: 0.30m, unsoldStockValue: 100m,
                qualityIndex: 90m, recommendationCode: "EXPAND", confidence: 90m),
            Row(2, "Risk", revenue: 3_000m, units: 10m, fullPriceRevenueShare: 0.20m, fullPriceSellthrough: 0.10m,
                markdownRevenueShare: 0.80m, preMarkdownMarginPct: 0.10m, unsoldStockValue: 900m,
                qualityIndex: 40m, recommendationCode: "ASSORTMENT_REDUCE", confidence: 40m, stockRiskScore: 95m),
            Row(3, "Hold", revenue: 1_000m, units: 20m, fullPriceRevenueShare: 0.50m, fullPriceSellthrough: 0.40m,
                markdownRevenueShare: 0.50m, preMarkdownMarginPct: 0.20m, unsoldStockValue: 100m,
                qualityIndex: 70m, recommendationCode: "HOLD", confidence: 75m));

        var response = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);

        Assert.Equal(3, response.SupplierCount);
        Assert.Equal(0.38m, response.FullPriceRevenueShare);
        Assert.Equal(0.5308m, response.FullPriceSellthrough);
        Assert.Equal(0.62m, response.MarkdownRevenueShare);
        Assert.Equal(0.2105m, response.PreMarkdownMarginPct);
        Assert.Equal(1_100m, response.CapitalAtRisk);

        Assert.Equal("Grow", response.TopGrowSuppliers.First().SupplierName);
        Assert.Equal("Risk", response.TopRiskSuppliers.First().SupplierName);
        Assert.Equal("good", response.TrustMetadata!.DataCoverageStatus);
        Assert.True(response.TrustMetadata.RecommendationAllowed);
        Assert.False(response.TrustMetadata.UsedFallback);
        Assert.True(response.Meta!.Success);
        Assert.Equal("good", response.Meta.DataQualityStatus);
    }

    [Fact]
    public void BuildSummaryResponse_ThirtyDayFallbackIsExplicitAndBlocksRecommendation()
    {
        var filters = Filters(
            from: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc));
        var dataset = Dataset(
            Row(1, "A", recommendationCode: "EXPAND", confidence: 90m),
            Row(2, "B", recommendationCode: "HOLD", confidence: 80m),
            Row(3, "C", recommendationCode: "HOLD", confidence: 75m));

        var response = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);

        Assert.NotNull(response.TrustMetadata);
        Assert.True(response.TrustMetadata!.UsedFallback);
        Assert.Equal("30d", response.TrustMetadata.RequestedDataset);
        Assert.Equal("90d", response.TrustMetadata.EffectiveDataset);
        Assert.Equal("no_mv_30d", response.TrustMetadata.FallbackReasonCode);
        Assert.False(response.TrustMetadata.RecommendationAllowed);
        Assert.Equal("warning", response.TrustMetadata.DataCoverageStatus);
        Assert.Contains("30d", response.DataNote ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Meta!.IsPartial);
        Assert.Equal("FALLBACK_DATASET_USED", response.Meta.WarningCode);
    }

    [Fact]
    public void BuildSummaryResponse_LowSampleSizeBlocksRecommendationEvenWithoutFallback()
    {
        var filters = Filters90Days();
        var response = SupplierDecisionHubEndpoints.BuildSummaryResponse(
            Dataset(
                Row(1, "A", recommendationCode: "EXPAND", confidence: 90m),
                Row(2, "B", recommendationCode: "HOLD", confidence: 80m)),
            filters);

        Assert.False(response.TrustMetadata!.UsedFallback);
        Assert.Equal("warning", response.TrustMetadata.DataCoverageStatus);
        Assert.False(response.TrustMetadata.RecommendationAllowed);
        Assert.Equal(2, response.TrustMetadata.RowCount);
        Assert.Equal("RECOMMENDATION_GATED", response.Meta!.WarningCode);
    }

    [Fact]
    public void BuildSummaryResponse_MissingSupplierNameIsCriticalAndBlocksRecommendation()
    {
        var filters = Filters90Days();
        var response = SupplierDecisionHubEndpoints.BuildSummaryResponse(
            Dataset(
                Row(1, "Nepoznat dobavljač", supplierNameMissing: true),
                Row(2, "B"),
                Row(3, "C")),
            filters);

        Assert.Equal("critical", response.TrustMetadata!.DataCoverageStatus);
        Assert.Equal(1, response.TrustMetadata.MissingSupplierNameCount);
        Assert.False(response.TrustMetadata.RecommendationAllowed);
        Assert.Equal("critical", response.Meta!.DataQualityStatus);
        Assert.Equal("RECOMMENDATION_GATED", response.Meta.WarningCode);
    }

    [Fact]
    public void BuildSummaryResponse_EmptyDatasetReturnsExplicitInsufficientDataContract()
    {
        var response = SupplierDecisionHubEndpoints.BuildSummaryResponse(
            new SupplierDecisionHubEndpoints.SupplierRowsDataset(
                Rows: [],
                ZeroRevenueRowsExcludedCount: 0,
                IgnoredRowCount: 0,
                GeneratedAtUtc: GeneratedAtUtc),
            Filters90Days());

        Assert.Equal(0, response.SupplierCount);
        Assert.Empty(response.TopGrowSuppliers);
        Assert.Empty(response.TopRiskSuppliers);
        Assert.False(response.TrustMetadata!.HasData);
        Assert.False(response.TrustMetadata.RecommendationAllowed);
        Assert.Equal("insufficient_data", response.TrustMetadata.DataCoverageStatus);
        Assert.True(response.Meta!.Success);
        Assert.Equal("insufficient_data", response.Meta.DataQualityStatus);
        Assert.Equal("no_data_in_period", response.Meta.EmptyReason);
    }

    [Fact]
    public void BuildSupplierDecisionReportResponse_PreservesTrustFreshnessAndStableFilters()
    {
        var filters = Filters90Days(supplierId: 7, storeId: 3, dataScope: "existing");
        var dataset = Dataset(
            Row(7, "Grow", recommendationCode: "EXPAND", confidence: 92m),
            Row(8, "Hold", recommendationCode: "HOLD", confidence: 80m),
            Row(9, "Risk", recommendationCode: "PRICE_NEGOTIATE", confidence: 70m));
        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var refresh = new SupplierDecisionHubEndpoints.ReportRefreshInfo(
            LastRefreshAtUtc: GeneratedAtUtc.AddHours(-30),
            DataFreshnessStatus: "stale",
            WarningMessage: "Supplier agregati kasne.");

        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(
            summary,
            dataset,
            filters,
            refresh);

        Assert.Equal("supplier_decision", report.Type);
        Assert.Equal("supplier-decision", report.ReportType);
        Assert.Contains("supplierId=7", report.StableQueryUrl, StringComparison.Ordinal);
        Assert.Contains("storeId=3", report.StableQueryUrl, StringComparison.Ordinal);
        Assert.Contains("scope=existing", report.StableQueryUrl, StringComparison.Ordinal);
        Assert.True(report.RecommendationAllowed);
        Assert.False(report.UsedFallback);
        Assert.Equal("stale", report.DataFreshnessStatus);
        Assert.Contains("Supplier agregati kasne.", report.Warnings);
        Assert.NotEmpty(report.Kpis);
        Assert.NotEmpty(report.Sections);
        Assert.NotEmpty(report.RecommendedActions);
        Assert.Contains(report.Payload.Filters, item => item.Key == "supplier" && item.Value == "7");
        Assert.Contains(report.Payload.Filters, item => item.Key == "store" && item.Value == "3");
        Assert.True(report.Meta!.Success);
        Assert.True(report.Meta.IsPartial);
        Assert.Equal("STALE_REFRESH", report.Meta.WarningCode);
        Assert.Equal("warning", report.Meta.DataQualityStatus);
    }

    [Fact]
    public void BuildSupplierDecisionErrorReportResponse_DoesNotFabricateBusinessResults()
    {
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionErrorReportResponse(
            Filters90Days(supplierId: 99),
            errorCode: "supplier_decision_schema_missing",
            message: "Supplier scorecard nije dostupan.",
            correlationId: "supplier-contract-test");

        Assert.False(report.RecommendationAllowed);
        Assert.False(report.UsedFallback);
        Assert.Empty(report.Kpis);
        Assert.Empty(report.RecommendedActions);
        Assert.Single(report.Sections);
        Assert.Equal("report-status", report.Sections[0].Key);
        Assert.Equal("insufficient_data", report.DataQualityStatus);
        Assert.NotNull(report.Meta);
        Assert.False(report.Meta!.Success);
        Assert.Equal("supplier_decision_schema_missing", report.Meta.ErrorCode);
        Assert.Equal("supplier-contract-test", report.Meta.CorrelationId);
        Assert.Contains(report.Rows, row => row.Section == "Status" && row.Value == "Greška");
    }

    private static SupplierDecisionHubEndpoints.SupplierDecisionHubFilters Filters90Days(
        int? supplierId = null,
        int? storeId = null,
        string dataScope = "all") =>
        Filters(
            from: new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            supplierId: supplierId,
            storeId: storeId,
            dataScope: dataScope);

    private static SupplierDecisionHubEndpoints.SupplierDecisionHubFilters Filters(
        DateTime from,
        DateTime to,
        int? supplierId = null,
        int? storeId = null,
        string dataScope = "all") =>
        new(
            FromDate: from,
            ToDate: to,
            HasExplicitDateRange: true,
            Category: null,
            Gender: null,
            SeasonId: null,
            MinRevenue: null,
            OnlyHighConfidence: false,
            ExcludeOosBeforeMarkdown: false,
            SupplierId: supplierId,
            StoreId: storeId,
            DataScope: dataScope);

    private static SupplierDecisionHubEndpoints.SupplierRowsDataset Dataset(
        params SupplierDecisionHubEndpoints.SupplierScoreRow[] rows) =>
        new(
            Rows: rows,
            ZeroRevenueRowsExcludedCount: 0,
            IgnoredRowCount: 0,
            GeneratedAtUtc: GeneratedAtUtc);

    private static SupplierDecisionHubEndpoints.SupplierScoreRow Row(
        int supplierId,
        string supplierName,
        decimal revenue = 1_000m,
        decimal units = 10m,
        decimal fullPriceRevenueShare = 0.60m,
        decimal fullPriceSellthrough = 0.50m,
        decimal markdownRevenueShare = 0.40m,
        decimal preMarkdownMarginPct = 0.25m,
        decimal unsoldStockValue = 100m,
        decimal qualityIndex = 75m,
        string recommendationCode = "HOLD",
        decimal confidence = 75m,
        decimal stockRiskScore = 20m,
        bool supplierNameMissing = false) =>
        new(
            SupplierId: supplierId,
            SupplierName: supplierName,
            PeriodFrom: new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Revenue: revenue,
            Units: units,
            FullPriceRevenueShare: fullPriceRevenueShare,
            FullPriceSellthrough: fullPriceSellthrough,
            MarkdownRevenueShare: markdownRevenueShare,
            PreMarkdownMarginPct: preMarkdownMarginPct,
            DeadStockRate: 0.10m,
            UnsoldStockValue: unsoldStockValue,
            RepeatWinnerRate: 0.40m,
            MarkdownDependencyScore: markdownRevenueShare * 100m,
            StockRiskScore: stockRiskScore,
            ReturnRate: 0.02m,
            CategoryFocusScore: 55m,
            MlSupplierScore: qualityIndex,
            AiExplanation: "Stabilan signal.",
            TopFeature1: "margin",
            TopFeature2: "sellthrough",
            TopFeature3: "stock",
            SupplierQualityIndex: qualityIndex,
            RecommendationCode: recommendationCode,
            ConfidenceScore: confidence,
            SupplierNameMissing: supplierNameMissing,
            ReliabilityPct: confidence,
            DataQualityStatus: confidence >= 70m ? "good" : confidence >= 45m ? "warning" : "critical",
            StatusReason: "Test signal",
            ReasonCodes: [recommendationCode.ToLowerInvariant()]);
}
