using Api.Services.Startup;
using Infrastructure.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsConnectionDiagnosticsCapabilityTests
{
    private static readonly MaterializedViewCapabilityResult Ready90 = new(
        "public", "mv_supplier_decision_score_cache_90d",
        Exists: true, IsPopulated: true, MissingColumns: Array.Empty<string>());

    private static readonly MaterializedViewCapabilityResult Ready180 = new(
        "public", "mv_supplier_decision_score_cache_180d",
        Exists: true, IsPopulated: true, MissingColumns: Array.Empty<string>());

    [Fact]
    public void BothWindowedCachesReady_DiagnosticsDoesNotReportMissingCapability()
    {
        Assert.False(AnalyticsConnectionDiagnosticsHostedService.HasUnavailableSupplierWindowedCache(
            Ready90, Ready180));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void MissingUnpopulatedOrStaleColumnCache_IsReportedUnavailable(
        bool exists,
        bool populated,
        bool hasMissingColumn)
    {
        var incomplete = new MaterializedViewCapabilityResult(
            "public",
            "mv_supplier_decision_score_cache_90d",
            exists,
            populated,
            hasMissingColumn ? new[] { "period_from" } : Array.Empty<string>());

        Assert.True(AnalyticsConnectionDiagnosticsHostedService.HasUnavailableSupplierWindowedCache(
            incomplete, Ready180));
        Assert.True(AnalyticsConnectionDiagnosticsHostedService.HasUnavailableSupplierWindowedCache(
            Ready90, incomplete));
    }
}
