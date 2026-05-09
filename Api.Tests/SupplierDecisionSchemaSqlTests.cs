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
