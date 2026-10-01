using Xunit;

namespace Api.Tests;

public sealed class SupplierDecisionSchemaSqlTests
{
    [Fact]
    public void SupplierDecisionSqlDefinesCompleteStackInDependencyOrder()
    {
        var sql = ReadRepoFile("Database/Migrations/018_AddSupplierDecisionHubViews.sql");

        AssertInOrder(
            sql,
            "CREATE OR REPLACE VIEW vw_supplier_fullprice_signals AS",
            "CREATE OR REPLACE VIEW vw_supplier_markdown_dependency AS",
            "CREATE OR REPLACE VIEW vw_supplier_decision_score AS",
            "CREATE OR REPLACE VIEW vw_supplier_recommendations AS",
            "CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_markdown_dependency_cache AS",
            "CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache AS",
            "CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_recommendations_cache AS");
    }

    [Fact]
    public void SupplierDecisionMarkdownCacheIndexUsesPostgresExpressionIndexSyntax()
    {
        var sql = ReadRepoFile("Database/Migrations/018_AddSupplierDecisionHubViews.sql");
        var normalizedSql = NormalizeWhitespace(sql);

        Assert.Contains(
            "ON mv_supplier_markdown_dependency_cache (supplier_id, (COALESCE(category, '')))",
            normalizedSql);
        Assert.DoesNotContain(
            "ON mv_supplier_markdown_dependency_cache (supplier_id, COALESCE(category, ''))",
            normalizedSql);
    }

    [Fact]
    public void VendorSalesNivelacijaZeroBaselinePercentContractKeepsExplicitSentinelValues()
    {
        var sql = ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");

        Assert.Contains("WHEN pre.pre_qty = 0 AND COALESCE(post.post_qty, 0) > 0 THEN NULL", sql);
        Assert.Contains("WHEN pre.pre_revenue = 0 AND COALESCE(post.post_revenue, 0) > 0 THEN NULL", sql);
        Assert.DoesNotContain("WHEN pre.pre_revenue = 0 AND post.post_revenue > 0 THEN 100", sql);
        Assert.Contains("ELSE ROUND(((post.post_qty - pre.pre_qty) / NULLIF(pre.pre_qty, 0)) * 100, 2)", sql);
        Assert.Contains("ELSE ROUND(((post.post_revenue - pre.pre_revenue) / NULLIF(pre.pre_revenue, 0)) * 100, 2)", sql);
    }

    [Fact]
    public void VendorSalesNivelacijaSemanticColumnsExposeZeroBaselineAsExplicitNullContract()
    {
        var sql = ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");

        Assert.Contains("has_qty_baseline", sql);
        Assert.Contains("qty_baseline_reason", sql);
        Assert.Contains("change_percent_qty_semantic", sql);
        Assert.Contains("has_revenue_baseline", sql);
        Assert.Contains("revenue_baseline_reason", sql);
        Assert.Contains("change_percent_revenue_semantic", sql);
        Assert.Contains("WHEN pre.pre_qty = 0 AND post.post_qty > 0 THEN 'no_pre_qty_baseline_uplift'", sql);
        Assert.Contains("WHEN pre.pre_qty = 0 AND post.post_qty = 0 THEN 'no_pre_qty_baseline_flat'", sql);
        Assert.Contains("WHEN pre.pre_qty = 0 THEN NULL", sql);
        Assert.Contains("WHEN pre.pre_revenue = 0 AND post.post_revenue > 0 THEN 'no_pre_revenue_baseline_uplift'", sql);
        Assert.Contains("WHEN pre.pre_revenue = 0 AND post.post_revenue = 0 THEN 'no_pre_revenue_baseline_flat'", sql);
        Assert.Contains("WHEN pre.pre_revenue = 0 THEN NULL", sql);
    }

    [Fact]
    public void VendorSalesNivelacijaLowSignalPropagationRemainsIntact()
    {
        var sql = ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");

        Assert.Contains("(pre.is_low_signal OR post.coverage_post30 < 0.2) AS is_low_signal", sql);
    }

    [Fact]
    public void VendorSalesNivelacijaEndpointUsesDedicatedPriceChangeEffectPolicy()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("VendorSalesNivelacijaPriceChangeEffectPolicy.Evaluate", source);
        Assert.DoesNotContain("PopRevenueChangePct: (double)row.Vendor.ChangePercent", source);
        Assert.Contains("RecommendationAllowed = false", source);
    }

    [Fact]
    public void VendorSalesNivelacijaEndpointUsesSemanticRevenueChangeWithoutZeroCoalescing()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("change_percent_revenue_semantic::numeric AS change_percent", source);
        Assert.DoesNotContain("changePercentExpr = hasRevenuePercent ? \"change_percent_revenue\" : \"change_percent_qty\"", source);
        Assert.DoesNotContain("COALESCE(pre_qty, 0)::numeric AS pre_qty", source);
        Assert.DoesNotContain("COALESCE(post_revenue, 0)::numeric AS post_revenue", source);
        Assert.Contains("HasComparableSalesWindow", source);
        Assert.Contains("preQtyEvidence.HasValue && postQtyEvidence.HasValue", source);
    }

    [Fact]
    public void VendorSalesNivelacijaScopedFactQueryBindsStoreAndDataOriginForEventsAndSales()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("BuildVendorSalesNivelacijaScopedSourceSql", source);
        Assert.Contains("@storeId IS NULL OR d.\"IDObjekat\" = @storeId::int", source);
        Assert.Contains("@storeId IS NULL OR pz.id_objekat = @storeId::int", source);
        Assert.Contains("@dataScope::text = 'all'", source);
        Assert.Contains("d.\"DataOrigin\" = 'access'", source);
        Assert.Contains("pz.data_origin = 'access'", source);
        Assert.Contains("var useScopedFactQuery = storeId.HasValue || normalizedDataScope != \"all\";", source);
    }

    [Fact]
    public void VendorSalesNivelacijaScopedFactQueryBoundsSalesToSelectedEventWindows()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("event_bounds AS", source);
        Assert.Contains("MIN(event_date) AS min_event_date", source);
        Assert.Contains("MAX(event_date) AS max_event_date", source);
        Assert.Contains("CROSS JOIN event_bounds bounds", source);
        Assert.Contains("bounds.min_event_date IS NOT NULL", source);
        Assert.Contains("pz.datum_prodaje::date >= bounds.min_event_date - INTERVAL '30 days'", source);
        Assert.Contains("pz.datum_prodaje::date < bounds.max_event_date + INTERVAL '30 days'", source);
        AssertInOrder(source, "event_bounds AS", "sales_daily AS", "pre_window AS", "post_window AS");
    }

    [Fact]
    public void NivelacijaStartupRechecks016DependenciesAfterDestructiveViewScripts()
    {
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");

        Assert.Contains("AreVendorSalesNivelacijaDependenciesReadyAsync", initializer);
        Assert.Contains("vw_nivelacija_kontrolna_grupa", initializer);
        Assert.Contains("vw_nivelacija_did", initializer);
        Assert.Contains("DeleteAppliedStartupSqlHistoryAsync(connectionString, sqlFile)", initializer);
        Assert.Contains("Supplier nivelacija dependencies remain unavailable", initializer);
        Assert.Contains("Database/Migrations/014_NormalizeNivelacijaEvents.sql", initializer);
        Assert.DoesNotContain(
            "ExecuteSqlFileAsync(connectionString, \"Database/Migrations/014_FixNivelacijaViewsFromDnevnik.sql\"",
            initializer);
        var trendStartupStart = initializer.IndexOf(
            "logger.LogInformation(\"[Startup] Executing sequential migrations: 014, 016...\")",
            StringComparison.Ordinal);
        var trendStartupEnd = initializer.IndexOf(
            "// 005: Test data (if needed)",
            trendStartupStart,
            StringComparison.Ordinal);
        Assert.True(trendStartupStart >= 0);
        Assert.True(trendStartupEnd > trendStartupStart);
        var trendStartup = initializer[trendStartupStart..trendStartupEnd];
        AssertInOrder(
            trendStartup,
            "Database/Migrations/014_NormalizeNivelacijaEvents.sql",
            "Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql",
            "EnsureVendorSalesNivelacijaDependenciesAsync(connectionString, logger, \"trendplus\")");
        var analyticsStartupStart = initializer.IndexOf(
            "if (!unifiedDb)\n        {\n            if (!await AreVendorSalesNivelacijaViewReadyAsync",
            StringComparison.Ordinal);
        var analyticsStartupEnd = initializer.IndexOf(
            "await ExecuteSqlFileAsync(connectionString, \"Database/Analytics/Intelligence/020_create_intelligence_schema.sql\", logger);",
            analyticsStartupStart,
            StringComparison.Ordinal);
        Assert.True(analyticsStartupStart >= 0);
        Assert.True(analyticsStartupEnd > analyticsStartupStart);
        var analyticsStartup = initializer[analyticsStartupStart..analyticsStartupEnd];
        AssertInOrder(
            analyticsStartup,
            "Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql",
            "EnsureVendorSalesNivelacijaDependenciesAsync(connectionString, logger, \"analytics\")");
    }

    [Fact]
    public void NivelacijaStartupHasOneCanonicalViewOwnerAndDataOnlyNormalizationRepair()
    {
        var normalization = ReadRepoFile("Database/Migrations/014_NormalizeNivelacijaEvents.sql");
        var canonicalViews = ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");

        Assert.Contains("UPDATE \"DnevnikPromena\"", normalization);
        Assert.Contains("line.\"BrojRacuna\" ~ '^-?[0-9]+$'", normalization);
        Assert.DoesNotContain("CREATE VIEW", normalization, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP VIEW", normalization, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE OR REPLACE VIEW vw_vendor_sales_nivelacija AS", canonicalViews);
        Assert.Contains("change_percent_revenue_semantic", canonicalViews);
    }

    [Fact]
    public void VendorSalesNivelacijaViewPreservesMissingWindowAsNullAndLabelsBaselineReason()
    {
        var sql = ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");

        Assert.Contains("SUM(s.units) AS pre_qty", sql);
        Assert.Contains("WHEN e.event_date + INTERVAL '30 days' <= CURRENT_DATE THEN COALESCE(SUM(s.revenue), 0)", sql);
        Assert.Contains("WHEN pre.pre_qty IS NULL THEN 'missing_pre_qty_window'", sql);
        Assert.Contains("WHEN pre.pre_revenue IS NULL THEN 'missing_pre_revenue_window'", sql);
        Assert.Contains("change_percent_revenue_semantic::numeric AS change_percent", ReadRepoFile("Api/Endpoints/AllEndpoints.cs"));
    }

    [Fact]
    public void VendorSalesNivelacijaEndpointFailsClosedForMissingComparabilityEvidence()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("&& hasQtyBaseline", source);
        Assert.Contains("&& hasRevenueBaseline", source);
        Assert.DoesNotContain("ChangePercent = changePercentRevenue ?? 0m", source);
        Assert.Contains("VendorSalesNivelacijaPriceChangeEffectPolicy.Evaluate", source);
        Assert.Contains("RecommendationAllowed = false", source);
        Assert.Contains("var comparableRows = analyzed", source);
        Assert.Contains("var matureComparableRows = comparableRows", source);
        Assert.Contains("IsPostWindowMature", source);
        Assert.Contains("HasComparableSalesWindow = matureComparableRows.Count > 0", source);
        Assert.DoesNotContain("HasComparableSalesWindow = analyzedRows > 0 && analyzed.All(x => x.HasComparableSalesWindow)", source);
        Assert.Contains("VendorSalesNivelacijaCohortPolicy", source);
        Assert.Contains("SelectLatestEventPerArticle(dedupRows)", source);
        Assert.Contains("ArticleStats = articleStats", source);
        Assert.Contains("CohortPolicy = \"latest_event_per_article\"", source);
        Assert.DoesNotContain("LIMIT @maxRows", source);
        Assert.Contains("var totalAbsoluteChangeRevenue = vendorStats.Sum(x => x.AbsoluteChangeRevenue);", source);
        Assert.Contains("totals.AbsoluteChangeRevenue = totalAbsoluteChangeRevenue;", source);
        Assert.Contains("vendor.ChangeSharePercent = totalAbsoluteChangeRevenue == 0m", source);
        Assert.Equal(3, source.Split("var hasComparableNivelacijaSignal =", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, source.Split("var exposedRecommendation = AnalyticsDecisionRecommendationEngine.ApplyComparableSignalGate(", StringSplitOptions.None).Length - 1);
        Assert.Contains("var exposedRecommendation = OperationsRecommendationGatePolicy.ApplySupplierPolicy(", source);
        Assert.Contains("var supplierPageRecommendationAllowed", source);
        Assert.Contains("scope = \"known_supplier_rows\"", source);
        Assert.Contains("unknownSupplierRevenueDenominator = \"supplier_trust_contract_revenue\"", source);
    }

    [Fact]
    public void VendorSalesNivelacijaCoverageContractPreservesUnknownAndTrueZero()
    {
        var sql = ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");

        Assert.Contains("CASE WHEN COUNT(DISTINCT s.day) = 0 THEN NULL", sql);
        Assert.Contains("ELSE LEAST(COUNT(DISTINCT s.day) / 30.0, 1)", sql);
        Assert.Equal(2, sql.Split("CASE WHEN COUNT(DISTINCT s.day) = 0 THEN NULL", StringSplitOptions.None).Length - 1);

        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaArticleStatDto>(nameof(Api.Models.VendorSalesNivelacijaArticleStatDto.CoveragePre30));
        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaArticleStatDto>(nameof(Api.Models.VendorSalesNivelacijaArticleStatDto.CoveragePost30));
        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaVendorStatDto>(nameof(Api.Models.VendorSalesNivelacijaVendorStatDto.AvgCoveragePre30));
        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaVendorStatDto>(nameof(Api.Models.VendorSalesNivelacijaVendorStatDto.AvgCoveragePost30));
        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaTotalsDto>(nameof(Api.Models.VendorSalesNivelacijaTotalsDto.AvgCoveragePre30));
        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaTotalsDto>(nameof(Api.Models.VendorSalesNivelacijaTotalsDto.AvgCoveragePost30));
        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.AvgCoveragePre30));
        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.AvgCoveragePost30));
    }

    [Fact]
    public void VendorSalesNivelacijaDataQualityContractPreservesMissingSnapshotAndMeasuredZeros()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.RawRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.DeduplicatedRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.DuplicateRowsRemoved));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.CohortRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.CohortRowsExcluded));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.ReturnedRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.TruncatedRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.ComparableRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.InactiveRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.UnchangedPriceRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.AnalyzedRows));
        AssertNullableIntProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.LowPostCoverageRows));
        AssertNullableCoverageProperty<Api.Models.VendorSalesNivelacijaDataQualityDto>(nameof(Api.Models.VendorSalesNivelacijaDataQualityDto.AnalyzedSharePercent));
        Assert.Contains("DataQuality = null", source);
        Assert.Contains("DataQuality = new VendorSalesNivelacijaDataQualityDto", source);
    }

    [Fact]
    public void VendorSalesNivelacijaEndpointDoesNotConvertMissingCoverageToZero()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");

        Assert.Contains("var coveragePre30 = coveragePre30Evidence;", source);
        Assert.Contains("var coveragePost30 = coveragePost30Evidence;", source);
        Assert.DoesNotContain("var coveragePre30 = coveragePre30Evidence ?? 0m;", source);
        Assert.DoesNotContain("var coveragePost30 = coveragePost30Evidence ?? 0m;", source);
        Assert.Contains("AverageKnownCoverage", source);
        Assert.Contains("x.CoveragePost30.HasValue && x.CoveragePost30.Value < 0.2m", source);
    }

    private static void AssertNullableCoverageProperty<T>(string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(typeof(decimal), Nullable.GetUnderlyingType(property!.PropertyType));
    }

    private static void AssertNullableIntProperty<T>(string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(typeof(int), Nullable.GetUnderlyingType(property!.PropertyType));
    }

    [Fact]
    public void SupplierDecisionViewsExposeMissingEvidenceFlagsAndConservativeGuardrails()
    {
        var sql = ReadRepoFile("Database/Migrations/018_AddSupplierDecisionHubViews.sql");

        Assert.Contains("vn.post_qty::numeric AS post_qty_30d", sql);
        Assert.Contains("vn.post_revenue::numeric(18,2) AS post_revenue_30d", sql);
        Assert.DoesNotContain("COALESCE(vn.post_qty, 0)::numeric AS post_qty_30d", sql);
        Assert.DoesNotContain("COALESCE(vn.post_revenue, 0)::numeric(18,2) AS post_revenue_30d", sql);
        Assert.Contains("COALESCE(nd.did_revenue, 0)::numeric(18,2) AS did_revenue", sql);
        Assert.Contains("COALESCE(nd.did_qty, 0)::numeric AS did_qty", sql);
        Assert.Contains("has_post_signal", sql);
        Assert.Contains("has_did_signal", sql);
        Assert.Contains("has_cost_signal", sql);
        Assert.Contains("post_signal_coverage", sql);
        Assert.Contains("did_signal_coverage", sql);
        Assert.Contains("cost_signal_coverage", sql);
        Assert.Contains("return_rate_missing_evidence_reason", sql);
        Assert.Contains("evidence_quality_status", sql);
        Assert.Contains("WHEN COALESCE(fs.evidence_quality_status, 'partial') <> 'complete' THEN 'REVIEW_QUALITY'", sql);
        Assert.Contains("stock_proxy_clamped_to_zero", sql);
        Assert.Contains("SUM(GREATEST(COALESCE(current_stock, 0), 0) * COALESCE(current_cost, 0))::numeric(18,2) AS unsold_stock_value", sql);
        Assert.Contains("WHERE has_post_signal", sql);
        Assert.Contains("NULLIF(COUNT(*) FILTER (WHERE has_post_signal), 0) AS dead_stock_rate", sql);
    }

    [Fact]
    public void AnalyticsCompatibilityRepairHasNabavnaCenaPrerequisite()
    {
        var prerequisiteSql = ReadRepoFile("Database/Analytics/012_AddNabavnaCenaToSalesLineFacts.sql");
        var compatibilitySql = ReadRepoFile("Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql");
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");

        Assert.Contains("ADD COLUMN IF NOT EXISTS \"NabavnaCena\"", prerequisiteSql);
        Assert.Contains("slf.\"NabavnaCena\" AS nabavna_cena", compatibilitySql);
        AssertInOrder(
            initializer,
            "\"Database/Analytics/011_AddDataOriginColumns.sql\"",
            "\"Database/Analytics/012_AddNabavnaCenaToSalesLineFacts.sql\"",
            "\"Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql\"");
    }

    [Fact]
    public void StartupRepairRunsCoreViewsBeforeMaterializedCaches()
    {
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");

        Assert.Contains("SupplierDecisionHubCoreBatchCount = 5", initializer);
        Assert.Contains("SupplierDecisionHubCacheStartBatchNumber = SupplierDecisionHubCoreBatchCount + 1", initializer);
        Assert.Contains("maxBatchCount: SupplierDecisionHubCoreBatchCount", initializer);
        Assert.Contains("startBatchNumber: SupplierDecisionHubCacheStartBatchNumber", initializer);
    }

    [Fact]
    public void SupplierDecisionLiveQueryDoesNotRequireOptionalMlPredictionTable()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("GetSupplierMlQueryCapabilitiesAsync", endpoint);
        Assert.Contains("to_regclass('public.supplier_ml_predictions') IS NOT NULL", endpoint);
        Assert.Contains("CanUseSupplierMlPredictions", endpoint);
        Assert.Contains("ROUND(fs.supplier_quality_index, 2) AS ml_supplier_score", endpoint);
        AssertInOrder(
            endpoint,
            "var mlJoin = mlCapabilities.CanUseSupplierMlPredictions",
            "FROM supplier_ml_predictions p");
    }

    [Fact]
    public void SupplierDecisionLiveAndPrecomputedSqlPreservePostObservationState()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("vn.post_qty::numeric AS post_qty_30d", endpoint);
        Assert.Contains("vn.post_revenue::numeric(18,2) AS post_revenue_30d", endpoint);
        Assert.DoesNotContain("COALESCE(vn.post_qty, 0)::numeric AS post_qty_30d", endpoint);
        Assert.Contains("(vn.price_event_id IS NOT NULL) AS has_post_signal", endpoint);
        Assert.Contains("AVG(CASE WHEN has_post_signal THEN 1::numeric ELSE 0::numeric END) AS post_signal_coverage", endpoint);
        Assert.Contains("WHEN COALESCE(sr.post_signal_coverage, 0) < 1 THEN 'REVIEW_QUALITY'", endpoint);
        Assert.Contains("ROUND(COALESCE(ds.post_signal_coverage, 0), 4) AS post_signal_coverage", endpoint);
        Assert.Contains("BuildRecommendationSignal(recommendationCode, confidenceScore, postSignalCoverage)", endpoint);
        Assert.Contains("hasIncompletePostCoverage", endpoint);
        Assert.Contains("missing_post_observation", endpoint);
        Assert.Contains("WHERE has_post_signal", endpoint);
        Assert.Contains("AND COALESCE(post_revenue_30d, 0) > 0", endpoint);
        Assert.Contains("NULLIF(COUNT(*) FILTER (WHERE has_post_signal), 0) AS dead_stock_rate", endpoint);
    }

    [Fact]
    public void SupplierDecisionPrecomputedAndLiveSqlParityMatrixLocksIntentionalDifferences()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("CanUsePrecomputedSupplierRows(filters)", endpoint);
        Assert.Contains("string.IsNullOrWhiteSpace(filters.Category)", endpoint);
        Assert.Contains("string.IsNullOrWhiteSpace(filters.Gender)", endpoint);
        Assert.Contains("!filters.SeasonId.HasValue", endpoint);
        Assert.Contains("!filters.StoreId.HasValue", endpoint);
        Assert.Contains("string.Equals(filters.DataScope, \"all\", StringComparison.OrdinalIgnoreCase)", endpoint);
        Assert.Contains("string.Equals(filters.DataScope, \"imported\", StringComparison.OrdinalIgnoreCase)", endpoint);
        Assert.Contains("string.Equals(filters.DataScope, \"existing\", StringComparison.OrdinalIgnoreCase)", endpoint);

        Assert.Contains("ds.period_to >= @fromDate AND ds.period_from <= @toDate", endpoint);
        Assert.Contains("fs.first_markdown_date >= @fromDate", endpoint);
        Assert.Contains("fs.first_markdown_date <= @toDate", endpoint);
        Assert.Contains("store_pz.\\\"IDObjekat\\\" = @storeId", endpoint);
        Assert.Contains("a.\\\"DataOrigin\\\" = 'access'", endpoint);
        Assert.Contains("a.\\\"DataOrigin\\\" IS NULL OR a.\\\"DataOrigin\\\" = ''", endpoint);
        Assert.Contains("COALESCE(fs.category, 'Uncategorized') ILIKE @category", endpoint);
        Assert.Contains("COALESCE(a.\\\"Pol\\\", '') ILIKE @gender", endpoint);
        Assert.Contains("a.\\\"IDSezona\\\" = @seasonId", endpoint);

        Assert.Contains("ROUND(ds.confidence_score * 100, 2) AS confidence_score", endpoint);
        Assert.Contains("ROUND(COALESCE(ds.post_signal_coverage, 0), 4) AS post_signal_coverage", endpoint);
        Assert.Contains("ROUND(COALESCE(ml.ml_supplier_score, fs.supplier_quality_index), 2) AS ml_supplier_score", endpoint);
        Assert.Contains("GetString(reader, \"recommendation_code\")", endpoint);
        Assert.Contains("BuildRecommendationSignal(recommendationCode, confidenceScore, postSignalCoverage)", endpoint);
        Assert.Contains("GetDecimal(reader, \"fullprice_revenue_share\")", endpoint);
        Assert.Contains("GetString(reader, \"ai_explanation\")", endpoint);
    }

    [Fact]
    public void SupplierDecisionBroadDateRangesUsePrecomputedCaches()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("CanUsePrecomputedSupplierRows(filters)", endpoint);
        Assert.DoesNotContain("!filters.HasExplicitDateRange\n        && string.IsNullOrWhiteSpace(filters.Category)", endpoint);
        Assert.Contains("ds.period_to >= @fromDate AND ds.period_from <= @toDate", endpoint);
        Assert.Contains("var mvName = SelectDecisionScoreMv(windowDays);", endpoint);
        Assert.Contains("FROM {mvName} ds", endpoint);
    }

    [Fact]
    public void SupplierDecisionPrecomputedCapabilitiesUseMaterializedViewCatalogTruth()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");
        var capabilityReader = ReadRepoFile("Infrastructure/Analytics/SupplierDecisionMaterializedViewCapability.cs");

        Assert.Contains("var windowDays = GetDecisionScoreWindowDays(filters);", endpoint);
        Assert.Contains("var decisionScoreCapability = capabilities.DecisionScoreCacheForWindow(windowDays);", endpoint);
        Assert.Contains("if (!decisionScoreCapability.IsReady)", endpoint);
        Assert.Contains("PostgresMaterializedViewCapabilityReader.InspectAsync", endpoint);
        Assert.Contains("SupplierDecisionMaterializedViewContract.DecisionScoreRequiredColumns", endpoint);
        Assert.Contains("SupplierDecisionMaterializedViewContract.MlSupplierScoreColumn", endpoint);
        Assert.Contains("to_regclass('public.vw_supplier_ml_latest_predictions')", endpoint);
        Assert.Contains("ml_latest_predictions_view_has_required_columns", endpoint);
        Assert.Contains("table_name = 'vw_supplier_ml_latest_predictions'", endpoint);
        Assert.Contains("'top_feature_1'", endpoint);
        Assert.Contains("'top_feature_2'", endpoint);
        Assert.Contains("'top_feature_3'", endpoint);
        Assert.Contains("'explanation_text'", endpoint);
        Assert.DoesNotContain("table_name = 'mv_supplier_decision_score_cache", endpoint);
        Assert.Contains("\"MISSING_OBJECT\"", endpoint);
        Assert.Contains("\"MISSING_COLUMNS\"", endpoint);
        Assert.Contains("\"NOT_POPULATED\"", endpoint);
        Assert.Contains("FROM pg_class c", capabilityReader);
        Assert.Contains("JOIN pg_namespace n", capabilityReader);
        Assert.Contains("LEFT JOIN pg_matviews mv", capabilityReader);
        Assert.Contains("JOIN pg_attribute a", capabilityReader);
        Assert.Contains("a.attnum > 0", capabilityReader);
        Assert.Contains("NOT a.attisdropped", capabilityReader);
        Assert.Contains("\"post_signal_coverage\"", capabilityReader);
        Assert.Contains("\"confidence_score\"", capabilityReader);
        Assert.Contains("\"recommendation_code\"", capabilityReader);
        Assert.DoesNotContain("? GetDecimal(reader, \"post_signal_coverage\")\n                    : 1m", endpoint);
    }

    [Fact]
    public void SupplierDecisionAllTimeMlProjectionCarriesTheSameEvidenceContract()
    {
        var sql = ReadRepoFile("Database/Analytics/015_AddSupplierMlRanking.sql");

        Assert.Contains("ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage", sql);
        Assert.Contains("ROUND(COALESCE(did_signal_coverage, 0), 4) AS did_signal_coverage", sql);
        Assert.Contains("ROUND(COALESCE(cost_signal_coverage, 0), 4) AS cost_signal_coverage", sql);
        Assert.Contains("evidence_quality_status", sql);
        Assert.Contains("return_rate_missing_evidence_reason", sql);
        AssertInOrder(
            sql,
            "confidence_score,\n    ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage",
            "CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache AS");
    }

    [Fact]
    public void SupplierDecisionWindowedMvAudit_Confirms90d180dAndAllTimeContract()
    {
        var sql = ReadRepoFile("Database/Migrations/029_AddSupplierDecisionWindowedViews.sql");
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");
        var options = ReadRepoFile("Infrastructure/Configuration/NightlyAnalyticsRefreshOptions.cs");

        Assert.Contains("-- scorecard so that 30d / 90d / 180d date ranges return metrics", sql);
        Assert.Contains("COMMENT ON VIEW vw_supplier_fullprice_signals_90d IS", sql);
        Assert.Contains("Supplier fullprice signals limited to the rolling 90-day window ending today.", sql);
        Assert.Contains("COMMENT ON VIEW vw_supplier_fullprice_signals_180d IS", sql);
        Assert.Contains("Supplier fullprice signals limited to the rolling 180-day window ending today.", sql);
        Assert.Contains("mv_supplier_decision_score_cache_90d", sql);
        Assert.Contains("mv_supplier_decision_score_cache_180d", sql);
        Assert.DoesNotContain("mv_supplier_decision_score_cache_30d", sql);

        Assert.Contains("return \"30d\"", endpoint);
        Assert.Contains("return \"90d\"", endpoint);
        Assert.Contains("return \"180d\"", endpoint);
        Assert.Contains("_ => \"all_time\"", endpoint);
        Assert.Contains("no_mv_30d", endpoint);

        Assert.Contains("\"mv_supplier_decision_score_cache_90d\"", options);
        Assert.Contains("\"mv_supplier_decision_score_cache_180d\"", options);
        Assert.DoesNotContain("\"mv_supplier_decision_score_cache_30d\"", options);
    }

    [Fact]
    public void SupplierDecisionWindowedScoreCachesRepeatTheSameColumnContract()
    {
        var sql = ReadRepoFile("Database/Migrations/029_AddSupplierDecisionWindowedViews.sql");

        Assert.Contains("CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache_90d AS", sql);
        Assert.Contains("ROUND(COALESCE(fullprice_revenue_share, 0), 4) AS fullprice_revenue_share", sql);
        Assert.Contains("ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage", sql);
        Assert.Contains("ROUND(COALESCE(cost_signal_coverage, 0), 4) AS cost_signal_coverage", sql);
        Assert.Contains("ROUND(return_rate, 4) AS return_rate", sql);
        Assert.Contains("ROUND(COALESCE(markdown_penalty, 0), 2) AS markdown_dependency_score", sql);
        Assert.Contains("ROUND(COALESCE(inventory_penalty, 0), 2) AS stock_risk_score", sql);
        Assert.Contains("CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache_180d AS", sql);
        Assert.Contains("ROUND(COALESCE(fullprice_revenue_share, 0), 4) AS fullprice_revenue_share", sql);
        Assert.Contains("ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage", sql);
        Assert.Contains("ROUND(COALESCE(cost_signal_coverage, 0), 4) AS cost_signal_coverage", sql);
        Assert.Contains("ROUND(return_rate, 4) AS return_rate", sql);
        Assert.Contains("ROUND(COALESCE(markdown_penalty, 0), 2) AS markdown_dependency_score", sql);
        Assert.Contains("ROUND(COALESCE(inventory_penalty, 0), 2) AS stock_risk_score", sql);
    }

    [Fact]
    public void SupplierDecisionWindowedScoreCachesKeepOneSupplierRankGuardAndEvidenceReviewFallback()
    {
        var sql = ReadRepoFile("Database/Migrations/029_AddSupplierDecisionWindowedViews.sql");

        Assert.Contains("CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric", sql);
        Assert.Contains("WHEN COALESCE(fs.evidence_quality_status, 'partial') <> 'complete' THEN 'REVIEW_QUALITY'", sql);
        Assert.Contains("return_rate_missing_evidence_reason", sql);
    }

    [Fact]
    public void SupplierDecisionLiveSqlUsesRetailReceiptPopulationSaleAttributionAndReceiptStore()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("SalesReceiptPopulationPolicy.IncludedHeaderPredicate", endpoint);
        Assert.Contains("ps.supplier_id_at_sale = b.supplier_id", endpoint);
        Assert.Contains("UPPER(TRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')", endpoint);
        Assert.Contains("store_pz.\\\"IDObjekat\\\" = @storeId", endpoint);
        Assert.Contains("returned_units_in_period", endpoint);
        Assert.Contains("/ NULLIF(COALESCE(s.gross_sold_units_in_period, 0), 0)", endpoint);
        Assert.Contains("WHEN ss.return_rate IS NULL OR COUNT(ss.return_rate) OVER () = 1 THEN 0.5::numeric", endpoint);
        Assert.DoesNotContain("ORDER BY COALESCE(ss.return_rate, 0)", endpoint);
        Assert.DoesNotContain("period_returns AS", endpoint);
        Assert.DoesNotContain("povracaj_zaglavlje", endpoint);
    }

    [Fact]
    public void SupplierDecisionMaterializedViewsUseSignedRetailReturnsAndSaleTimeSupplierAttribution()
    {
        foreach (var path in new[]
        {
            "Database/Migrations/018_AddSupplierDecisionHubViews.sql",
            "Database/Migrations/029_AddSupplierDecisionWindowedViews.sql"
        })
        {
            var sql = ReadRepoFile(path);

            Assert.Contains("ps.supplier_id_at_sale = sr.supplier_id", sql);
            Assert.Contains("returned_units_in_period", sql);
            Assert.Contains("/ NULLIF(si.gross_sold_units_in_period, 0)", sql);
            Assert.Contains("WHEN di.return_rate IS NULL OR COUNT(di.return_rate) OVER () = 1 THEN 0.5::numeric", sql);
            Assert.DoesNotContain("ORDER BY COALESCE(di.return_rate, 0)", sql);
            Assert.DoesNotContain("LEFT JOIN prodaja_stavke ps ON ps.id_artikal = a.\"Id\" AND ps.supplier_id_at_sale", sql);
            Assert.Contains("UPPER(TRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')", sql);
            Assert.DoesNotContain("returns_in_period AS", sql);
            Assert.DoesNotContain("povracaj_zaglavlje", sql);
            Assert.DoesNotContain("povracaj_stavke", sql);
        }
    }

    [Fact]
    public void SupplierDecisionScorecardInputRepairReachesExistingDatabases()
    {
        var windowed = ReadRepoFile("Database/Migrations/029_AddSupplierDecisionWindowedViews.sql");
        foreach (var mv in new[] { "mv_supplier_decision_score_cache_90d", "mv_supplier_decision_score_cache_180d" })
        {
            var drop = windowed.IndexOf($"DROP MATERIALIZED VIEW public.{mv};", StringComparison.Ordinal);
            var create = windowed.IndexOf($"CREATE MATERIALIZED VIEW IF NOT EXISTS {mv} AS", StringComparison.Ordinal);
            Assert.True(drop >= 0 && drop < create, $"{mv} must drop a stale definition before CREATE IF NOT EXISTS.");
            Assert.Contains($"pg_get_viewdef(to_regclass('public.{mv}')) NOT LIKE '%gross_sold_units_in_period%'", windowed);
        }

        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");
        Assert.Contains("pg_get_viewdef(to_regclass('public.vw_supplier_fullprice_signals')) LIKE '%KOREKCIJA%'", initializer);
        Assert.Contains("pg_get_viewdef(to_regclass('public.vw_supplier_decision_score')) LIKE '%gross_sold_units_in_period%'", initializer);
    }

    [Fact]
    public void SupplierReportMetricBasisUsesMarginPolicyAndDoesNotTreatMissingCostAsZero()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("BuildSupplierReportMetricBasis", endpoint);
        Assert.Contains("AnalyticsMarginPolicy.ResolveNoCostRevenue", endpoint);
        Assert.Contains("SalesReceiptPopulationPolicy.IncludedHeaderPredicate", endpoint);
        Assert.Contains("saleLine.SupplierIdAtSale", endpoint);
        Assert.Contains("receipt.IDObjekat", endpoint);
        Assert.DoesNotContain("povracaj_zaglavlje", endpoint);
    }

    [Fact]
    public void SupplierDecisionLiveRecommendationUsesTheBaseScoreScaleNotAnOptionalMlBlend()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("PERCENT_RANK() OVER (ORDER BY COALESCE(ss.fullprice_sellthrough, 0))", endpoint);
        Assert.Contains("0.60 * ns.fullprice_sellthrough_rank", endpoint);
        Assert.Contains("ns.pre_markdown_margin_rank * 100", endpoint);
        Assert.Contains("ns.markdown_revenue_share_rank", endpoint);
        Assert.Contains("ns.dead_stock_rate_rank", endpoint);
        Assert.Contains("WHEN sr.supplier_quality_index > 80 THEN 'EXPAND'", endpoint);
        Assert.Contains("WHEN sr.supplier_quality_index >= 60 THEN 'EXPAND_SELECTIVELY'", endpoint);
        Assert.Contains("supplier_quality_index,\n    recommendation_code", endpoint);
        Assert.DoesNotContain("WHEN sr.blended_supplier_quality_index > 80 THEN 'EXPAND'", endpoint);
    }

    [Fact]
    public void SupplierDecision180DayDependencyViewExposesCoverageColumnsConsumedByItsScoreCache()
    {
        var sql = ReadRepoFile("Database/Migrations/029_AddSupplierDecisionWindowedViews.sql");
        var dependencyStart = sql.IndexOf("CREATE OR REPLACE VIEW vw_supplier_markdown_dependency_180d AS", StringComparison.Ordinal);
        var scoreCacheStart = sql.IndexOf("CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache_90d AS", StringComparison.Ordinal);

        Assert.True(dependencyStart >= 0);
        Assert.True(scoreCacheStart > dependencyStart);

        var dependencySql = sql[dependencyStart..scoreCacheStart];
        Assert.Contains("has_post_signal", dependencySql);
        Assert.Contains("has_did_signal", dependencySql);
        Assert.Contains("has_cost_signal", dependencySql);
        Assert.Contains("AS post_signal_coverage", dependencySql);
        Assert.Contains("AS did_signal_coverage", dependencySql);
        Assert.Contains("AS cost_signal_coverage", dependencySql);

        var scoreCacheSql = sql[scoreCacheStart..];
        Assert.Contains("AS evidence_quality_status", scoreCacheSql);
        Assert.Contains("WHEN COALESCE(fs.evidence_quality_status, 'partial') <> 'complete' THEN 'REVIEW_QUALITY'", scoreCacheSql);

        var scoreCache180Start = sql.IndexOf("CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache_180d AS", scoreCacheStart, StringComparison.Ordinal);
        var signalRollup180Start = sql.IndexOf("signal_rollup AS (", scoreCache180Start, StringComparison.Ordinal);
        Assert.True(scoreCache180Start > scoreCacheStart);
        Assert.True(signalRollup180Start > scoreCache180Start);

        var supplierTotals180Sql = sql[scoreCache180Start..signalRollup180Start];
        Assert.Contains("post_signal_coverage", supplierTotals180Sql);
        Assert.Contains("did_signal_coverage", supplierTotals180Sql);
        Assert.Contains("cost_signal_coverage", supplierTotals180Sql);
    }

    [Fact]
    public void SupplierDecisionHeavyRefreshIsNotOwnedByWebStartupRepair()
    {
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");
        var worker = ReadRepoFile("Workers/NightlyAnalyticsRefreshWorker.cs");
        var options = ReadRepoFile("Infrastructure/Configuration/NightlyAnalyticsRefreshOptions.cs");
        var appsettings = ReadRepoFile("Api/appsettings.json");

        Assert.Contains("AllowSupplierDecisionHeavyRefreshInInitializer", initializer);
        Assert.Contains("allowHeavyRefresh: false", initializer);
        Assert.Contains("NightlyAnalyticsRefreshWorker is the refresh owner", initializer);
        Assert.Contains("if (!cachesRefreshed && allowHeavyRefresh", initializer);
        Assert.DoesNotContain("if (!cachesRefreshed && await AreSupplierDecisionHubCachesReadyAsync", initializer);
        Assert.Contains("\"AllowSupplierDecisionHeavyRefreshInInitializer\": false", appsettings);

        Assert.Contains("CanRefreshMaterializedViewConcurrentlyAsync", worker);
        Assert.Contains("idx.indexprs IS NULL", worker);
        Assert.Contains("RefreshConcurrently", options);
        Assert.Contains("\"mv_supplier_markdown_dependency_cache\"", options);
        Assert.Contains("\"mv_supplier_decision_score_cache\"", options);
        Assert.Contains("\"mv_supplier_recommendations_cache\"", options);
    }

    [Fact]
    public void SupplierDecisionWindowedMvStartupReadinessUsesEndpointCapabilityContract()
    {
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");
        var options = ReadRepoFile("Infrastructure/Configuration/NightlyAnalyticsRefreshOptions.cs");

        Assert.Contains("AreSupplierDecisionHubCachesReadyAsync", initializer);
        Assert.Contains("mv_supplier_markdown_dependency_cache", initializer);
        Assert.Contains("mv_supplier_decision_score_cache", initializer);
        Assert.Contains("mv_supplier_recommendations_cache", initializer);
        Assert.Contains("LogSupplierDecisionHubWindowedCacheStatusAsync", initializer);
        Assert.Contains("InspectSupplierDecisionScoreMaterializedViewAsync", initializer);
        Assert.Contains("PostgresMaterializedViewCapabilityReader.InspectAsync", initializer);
        Assert.Contains("SupplierDecisionMaterializedViewContract.DecisionScoreRequiredColumns", initializer);
        Assert.Contains("90d=READY 180d=READY", initializer);
        Assert.Contains("Endpoint readiness uses the same materialized-view capability contract.", initializer);
        Assert.Contains("windowed90.ErrorCode ?? \"READY\"", initializer);
        Assert.Contains("windowed180.ErrorCode ?? \"READY\"", initializer);
        Assert.Contains("\"mv_supplier_decision_score_cache_90d\"", options);
        Assert.Contains("\"mv_supplier_decision_score_cache_180d\"", options);
    }

    [Fact]
    public void SupplierDecisionWindowedViewsAreVerifiedAndRepairedWhenStartupHistoryIsStale()
    {
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");

        Assert.Contains("EnsureSupplierDecisionWindowedViewsAsync", initializer);
        Assert.Contains("DeleteAppliedStartupSqlHistoryAsync(connectionString, sqlFile)", initializer);
        Assert.Contains("await ExecuteSqlFileAsync(connectionString, sqlFile, logger);", initializer);
        Assert.Contains("remain unavailable after {sqlFile}", initializer);
        Assert.Contains("EnsureSupplierDecisionWindowedViewsAsync(connectionString, logger, \"analytics\")", initializer);
        Assert.Contains("EnsureSupplierDecisionWindowedViewsAsync(connectionString, logger, \"supplier-decision-repair\")", initializer);
        Assert.Contains("share the same database", initializer);
    }

    [Fact]
    public void SupplierDecisionResponsesExposeAndPopulateTrustMetadata()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("public sealed record ScorecardTrustMetadata(", endpoint);
        Assert.Contains("string RequestedDataset", endpoint);
        Assert.Contains("string EffectiveDataset", endpoint);
        Assert.Contains("RequestedPeriodFrom", endpoint);
        Assert.Contains("RequestedPeriodTo", endpoint);
        Assert.Contains("string EffectivePeriodLabel", endpoint);
        Assert.Contains("string DataCoverageStatus", endpoint);
        Assert.Contains("bool UsedFallback", endpoint);
        Assert.Contains("string? FallbackReasonCode", endpoint);
        Assert.Contains("DateTime? LastRefreshAtUtc", endpoint);
        Assert.Contains("string? ProvenanceBasis", endpoint);
        Assert.Contains("int RowCount", endpoint);
        Assert.Contains("int IgnoredRowCount", endpoint);
        Assert.Contains("int ZeroRevenueRowsExcludedCount", endpoint);
        Assert.Contains("int MissingSupplierNameCount", endpoint);
        Assert.Contains("string? DataNote", endpoint);
        Assert.Contains("bool NoSilentFallback", endpoint);
        Assert.Contains("string Coverage", endpoint);

        Assert.Contains("ScorecardTrustMetadata? TrustMetadata = null", endpoint);
        Assert.Contains("BuildScorecardTrustMetadata(dataset, filters)", endpoint);
        Assert.Contains("BuildScorecardTrustMetadata(orderedDataset, activeFilters)", endpoint);
        Assert.Contains("bool RecommendationAllowed", endpoint);
        Assert.Contains("ResolveRequestedDataset", endpoint);
        Assert.Contains("BuildEffectivePeriodLabel", endpoint);
        Assert.Contains("dataCoverageStatus", endpoint);
        Assert.Contains("meta.RequestedPeriodFromUtc = trustMetadata?.RequestedFrom", endpoint);
        Assert.Contains("meta.EffectivePeriodFromUtc = trustMetadata?.EffectiveFrom", endpoint);
        Assert.Contains("meta.ObservedPeriodFromUtc = rows.Count > 0 ? rows.Min(row => row.PeriodFrom) : null", endpoint);
    }

    [Fact]
    public void SupplierDecisionRecommendationRowsExposeReliabilityAndReasonPayload()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("private sealed record RecommendationSignal(", endpoint);
        Assert.Contains("BuildRecommendationSignal", endpoint);
        Assert.Contains("decimal ReliabilityPct", endpoint);
        Assert.Contains("string DataQualityStatus", endpoint);
        Assert.Contains("string StatusReason", endpoint);
        Assert.Contains("IReadOnlyList<string> ReasonCodes", endpoint);
        Assert.Contains("recommendationSignal.ReliabilityPct", endpoint);
        Assert.Contains("recommendationSignal.StatusReason", endpoint);
    }

    [Fact]
    public void SupplierDecisionRichDetailsReuseCanonicalTrustMetadataAndResponseMeta()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("BuildDetailsResponseAsync(analyticsConnectionString, activeFilters, dataset, supplier, ct)", endpoint);
        Assert.Contains("var trustMetadata = BuildScorecardTrustMetadata(dataset, filters);", endpoint);
        Assert.Contains("BuildResponseMeta(dataset.Rows, trustMetadata)", endpoint);
        Assert.Contains("Meta = ApplyCorrelationId(response.Response.Meta, ResolveCorrelationId(httpContext))", endpoint);
        Assert.Contains("ScorecardTrustMetadata? TrustMetadata = null", endpoint);
        Assert.Contains("string? DataNote = null", endpoint);
        Assert.Contains("AnalyticsResponseMetaDto? Meta = null", endpoint);
    }

    [Fact]
    public void SupplierDecisionReaderNullabilityContractKeepsHighRiskFieldsExplicit()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("GetInt32(reader, \"supplier_id\")", endpoint);
        Assert.Contains("GetString(reader, \"supplier_name\")", endpoint);
        Assert.Contains("NormalizeSupplierName(supplierId, sourceSupplierName)", endpoint);
        Assert.Contains("GetString(reader, \"recommendation_code\")", endpoint);
        Assert.Contains("BuildRecommendationSignal(recommendationCode, confidenceScore, postSignalCoverage)", endpoint);
        Assert.Contains("GetInt32(reader, \"article_id\")", endpoint);
        Assert.Contains("GetString(reader, \"signal_quality_flag\")", endpoint);
        Assert.Contains("GetString(reader, \"signal_quality_reason\")", endpoint);
        Assert.Contains("GetDecimal(reader, \"confidence_score\")", endpoint);
        Assert.Contains("GetString(reader, \"ai_explanation\")", endpoint);
        Assert.Contains("GetString(reader, \"top_feature_1\")", endpoint);
    }

    [Fact]
    public void SupplierDecisionBackendCopyUsesReadableSerbianInDecisionStrings()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.DoesNotContain("PoveÄ‡ati", endpoint);
        Assert.DoesNotContain("DobavljaÄ", endpoint);
        Assert.DoesNotContain("sniÅ¾enja", endpoint);
        Assert.DoesNotContain("uÄinak", endpoint);
        Assert.DoesNotContain("Å¡irenje", endpoint);
        Assert.DoesNotContain("meÅ¡ovit", endpoint);
        Assert.Contains("Povećati saradnju", endpoint);
        Assert.Contains("Dobavljač #", endpoint);
        Assert.Contains("Zavisnost od sniženja", endpoint);
        Assert.Contains("Zadržati trenutni nivo", endpoint);
        Assert.Contains("Povraćaji ili kvalitet su dovoljno loši da blokiraju bezbedno širenje saradnje.", endpoint);
    }

    [Fact]
    public void AnalyticsResponseMetaIncludesCorrelationId()
    {
        var dto = ReadRepoFile("Api/Dtos/AnalyticsResponseMetaDto.cs");

        Assert.Contains("public string? CorrelationId { get; set; }", dto);
    }

    [Fact]
    public void SupplierDecisionUnavailablePathsReturnExplicitErrorMeta()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("SupplierDecisionUnavailableException", endpoint);
        Assert.Contains("BuildErrorMeta(ex.ErrorCode, ex.Message, ResolveCorrelationId(httpContext))", endpoint);
        Assert.Contains("MISSING_TABLE", endpoint);
        Assert.Contains("SQL_TIMEOUT", endpoint);
    }

    [Fact]
    public void SupplierDecisionDatasetCacheKeyIsVersionedWhenPayloadChanges()
    {
        var keys = ReadRepoFile("Infrastructure/Services/Caching/IAnalyticsCacheService.cs");
        Assert.Contains("supplier-decision-hub:dataset:v2:", keys);
    }

    private static void AssertInOrder(string text, params string[] fragments)
    {
        var lastIndex = -1;
        foreach (var fragment in fragments)
        {
            var index = text.IndexOf(fragment, lastIndex + 1, StringComparison.OrdinalIgnoreCase);
            Assert.True(index > lastIndex, $"Expected '{fragment}' after index {lastIndex}.");
            lastIndex = index;
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ReadRepoFile(string relativePath)
    {
        var repoRoot = FindRepoRoot();
        // Normalize line endings so multi-line source fragments match on CRLF (Windows autocrlf) and LF checkouts alike.
        return File.ReadAllText(Path.Combine(repoRoot, relativePath)).ReplaceLineEndings("\n");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
