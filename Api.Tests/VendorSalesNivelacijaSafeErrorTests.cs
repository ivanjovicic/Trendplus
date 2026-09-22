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
