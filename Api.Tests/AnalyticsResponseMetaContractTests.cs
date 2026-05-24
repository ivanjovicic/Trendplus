using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Verifies that AnalyticsResponseMetaFactory and response DTOs respect the
/// backend meta contract rules (Rule A-D from the spec).
/// </summary>
public sealed class AnalyticsResponseMetaContractTests
{
    // ──────────────────────────────────────────────────────────────────
    // Factory: Success (Rule A – rows > 0)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Factory_Success_ReturnsSuccessTrue_WithDefaults()
    {
        var meta = AnalyticsResponseMetaFactory.Success();

        Assert.True(meta.Success);
        Assert.False(meta.IsPartial);
        Assert.Null(meta.ErrorCode);
        Assert.Null(meta.EmptyReason);
        Assert.NotEqual(default, meta.GeneratedAtUtc);
    }

    [Fact]
    public void Factory_Success_WithDataQualityStatus_Propagates()
    {
        var meta = AnalyticsResponseMetaFactory.Success(dataQualityStatus: "warning");

        Assert.True(meta.Success);
        Assert.Equal("warning", meta.DataQualityStatus);
    }

    // ──────────────────────────────────────────────────────────────────
    // Factory: Empty (Rule B – rows = 0)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Factory_Empty_ReturnsSuccessTrue_InsufficientData_EmptyReason()
    {
        var meta = AnalyticsResponseMetaFactory.Empty(
            emptyReason: "no_rows_for_period",
            message: "Nema podataka za izabrani period.");

        Assert.True(meta.Success);
        Assert.Equal("insufficient_data", meta.DataQualityStatus);
        Assert.Equal("no_rows_for_period", meta.EmptyReason);
        Assert.NotNull(meta.Message);
        Assert.Null(meta.ErrorCode);
        Assert.False(meta.IsPartial);
    }

    [Fact]
    public void Factory_Empty_CustomDataQualityStatus_IsRespected()
    {
        var meta = AnalyticsResponseMetaFactory.Empty(
            emptyReason: "no_open_issues",
            message: "Nema problema.",
            dataQualityStatus: "good");

        Assert.True(meta.Success);
        Assert.Equal("good", meta.DataQualityStatus);
        Assert.Equal("no_open_issues", meta.EmptyReason);
    }

    // ──────────────────────────────────────────────────────────────────
    // Factory: Warning (Rule C – partial / stale)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Factory_Warning_ReturnsSuccessTrue_IsPartialTrue_WithWarningCode()
    {
        var meta = AnalyticsResponseMetaFactory.Warning(
            warningCode: "ANALYTICS_PARTIAL_DATA",
            warningMessage: "Deo sekcija nije dostupan.");

        Assert.True(meta.Success);
        Assert.True(meta.IsPartial);
        Assert.Equal("ANALYTICS_PARTIAL_DATA", meta.WarningCode);
        Assert.Equal("Deo sekcija nije dostupan.", meta.WarningMessage);
        Assert.Equal(meta.WarningMessage, meta.Message);
        Assert.Null(meta.ErrorCode);
        Assert.Null(meta.EmptyReason);
    }

    [Fact]
    public void Factory_Warning_WithDataQualityStatus_Propagates()
    {
        var meta = AnalyticsResponseMetaFactory.Warning(
            warningCode: "STALE_DATA",
            warningMessage: "Podaci su stariji od 24h.",
            dataQualityStatus: "warning");

        Assert.True(meta.Success);
        Assert.Equal("warning", meta.DataQualityStatus);
        Assert.True(meta.IsPartial);
    }

    // ──────────────────────────────────────────────────────────────────
    // Factory: Error (Rule D – SQL exception / timeout)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Factory_Error_ReturnsSuccessFalse_WithErrorFields()
    {
        var correlationId = Guid.NewGuid().ToString();
        var meta = AnalyticsResponseMetaFactory.Error(
            errorCode: "ANALYTICS_TIMEOUT",
            errorMessage: "Isteklo vreme upita.",
            correlationId: correlationId);

        Assert.False(meta.Success);
        Assert.Equal("ANALYTICS_TIMEOUT", meta.ErrorCode);
        Assert.Equal("Isteklo vreme upita.", meta.ErrorMessage);
        Assert.Equal(correlationId, meta.CorrelationId);
        Assert.Null(meta.EmptyReason);
        Assert.False(meta.IsPartial);
    }

    [Fact]
    public void Factory_Error_IsNotFakeZero_SuccessIsFalse()
    {
        // Rule D: a backend error must never look like a success with 0 RSD.
        var meta = AnalyticsResponseMetaFactory.Error(
            errorCode: "ANALYTICS_DB_UNAVAILABLE",
            errorMessage: "Baza nije dostupna.",
            correlationId: null);

        Assert.False(meta.Success, "Backend DB error must set Success=false, not return fake-zero success.");
        Assert.NotNull(meta.ErrorCode);
    }

    // ──────────────────────────────────────────────────────────────────
    // Contract: ProductDecisionCenter empty state shape
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ProductDecisionCenter_EmptyResponse_HasInsufficientDataMeta()
    {
        // Simulate what BuildProductDecisionCenterAsync returns when articles.Count == 0.
        var dto = new ProductDecisionCenterResponseDto
        {
            TotalRows = 0,
            Rows = [],
            Summary = new ProductDecisionCenterSummaryDto(),
            Meta = new AnalyticsResponseMetaDto
            {
                Success = true,
                DataQualityStatus = "insufficient_data",
                EmptyReason = "no_rows_for_period",
                Message = "Nema podataka za izabrani period i filtere.",
                GeneratedAtUtc = DateTime.UtcNow
            }
        };

        Assert.NotNull(dto.Meta);
        Assert.True(dto.Meta.Success);
        Assert.Equal("insufficient_data", dto.Meta.DataQualityStatus);
        Assert.Equal("no_rows_for_period", dto.Meta.EmptyReason);
        Assert.Empty(dto.Rows);
    }

    // ──────────────────────────────────────────────────────────────────
    // Contract: Exception paths must return error meta, not fake zero
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DashboardBootstrap_TimeoutException_ResponseHasErrorMeta()
    {
        // Simulate what the bootstrap handler returns on OperationCanceledException.
        var correlationId = "test-corr-001";
        var dto = new AnalyticsDashboardBootstrapDto
        {
            Errors = ["Dashboard bootstrap fallback: request timed out."],
            Meta = AnalyticsResponseMetaFactory.Error(
                errorCode: "ANALYTICS_TIMEOUT",
                errorMessage: "Dashboard podaci trenutno nisu dostupni zbog isteka vremena.",
                correlationId: correlationId)
        };

        Assert.NotNull(dto.Meta);
        Assert.False(dto.Meta.Success, "Timeout must not appear as success.");
        Assert.Equal("ANALYTICS_TIMEOUT", dto.Meta.ErrorCode);
        Assert.Equal(correlationId, dto.Meta.CorrelationId);
        Assert.NotEmpty(dto.Errors);
    }

    [Fact]
    public void ProductDecisionCenter_DbException_ResponseHasErrorMeta_NotFakeZero()
    {
        // Simulate what the PDC handler returns on NpgsqlException.
        var dto = new ProductDecisionCenterResponseDto
        {
            Meta = AnalyticsResponseMetaFactory.Error(
                errorCode: "ANALYTICS_DB_UNAVAILABLE",
                errorMessage: "Product Decision Center podaci trenutno nisu dostupni zbog greske baze.",
                correlationId: "test-corr-002")
        };

        Assert.NotNull(dto.Meta);
        Assert.False(dto.Meta.Success, "DB error must set Success=false.");
        // The row list must be empty – there should be 0 rows, but that is NOT the same
        // as a successful empty query. The meta distinguishes these two cases.
        Assert.Empty(dto.Rows);
        Assert.Equal("ANALYTICS_DB_UNAVAILABLE", dto.Meta.ErrorCode);
    }

    [Fact]
    public void SupplierDecisionHub_UnavailableException_ResponseHasErrorMeta()
    {
        // Simulate what the summary endpoint returns on SupplierDecisionUnavailableException.
        var meta = AnalyticsResponseMetaFactory.Error(
            errorCode: "ANALYTICS_DB_UNAVAILABLE",
            errorMessage: "Supplier Decision Hub nije dostupan.",
            correlationId: "test-corr-003");

        Assert.False(meta.Success);
        Assert.Equal("ANALYTICS_DB_UNAVAILABLE", meta.ErrorCode);
    }

    // ──────────────────────────────────────────────────────────────────
    // Contract: Bootstrap partial success sets IsPartial
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DashboardBootstrap_PartialSections_MetaIsPartialAndHasWarningCode()
    {
        // Simulate BuildSuccessMeta called with some errors present.
        var meta = AnalyticsResponseMetaFactory.Warning(
            warningCode: "ANALYTICS_PARTIAL_DATA",
            warningMessage: "Deo dashboard sekcija nije trenutno dostupan.");

        Assert.True(meta.Success);
        Assert.True(meta.IsPartial);
        Assert.Equal("ANALYTICS_PARTIAL_DATA", meta.WarningCode);
    }
}
