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

    [Fact]
    public void Factory_StaleCacheWarning_ReturnsPartialWarningMeta()
    {
        var meta = AnalyticsResponseMetaFactory.StaleCacheWarning();

        Assert.True(meta.Success);
        Assert.True(meta.IsPartial);
        Assert.Equal("STALE_CACHE", meta.WarningCode);
        Assert.Equal("warning", meta.DataQualityStatus);
        Assert.Equal(meta.WarningMessage, meta.Message);
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
    public void DashboardBootstrap_DbError_ReturnsErrorMetaInsteadOfZeroSummary()
    {
        var dto = new AnalyticsDashboardBootstrapDto
        {
            Summary = null,
            Errors = ["Dashboard bootstrap failed: db unavailable."],
            Meta = AnalyticsResponseMetaFactory.Error(
                errorCode: "ANALYTICS_DB_UNAVAILABLE",
                errorMessage: "Dashboard podaci nisu dostupni zbog greske baze.",
                correlationId: "db-corr-001")
        };

        Assert.False(dto.Meta.Success);
        Assert.Equal("ANALYTICS_DB_UNAVAILABLE", dto.Meta.ErrorCode);
        Assert.Equal("db-corr-001", dto.Meta.CorrelationId);
        Assert.Null(dto.Summary);
    }

    [Fact]
    public void ProductDecisionCenter_DbError_ReturnsErrorMeta()
    {
        var dto = new ProductDecisionCenterResponseDto
        {
            Meta = AnalyticsResponseMetaFactory.Error(
                errorCode: "ANALYTICS_DB_UNAVAILABLE",
                errorMessage: "Product Decision Center podaci trenutno nisu dostupni zbog greske baze.",
                correlationId: "test-corr-002")
        };

        Assert.False(dto.Meta.Success);
        Assert.Equal("ANALYTICS_DB_UNAVAILABLE", dto.Meta.ErrorCode);
        Assert.Empty(dto.Rows);
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
    public void ProductDecisionCenter_NoRows_ReturnsInsufficientDataMeta()
    {
        var dto = new ProductDecisionCenterResponseDto
        {
            TotalRows = 0,
            Rows = [],
            Summary = new ProductDecisionCenterSummaryDto(),
            Meta = AnalyticsResponseMetaFactory.Empty(
                emptyReason: "no_rows_for_period",
                message: "Nema artikala za izabrani period i filtere.",
                dataQualityStatus: "insufficient_data")
        };

        Assert.True(dto.Meta.Success);
        Assert.Equal("no_rows_for_period", dto.Meta.EmptyReason);
        Assert.Equal("insufficient_data", dto.Meta.DataQualityStatus);
        Assert.Empty(dto.Rows);
    }

    [Fact]
    public void Inventory_MissingDependency_ReturnsErrorMeta()
    {
        var response = new InventoryBalanceDto(
            TotalSku: 0,
            TotalOnHand: 0,
            LowStockCount: 0,
            OutOfStockCount: 0,
            EstimatedInventoryValue: 0m,
            Meta: AnalyticsResponseMetaFactory.Error(
                errorCode: "inventory_dependency_missing",
                errorMessage: "Inventory dependency nije dostupna.",
                correlationId: "inv-corr-001"));

        Assert.NotNull(response.Meta);
        Assert.False(response.Meta!.Success);
        Assert.Equal("inventory_dependency_missing", response.Meta.ErrorCode);
        Assert.Equal("inv-corr-001", response.Meta.CorrelationId);
    }

    [Fact]
    public void Inventory_EmptyDataset_ReturnsEmptyMeta()
    {
        var response = new InventoryBalanceDto(
            TotalSku: 0,
            TotalOnHand: 0,
            LowStockCount: 0,
            OutOfStockCount: 0,
            EstimatedInventoryValue: 0m,
            Meta: AnalyticsResponseMetaFactory.Empty(
                emptyReason: "no_inventory_data",
                message: "Nema podataka o zalihama.",
                dataQualityStatus: "insufficient_data"));

        Assert.True(response.Meta!.Success);
        Assert.Equal("no_inventory_data", response.Meta.EmptyReason);
        Assert.Equal("insufficient_data", response.Meta.DataQualityStatus);
    }

    [Fact]
    public void Inventory_PartialData_ReturnsWarningMeta()
    {
        var response = new InventoryBalanceDto(
            TotalSku: 125,
            TotalOnHand: 880,
            LowStockCount: 24,
            OutOfStockCount: 7,
            EstimatedInventoryValue: 1560000m,
            Meta: AnalyticsResponseMetaFactory.Warning(
                warningCode: "STALE_DATA",
                warningMessage: "Podaci su delimicno zastareli.",
                dataQualityStatus: "warning"));

        Assert.True(response.Meta!.Success);
        Assert.True(response.Meta.IsPartial);
        Assert.Equal("STALE_DATA", response.Meta.WarningCode);
        Assert.Equal("warning", response.Meta.DataQualityStatus);
    }

    [Fact]
    public void PreNivelacijaPriority_EmptyDataset_ReturnsEmptyMeta()
    {
        var response = new Api.Models.PreNivelacijaPriorityResponseDto
        {
            TotalCandidates = 0,
            Candidates = [],
            Meta = AnalyticsResponseMetaFactory.Empty(
                emptyReason: "no_data_in_period",
                message: "Nema prodaje za izabrani period.",
                dataQualityStatus: "insufficient_data")
        };

        Assert.True(response.Meta!.Success);
        Assert.Equal("no_data_in_period", response.Meta.EmptyReason);
        Assert.Equal("insufficient_data", response.Meta.DataQualityStatus);
        Assert.Empty(response.Candidates);
    }

    [Fact]
    public void PrePostNivelacija_EmptyDataset_ReturnsEmptyMeta()
    {
        var response = new Api.Models.VendorSalesNivelacijaResponseDto
        {
            Meta = AnalyticsResponseMetaFactory.Empty(
                emptyReason: "no_data_in_period",
                message: "Nema podataka za pre/post nivelaciju.",
                dataQualityStatus: "insufficient_data")
        };

        Assert.True(response.Meta!.Success);
        Assert.Equal("no_data_in_period", response.Meta.EmptyReason);
        Assert.Equal("insufficient_data", response.Meta.DataQualityStatus);
    }

    [Fact]
    public void PrePostNivelacija_Exception_ReturnsErrorMetaWithCorrelationId()
    {
        var response = new Api.Models.VendorSalesNivelacijaResponseDto
        {
            Meta = AnalyticsResponseMetaFactory.Error(
                errorCode: "vendor_sales_nivelacija_error",
                errorMessage: "Pre/post nivelacija nije dostupna.",
                correlationId: "nivelacija-corr-001")
        };

        Assert.NotNull(response.Meta);
        Assert.False(response.Meta!.Success);
        Assert.Equal("vendor_sales_nivelacija_error", response.Meta.ErrorCode);
        Assert.Equal("nivelacija-corr-001", response.Meta.CorrelationId);
    }

    [Fact]
    public void PrePostNivelacija_Exception_ReturnsErrorMetaWithCorrelation()
    {
        var response = new Api.Models.VendorSalesNivelacijaResponseDto
        {
            Meta = AnalyticsResponseMetaFactory.Error(
                errorCode: "vendor_sales_nivelacija_error",
                errorMessage: "Pre/post nivelacija nije dostupna.",
                correlationId: "nivelacija-corr-002")
        };

        Assert.False(response.Meta!.Success);
        Assert.Equal("vendor_sales_nivelacija_error", response.Meta.ErrorCode);
        Assert.Equal("nivelacija-corr-002", response.Meta.CorrelationId);
    }

    [Fact]
    public void PrePostNivelacija_CacheHitWithRows_DerivesSuccessMetaAndCorrelation()
    {
        var response = new Api.Models.VendorSalesNivelacijaResponseDto
        {
            VendorStats = [new Api.Models.VendorSalesNivelacijaVendorStatDto { VendorId = 10, VendorName = "Alpha", PostRevenue = 120000m }],
            ArticleStats = [new Api.Models.VendorSalesNivelacijaArticleStatDto { VendorId = 10, VendorName = "Alpha", Sku = "SKU-10", ArticleName = "Model X", PostRevenue = 120000m }]
        };

        var patched = AllEndpoints.ApplyVendorSalesNivelacijaMeta(response, "nivelacija-cache-hit-001");

        Assert.True(patched.Meta!.Success);
        Assert.False(patched.Meta.IsPartial);
        Assert.Null(patched.Meta.EmptyReason);
        Assert.Equal("good", patched.Meta.DataQualityStatus);
        Assert.Equal("nivelacija-cache-hit-001", patched.Meta.CorrelationId);
    }

    [Fact]
    public void PrePostNivelacija_CacheHitWithoutRows_DerivesEmptyMetaAndCorrelation()
    {
        var response = new Api.Models.VendorSalesNivelacijaResponseDto();

        var patched = AllEndpoints.ApplyVendorSalesNivelacijaMeta(response, "nivelacija-cache-hit-002");

        Assert.True(patched.Meta!.Success);
        Assert.Equal("no_data_in_period", patched.Meta.EmptyReason);
        Assert.Null(patched.Meta.ErrorCode);
        Assert.Equal("nivelacija-cache-hit-002", patched.Meta.CorrelationId);
    }

    [Fact]
    public void PrePostNivelacija_CacheHitWithGlobalWarnings_DerivesWarningMetaAndCorrelation()
    {
        var response = new Api.Models.VendorSalesNivelacijaResponseDto
        {
            VendorStats = [new Api.Models.VendorSalesNivelacijaVendorStatDto { VendorId = 10, VendorName = "Alpha", PostRevenue = 120000m }],
            ArticleStats = [new Api.Models.VendorSalesNivelacijaArticleStatDto { VendorId = 10, VendorName = "Alpha", Sku = "SKU-10", ArticleName = "Model X", PostRevenue = 120000m }],
            MetricsStatus = "Metrics mapping failed; OOS/DiD mapping failed"
        };

        var patched = AllEndpoints.ApplyVendorSalesNivelacijaMeta(response, "nivelacija-cache-hit-003");

        Assert.True(patched.Meta!.Success);
        Assert.True(patched.Meta.IsPartial);
        Assert.Equal("vendor_sales_nivelacija_warning", patched.Meta.WarningCode);
        Assert.Equal("nivelacija-cache-hit-003", patched.Meta.CorrelationId);
    }

    [Fact]
    public void PrePostNivelacija_CacheHitFallbackPayload_DerivesErrorMetaAndCorrelation()
    {
        var response = new Api.Models.VendorSalesNivelacijaResponseDto
        {
            MetricsStatus = "Vendor sales nivelacija analytics fallback due to schema mismatch.",
            Insights =
            [
                new Api.Models.VendorSalesNivelacijaInsightDto
                {
                    Title = "Podaci privremeno nedostupni",
                    Value = "Fallback mode",
                    Details = "Schema mismatch",
                    Tone = "warning"
                }
            ]
        };

        var patched = AllEndpoints.ApplyVendorSalesNivelacijaMeta(response, "nivelacija-cache-hit-004");

        Assert.False(patched.Meta!.Success);
        Assert.Equal("vendor_sales_nivelacija_error", patched.Meta.ErrorCode);
        Assert.Equal("nivelacija-cache-hit-004", patched.Meta.CorrelationId);
        Assert.Empty(patched.ArticleStats);
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

    // ──────────────────────────────────────────────────────────────────
    // No-fake-zero regression: inventory empty vs error
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Inventory_EmptyBalance_HasEmptyMetaNotFakeZero()
    {
        // TotalSku=0 because the store has no articles → real empty state, not a fake zero from an error
        var response = new InventoryBalanceDto(
            TotalSku: 0,
            TotalOnHand: 0,
            LowStockCount: 0,
            OutOfStockCount: 0,
            EstimatedInventoryValue: 0m,
            Meta: AnalyticsResponseMetaFactory.Empty(
                emptyReason: "no_inventory_data",
                message: "Nema podataka o zalihama.",
                dataQualityStatus: null));

        // Empty = success=true with EmptyReason set, distinct from error (ErrorCode null)
        Assert.True(response.Meta!.Success);
        Assert.Equal("no_inventory_data", response.Meta.EmptyReason);
        Assert.Null(response.Meta.ErrorCode);
        Assert.Equal(0, response.TotalSku);
    }
}
