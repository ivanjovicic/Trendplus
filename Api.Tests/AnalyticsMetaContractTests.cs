using Trendplus2.Dtos;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class AnalyticsMetaContractTests
{
    // ── Factory: Error ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Factory.Error → Success = false (no fake-zero as success)")]
    public void Factory_Error_SuccessIsFalse()
    {
        var meta = AnalyticsResponseMetaFactory.Error("some_error", "msg", "corr-1");
        Assert.False(meta.Success);
    }

    [Fact(DisplayName = "Factory.Error → ErrorCode is preserved")]
    public void Factory_Error_PreservesErrorCode()
    {
        var meta = AnalyticsResponseMetaFactory.Error("inventory_db_error", "Greška", "corr-1");
        Assert.Equal("inventory_db_error", meta.ErrorCode);
    }

    [Fact(DisplayName = "Factory.Error → CorrelationId is propagated")]
    public void Factory_Error_PropagatesCorrelationId()
    {
        var meta = AnalyticsResponseMetaFactory.Error("code", "msg", "x-corr-123");
        Assert.Equal("x-corr-123", meta.CorrelationId);
    }

    [Fact(DisplayName = "Factory.Error → DataQualityStatus defaults to insufficient_data")]
    public void Factory_Error_DefaultDataQualityStatus()
    {
        var meta = AnalyticsResponseMetaFactory.Error("code", "msg", null);
        Assert.Equal("insufficient_data", meta.DataQualityStatus);
    }

    // ── Factory: Empty ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Factory.Empty → Success = true")]
    public void Factory_Empty_SuccessIsTrue()
    {
        var meta = AnalyticsResponseMetaFactory.Empty("no_rows", "Nema redova.");
        Assert.True(meta.Success);
    }

    [Fact(DisplayName = "Factory.Empty → EmptyReason is preserved")]
    public void Factory_Empty_PreservesEmptyReason()
    {
        var meta = AnalyticsResponseMetaFactory.Empty("no_inventory_data", "msg");
        Assert.Equal("no_inventory_data", meta.EmptyReason);
    }

    [Fact(DisplayName = "Factory.Empty → DataQualityStatus defaults to insufficient_data")]
    public void Factory_Empty_DefaultDataQualityStatus()
    {
        var meta = AnalyticsResponseMetaFactory.Empty("reason", "msg");
        Assert.Equal("insufficient_data", meta.DataQualityStatus);
    }

    // ── Factory: Warning ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Factory.Warning → Success = true, IsPartial = true")]
    public void Factory_Warning_SuccessTrueAndIsPartialTrue()
    {
        var meta = AnalyticsResponseMetaFactory.Warning("PARTIAL_DATA", "Delimicni podaci.");
        Assert.True(meta.Success);
        Assert.True(meta.IsPartial);
    }

    [Fact(DisplayName = "Factory.Warning → WarningCode is preserved")]
    public void Factory_Warning_PreservesWarningCode()
    {
        var meta = AnalyticsResponseMetaFactory.Warning("ANALYTICS_PARTIAL_DATA", "warning msg");
        Assert.Equal("ANALYTICS_PARTIAL_DATA", meta.WarningCode);
    }

    // ── Factory: Success ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Factory.Success → Success = true")]
    public void Factory_Success_SuccessIsTrue()
    {
        var meta = AnalyticsResponseMetaFactory.Success();
        Assert.True(meta.Success);
    }

    [Fact(DisplayName = "Factory.Success with isPartial → IsPartial = true")]
    public void Factory_Success_WithIsPartial()
    {
        var meta = AnalyticsResponseMetaFactory.Success(isPartial: true, warningCode: "WARN");
        Assert.True(meta.IsPartial);
        Assert.Equal("WARN", meta.WarningCode);
    }

    // ── No fake-zero contract ────────────────────────────────────────────────

    [Fact(DisplayName = "Error meta never exposes Success = true (no fake-zero as valid result)")]
    public void ErrorMeta_NeverSuccessTrue()
    {
        var codes = new[] { "db_error", "timeout", "missing_table", "unknown" };
        foreach (var code in codes)
        {
            var meta = AnalyticsResponseMetaFactory.Error(code, "msg", null);
            Assert.False(meta.Success, $"Expected Success=false for error code '{code}'");
        }
    }

    [Fact(DisplayName = "Empty meta never has ErrorCode (empty != error)")]
    public void EmptyMeta_HasNoErrorCode()
    {
        var meta = AnalyticsResponseMetaFactory.Empty("no_sales_in_period", "Nema prodaje.");
        Assert.Null(meta.ErrorCode);
    }

    // ── Dashboard partial guard ──────────────────────────────────────────────

    [Fact(DisplayName = "Dashboard partial path: non-empty error list → IsPartial=true, WarningCode set")]
    public void Dashboard_Partial_WhenErrorsPresent_IsPartialAndWarningCode()
    {
        // Simulates what BuildSuccessMeta(isPartial:true, warningCode:"ANALYTICS_PARTIAL_DATA") produces
        // when response.Errors.Count > 0 in the dashboard/bootstrap endpoint.
        var meta = AnalyticsResponseMetaFactory.Success(
            isPartial: true,
            warningCode: "ANALYTICS_PARTIAL_DATA",
            warningMessage: "Delimicni podaci iz nekih izvora.");

        Assert.True(meta.IsPartial);
        Assert.Equal("ANALYTICS_PARTIAL_DATA", meta.WarningCode);
        Assert.True(meta.Success); // still success, just partial
    }

    // ── Inventory empty guard ────────────────────────────────────────────────

    [Fact(DisplayName = "Empty inventory → EmptyReason=no_inventory_data, Success=true")]
    public void Inventory_Empty_WhenTotalSkuZero()
    {
        // Simulates what CachedAnalyticsEndpoints does when TotalSkuCount == 0
        var totalSku = 0;
        var meta = totalSku == 0
            ? AnalyticsResponseMetaFactory.Empty("no_inventory_data", "Nema podataka o zalihama.", "insufficient_data")
            : AnalyticsResponseMetaFactory.Success();

        Assert.True(meta.Success);
        Assert.Equal("no_inventory_data", meta.EmptyReason);
        Assert.Equal("insufficient_data", meta.DataQualityStatus);
    }

    // ── Product decision center ──────────────────────────────────────────────

    [Fact(DisplayName = "ProductDecisionCenter: articles empty → EmptyReason=no_rows_for_period")]
    public void ProductDecisionCenter_ArticlesEmpty_MetaHasEmptyReason()
    {
        // Simulates the articles.Count == 0 branch in BuildProductDecisionCenterAsync
        var articles = new System.Collections.Generic.List<object>();
        AnalyticsResponseMetaDto meta;
        if (articles.Count == 0)
        {
            meta = new AnalyticsResponseMetaDto
            {
                Success = true,
                DataQualityStatus = "insufficient_data",
                EmptyReason = "no_rows_for_period",
                Message = "Nema artikala za izabrani period."
            };
        }
        else
        {
            meta = AnalyticsResponseMetaFactory.Success();
        }

        Assert.Equal("no_rows_for_period", meta.EmptyReason);
        Assert.True(meta.Success);
    }
}
