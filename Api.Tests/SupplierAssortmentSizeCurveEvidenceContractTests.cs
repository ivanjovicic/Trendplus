using System.Reflection;
using Application.Analytics;
using Application.Analytics.Queries.GetInventorySizeCurve;
using Xunit;

namespace Api.Tests;

public sealed class SupplierAssortmentSizeCurveEvidenceContractTests
{
    [Fact]
    public void Contract_PinsSupplierFootwearSizeCurveAsUnavailable()
    {
        Assert.False(SupplierAssortmentSizeCurveEvidenceContract.SupplierFootwearSizeCurveAvailable);
        Assert.False(SupplierAssortmentSizeCurveEvidenceContract.ControlledMarkdownUpliftAvailable);
        Assert.Contains("missing_sold_received_on_hand_by_size", SupplierAssortmentSizeCurveEvidenceContract.SizeCurveUnavailableReasonCodes);
        Assert.Contains("raw_pre_post_not_causal_uplift", SupplierAssortmentSizeCurveEvidenceContract.ControlledUpliftUnavailableReasonCodes);
    }

    [Fact]
    public void InventorySizeCurveDto_DoesNotExposeSoldReceivedOnHandBySupplierFootwearType()
    {
        var dtoProps = typeof(InventorySizeCurveDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var propertyName in SupplierAssortmentSizeCurveEvidenceContract.MissingSupplierAggregationPropertyNames)
        {
            Assert.DoesNotContain(propertyName, dtoProps);
        }

        Assert.Contains("ActualSizeShare", dtoProps);
        Assert.Contains("SizeCode", dtoProps);
    }

    [Fact]
    public void InventorySizeCurveHandler_ReadsShareSnapshotOnly()
    {
        var handlerPath = Path.Combine(
            GetRepoRoot(),
            "Application",
            "Analytics",
            "Queries",
            "GetInventorySizeCurve",
            "GetInventorySizeCurveHandler.cs");
        var source = File.ReadAllText(handlerPath);

        Assert.Contains(SupplierAssortmentSizeCurveEvidenceContract.SizeCurveSnapshotRelation, source, StringComparison.Ordinal);
        Assert.Contains("actual_size_share", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sold_qty", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("received_qty", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("on_hand", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("footwear_type", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repository_DoesNotDefineAnalyticsSizeCurveSnapshotMigration()
    {
        var databaseRoot = Path.Combine(GetRepoRoot(), "Database");
        var migrationHits = Directory.EnumerateFiles(databaseRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(
                SupplierAssortmentSizeCurveEvidenceContract.SizeCurveSnapshotRelation,
                StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(migrationHits);
    }

    [Theory]
    [InlineData(false, 0, 2, "immature")]
    [InlineData(false, 2, 0, "ineffective")]
    [InlineData(true, 1, 0, "insufficient_data")]
    public void PriceChangeEffectPolicy_NeverMarksRecommendationAllowedOrCausalDiD(
        bool unknownVendor,
        int matureComparable,
        int immatureComparable,
        string expectedStatus)
    {
        var result = VendorSalesNivelacijaPriceChangeEffectPolicy.Evaluate(
            new VendorSalesNivelacijaPriceChangeEffectPolicy.VendorAggregateInput(
                IsUnknownVendor: unknownVendor,
                PreRevenue: 100m,
                PostRevenue: 80m,
                PreQty: 5,
                PostQty: 4,
                ComparableArticleCount: matureComparable + immatureComparable,
                MatureComparableArticleCount: matureComparable,
                ImmatureComparableArticleCount: immatureComparable,
                SemanticChangePercentRevenue: -20d,
                MarginPct: 35d,
                MarginCoveragePct: 80d,
                SplitCoveragePct: 70d));

        Assert.Equal(expectedStatus, result.Status);
        Assert.False(result.RecommendationAllowed);
        Assert.DoesNotContain("did", result.ReasonCodes, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Trendplus2.Backend.slnf"))
                || Directory.Exists(Path.Combine(dir.FullName, "Application")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found from test base directory.");
    }
}
