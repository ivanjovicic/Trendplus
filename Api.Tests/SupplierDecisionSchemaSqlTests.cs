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

        Assert.Contains("WHEN pre.pre_qty = 0 AND post.post_qty > 0 THEN 100", sql);
        Assert.Contains("WHEN pre.pre_qty = 0 THEN 0", sql);
        Assert.Contains("WHEN pre.pre_revenue = 0 AND post.post_revenue > 0 THEN 100", sql);
        Assert.Contains("WHEN pre.pre_revenue = 0 THEN 0", sql);
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
    public void SupplierDecisionViewsExposeMissingEvidenceFlagsAndConservativeGuardrails()
    {
        var sql = ReadRepoFile("Database/Migrations/018_AddSupplierDecisionHubViews.sql");

        Assert.Contains("COALESCE(vn.post_qty, 0)::numeric AS post_qty_30d", sql);
        Assert.Contains("COALESCE(vn.post_revenue, 0)::numeric(18,2) AS post_revenue_30d", sql);
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
        Assert.Contains("a.\\\"IDObjekat\\\" = @storeId", endpoint);
        Assert.Contains("a.\\\"DataOrigin\\\" = 'access'", endpoint);
        Assert.Contains("a.\\\"DataOrigin\\\" IS NULL OR a.\\\"DataOrigin\\\" = ''", endpoint);
        Assert.Contains("COALESCE(fs.category, 'Uncategorized') ILIKE @category", endpoint);
        Assert.Contains("COALESCE(a.\\\"Pol\\\", '') ILIKE @gender", endpoint);
        Assert.Contains("a.\\\"IDSezona\\\" = @seasonId", endpoint);

        Assert.Contains("ROUND(ds.confidence_score * 100, 2) AS confidence_score", endpoint);
        Assert.Contains("ROUND(COALESCE(ml.ml_supplier_score, fs.supplier_quality_index), 2) AS ml_supplier_score", endpoint);
        Assert.Contains("GetString(reader, \"recommendation_code\")", endpoint);
        Assert.Contains("BuildRecommendationSignal(recommendationCode, confidenceScore)", endpoint);
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
        Assert.Contains("return \"all_time\"", endpoint);
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
    public void SupplierDecisionWindowedMvStartupReadinessIsLoggedButNotGated()
    {
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");
        var options = ReadRepoFile("Infrastructure/Configuration/NightlyAnalyticsRefreshOptions.cs");

        Assert.Contains("AreSupplierDecisionHubCachesReadyAsync", initializer);
        Assert.Contains("mv_supplier_markdown_dependency_cache", initializer);
        Assert.Contains("mv_supplier_decision_score_cache", initializer);
        Assert.Contains("mv_supplier_recommendations_cache", initializer);
        Assert.Contains("LogSupplierDecisionHubWindowedCacheStatusAsync", initializer);
        Assert.Contains("Supplier decision windowed caches are present", initializer);
        Assert.Contains("Supplier decision windowed caches are not fully ready", initializer);
        Assert.Contains("Startup readiness still gates only the all-time cache stack", initializer);
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
    public void SupplierDecisionReaderNullabilityContractKeepsHighRiskFieldsExplicit()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("GetInt32(reader, \"supplier_id\")", endpoint);
        Assert.Contains("GetString(reader, \"supplier_name\")", endpoint);
        Assert.Contains("NormalizeSupplierName(supplierId, sourceSupplierName)", endpoint);
        Assert.Contains("GetString(reader, \"recommendation_code\")", endpoint);
        Assert.Contains("BuildRecommendationSignal(recommendationCode, confidenceScore)", endpoint);
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
        return File.ReadAllText(Path.Combine(repoRoot, relativePath));
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
