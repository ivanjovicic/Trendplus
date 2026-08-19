using Application.Analytics.Inventory;
using Application.Analytics.Queries.GetObservedInventoryDailySnapshot;
using Xunit;

namespace Api.Tests;

public sealed class ObservedInventoryDailySnapshotTests
{
    [Theory]
    [InlineData("0", null, InventoryDailyStockProvenance.Observed)]
    [InlineData("4.5", "4.5", InventoryDailyStockProvenance.Observed)]
    [InlineData(null, "0", InventoryDailyStockProvenance.Reconstructed)]
    [InlineData("2", "9", InventoryDailyStockProvenance.Mixed)]
    [InlineData(null, null, InventoryDailyStockProvenance.Missing)]
    public void Classify_KeepsObservedZeroDistinctFromMissingAndReconstructed(
        string? observed,
        string? reconstructed,
        string expected)
    {
        var provenance = InventoryDailyStockProvenance.Classify(
            ParseDecimal(observed),
            ParseDecimal(reconstructed));

        Assert.Equal(expected, provenance);
        if (expected == InventoryDailyStockProvenance.Missing)
        {
            Assert.Null(InventoryDailyStockProvenance.AuthoritativeQuantity(ParseDecimal(observed), ParseDecimal(reconstructed)));
        }
    }

    [Fact]
    public void Mapper_DoesNotFabricateZeroWhenBothSidesMissing()
    {
        var row = ObservedInventoryDailySnapshotMapper.Map(
            articleId: 11,
            storeId: 0,
            date: new DateTime(2026, 8, 1),
            observedQty: null,
            reconstructedQty: null,
            stockQty: null,
            provenance: null,
            capturedAtUtc: null,
            sourceSystem: null);

        Assert.Equal(InventoryDailyStockProvenance.Missing, row.Provenance);
        Assert.Null(row.ObservedQty);
        Assert.Null(row.ReconstructedQty);
        Assert.Null(row.StockQty);
        Assert.False(InventoryDailyStockProvenance.IsObservedAuthoritative(row.Provenance));
    }

    [Fact]
    public void Mapper_ObservedZeroRemainsTrueZero()
    {
        var row = ObservedInventoryDailySnapshotMapper.Map(
            articleId: 11,
            storeId: 0,
            date: new DateTime(2026, 8, 19),
            observedQty: 0m,
            reconstructedQty: 8m,
            stockQty: 0m,
            provenance: "mixed",
            capturedAtUtc: new DateTime(2026, 8, 19, 6, 0, 0, DateTimeKind.Utc),
            sourceSystem: "products_dim_current");

        Assert.Equal(InventoryDailyStockProvenance.Mixed, row.Provenance);
        Assert.Equal(0m, row.ObservedQty);
        Assert.Equal(8m, row.ReconstructedQty);
        Assert.Equal(0m, row.StockQty);
        Assert.NotEqual(InventoryDailyStockProvenance.Missing, row.Provenance);
        Assert.False(InventoryDailyStockProvenance.IsObservedAuthoritative(row.Provenance));
    }

    [Fact]
    public void SqlFoundation_DoesNotBackfillReconstructedIntoObservedTable()
    {
        var sql = ReadIntelligenceSql("025_observed_inventory_daily_snapshot_v1.sql");

        Assert.Contains("CREATE TABLE IF NOT EXISTS analytics_intel.inventory_observed_daily_snapshot", sql, StringComparison.Ordinal);
        Assert.Contains("analytics_intel.vw_inventory_daily_stock_v1", sql, StringComparison.Ordinal);
        Assert.Contains("'observed'", sql, StringComparison.Ordinal);
        Assert.Contains("'reconstructed'", sql, StringComparison.Ordinal);
        Assert.Contains("'mixed'", sql, StringComparison.Ordinal);
        Assert.Contains("'missing'", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE pd.\"Kolicina\" IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO analytics_intel.inventory_observed_daily_snapshot", StripCaptureFunction(sql), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FROM analytics_intel.vw_inventory_risk_signals_v1", ExtractCaptureFunction(sql), StringComparison.Ordinal);
        Assert.DoesNotContain("coalesce(observed_qty, 0)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(reconstructed_qty, 0)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(stock_qty, 0)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandlerSql_DoesNotCoalesceQuantitiesToZero()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "Application",
            "Analytics",
            "Queries",
            "GetObservedInventoryDailySnapshot",
            "GetObservedInventoryDailySnapshotHandler.cs"));

        Assert.Contains("from analytics_intel.vw_inventory_daily_stock_v1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("coalesce(observed_qty, 0)", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(reconstructed_qty, 0)", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(stock_qty, 0)", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("date < @toDate", source, StringComparison.Ordinal);
    }

    private static decimal? ParseDecimal(string? value) =>
        value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static string ReadIntelligenceSql(string fileName)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Database", "Analytics", "Intelligence", fileName);
        if (File.Exists(outputPath))
            return File.ReadAllText(outputPath);

        return File.ReadAllText(Path.Combine(FindRepoRoot(), "Database", "Analytics", "Intelligence", fileName));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln"))
                || File.Exists(Path.Combine(directory.FullName, "MASTER_ROADMAP.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output.");
    }

    private static string ExtractCaptureFunction(string sql)
    {
        var start = sql.IndexOf("CREATE OR REPLACE FUNCTION analytics_intel.capture_observed_inventory_daily", StringComparison.Ordinal);
        var end = sql.IndexOf("COMMENT ON FUNCTION analytics_intel.capture_observed_inventory_daily", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return sql[start..end];
    }

    private static string StripCaptureFunction(string sql)
    {
        var start = sql.IndexOf("CREATE OR REPLACE FUNCTION analytics_intel.capture_observed_inventory_daily", StringComparison.Ordinal);
        var end = sql.IndexOf("DO $$", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return sql[..start] + sql[end..];
    }
}
