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
    public void SupplierDecisionResponsesExposeAndPopulateTrustMetadata()
    {
        var endpoint = ReadRepoFile("Api/Endpoints/SupplierDecisionHubEndpoints.cs");

        Assert.Contains("public sealed record ScorecardTrustMetadata(", endpoint);
        Assert.Contains("string RequestedDataset", endpoint);
        Assert.Contains("string EffectiveDataset", endpoint);
        Assert.Contains("string EffectivePeriodLabel", endpoint);
        Assert.Contains("string DataCoverageStatus", endpoint);
        Assert.Contains("bool UsedFallback", endpoint);
        Assert.Contains("string? FallbackReasonCode", endpoint);
        Assert.Contains("DateTime? LastRefreshAtUtc", endpoint);
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
