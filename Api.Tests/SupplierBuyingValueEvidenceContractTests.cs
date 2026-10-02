using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class SupplierBuyingValueEvidenceContractTests
{
    [Fact]
    public void Contract_PinsInventorySnapshotSignalsAsProvenAndExtendedMetricsUnavailable()
    {
        Assert.True(SupplierBuyingValueEvidenceContract.InventorySnapshotBuyingSignalsAvailable);
        Assert.False(SupplierBuyingValueEvidenceContract.SupplierAggregatedDaysCoverAvailable);
        Assert.False(SupplierBuyingValueEvidenceContract.SupplierAggregatedSellThroughAvailable);
        Assert.False(SupplierBuyingValueEvidenceContract.SupplierGrossReturnRateAvailable);
        Assert.False(SupplierBuyingValueEvidenceContract.PurchaseOrderLeadTimeAvailable);
        Assert.Contains("purchase_order_lead_time_source_absent", SupplierBuyingValueEvidenceContract.UnavailableMetricReasonCodes);
    }

    [Fact]
    public void CachedInventoryBalanceEndpoint_AcceptsSupplierScopeFilter()
    {
        var endpointPath = Path.Combine(
            GetRepoRoot(),
            "Api",
            "Endpoints",
            "CachedAnalyticsEndpoints.cs");
        var source = File.ReadAllText(endpointPath);

        Assert.Contains("/inventory/balance", source, StringComparison.Ordinal);
        Assert.Contains("int? supplierId = null", source, StringComparison.Ordinal);
        Assert.Contains("IDDobavljac == supplierId.Value", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SupplierSalesStatsEndpoint_DoesNotExposeBuyingValueAggregateFields()
    {
        var endpointPath = Path.Combine(GetRepoRoot(), "Api", "Endpoints", "AllEndpoints.cs");
        var source = File.ReadAllText(endpointPath);

        Assert.DoesNotContain("daysCover", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sellThrough", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grossReturnRate", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leadTime", source, StringComparison.OrdinalIgnoreCase);
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
