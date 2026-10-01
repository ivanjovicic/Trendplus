using Api.Models;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class VendorSalesNivelacijaSafeErrorTests
{
    [Fact]
    public void MainAndOptionsEndpointsUseSafeLocalizedProblemContracts()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var mainStart = source.IndexOf("app.MapGet(\"/api/analytics/vendor-sales-nivelacija\",", StringComparison.Ordinal);
        var optionsStart = source.IndexOf("app.MapGet(\"/api/analytics/vendor-sales-nivelacija/options\"", StringComparison.Ordinal);
        var mainEnd = source.IndexOf("// Non-cached aliases", mainStart, StringComparison.Ordinal);
        var optionsEnd = source.IndexOf(".WithName(\"GetVendorSalesNivelacijaOptions\")", optionsStart, StringComparison.Ordinal);

        Assert.True(mainStart >= 0);
        Assert.True(optionsStart >= 0);
        Assert.True(mainEnd > mainStart);
        Assert.True(optionsEnd > optionsStart);
        Assert.Contains("CreateVendorSalesNivelacijaProblem", source);
        Assert.Contains("vendor_sales_nivelacija_invalid_period", source);
        Assert.Contains("vendor_sales_nivelacija_options_unavailable", source);
        Assert.Contains("Referentni ID:", source);
        Assert.Contains("var reason = $\"Pre/post nivelacija trenutno nije dostupna. Referentni ID: {correlationId}.\"", source);
        Assert.DoesNotContain("detail: ex.Message", source[mainStart..mainEnd]);
        Assert.DoesNotContain("detail: ex.Message", source[optionsStart..optionsEnd]);
        Assert.DoesNotContain("N/A", source[mainStart..mainEnd]);
        Assert.DoesNotContain("Metrics mapping failed", source[mainStart..mainEnd]);
        Assert.DoesNotContain("OOS/DiD mapping failed", source[mainStart..mainEnd]);
        Assert.DoesNotContain("Value = \"Fallback mode\"", source);
        Assert.Contains("Value = \"Rezervni režim\"", source);
    }

    [Fact]
    public void FallbackResponseKeepsErrorMetaAndDoesNotPersistProviderDetails()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var fallbackStart = source.IndexOf("private static VendorSalesNivelacijaResponseDto CreateVendorSalesNivelacijaFallbackResponse", StringComparison.Ordinal);
        var fallbackEnd = source.IndexOf("private sealed record SalesDataWindowCacheEntry", fallbackStart, StringComparison.Ordinal);

        Assert.True(fallbackStart >= 0);
        Assert.True(fallbackEnd > fallbackStart);
        var fallback = source[fallbackStart..fallbackEnd];
        Assert.Contains("Meta = meta", fallback);
        Assert.Contains("Details = reason", fallback);
        Assert.DoesNotContain("ex.Message", fallback);
    }

    [Fact]
    public void VendorDtosUseLocalizedUnavailableDefaults()
    {
        var source = ReadRepoFile("Api/Models/VendorSalesNivelacijaModels.cs");

        Assert.DoesNotContain("= \"N/A\"", source);
        Assert.DoesNotContain("= \"Insufficient data\"", source);
        Assert.Contains("= \"Nepoznato\"", source);
        Assert.Contains("= \"Nedovoljno podataka\"", source);
    }

    [Fact]
    public void ContractFailureMetaKeepsReadinessDiagnosticAndUnappliedPeriodAndScope()
    {
        var from = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(new DateTime(2026, 9, 30), DateTimeKind.Utc);
        var response = new VendorSalesNivelacijaResponseDto
        {
            From = from,
            To = to,
            DataScope = "imported",
            ScopeApplied = false,
            Meta = new AnalyticsResponseMetaDto
            {
                Success = false,
                ErrorCode = "vendor_sales_nivelacija_contract_missing",
                ErrorMessage = "Nedostaje očekivana analitička relacija.",
                RecommendationAllowed = false,
                RequestedPeriodFromUtc = from,
                RequestedPeriodToUtc = to,
                RequestedDataScope = "imported",
                DataScopeSource = "not_applied",
                ReadinessId = "vendor-sales-nivelacija-schema",
                RecoveryInstruction = "Pokrenite read-only proveru ugovora.",
                ContractDiagnostic = new AnalyticsContractDiagnosticDto
                {
                    Relation = "vw_vendor_sales_nivelacija",
                    MissingPart = "relation"
                }
            }
        };

        var meta = AllEndpoints.BuildVendorSalesNivelacijaMeta(response, "vendor-contract-test");

        Assert.False(meta.Success);
        Assert.False(meta.RecommendationAllowed);
        Assert.Equal("vendor-sales-nivelacija-schema", meta.ReadinessId);
        Assert.Equal("Pokrenite read-only proveru ugovora.", meta.RecoveryInstruction);
        Assert.NotNull(meta.ContractDiagnostic);
        Assert.Equal("vw_vendor_sales_nivelacija", meta.ContractDiagnostic.Relation);
        Assert.Equal(from, meta.RequestedPeriodFromUtc);
        Assert.Equal(to, meta.RequestedPeriodToUtc);
        Assert.Null(meta.EffectivePeriodFromUtc);
        Assert.Null(meta.EffectivePeriodToUtc);
        Assert.Equal("imported", meta.RequestedDataScope);
        Assert.Null(meta.EffectiveDataScope);
        Assert.Equal("not_applied", meta.DataScopeSource);
    }

    [Fact]
    public void SuccessfulEmptyMetaRemainsDistinctAndKeepsAppliedContext()
    {
        var from = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(new DateTime(2026, 9, 30), DateTimeKind.Utc);
        var response = new VendorSalesNivelacijaResponseDto
        {
            From = from,
            To = to,
            DataScope = "existing",
            ScopeApplied = true
        };

        var meta = AllEndpoints.BuildVendorSalesNivelacijaMeta(response, "vendor-empty-test");

        Assert.True(meta.Success);
        Assert.Equal("no_data_in_period", meta.EmptyReason);
        Assert.Equal(from, meta.RequestedPeriodFromUtc);
        Assert.Equal(to, meta.RequestedPeriodToUtc);
        Assert.Equal(from, meta.EffectivePeriodFromUtc);
        Assert.Equal(to, meta.EffectivePeriodToUtc);
        Assert.Equal("existing", meta.RequestedDataScope);
        Assert.Equal("existing", meta.EffectiveDataScope);
        Assert.Equal("vendor-sales-nivelacija", meta.DataScopeSource);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
