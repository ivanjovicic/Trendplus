using Xunit;

namespace Api.Tests;

public sealed class ColorSalesStatsSafeErrorTests
{
    [Fact]
    public void ColorEndpointUsesSafeTraceableProblemContracts()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var start = source.IndexOf("app.MapGet(\"/api/analytics/color-sales-stats\",", StringComparison.Ordinal);
        var end = source.IndexOf(".WithName(\"GetColorSalesStats\")", start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        var endpoint = source[start..end];
        Assert.Contains("CreateColorSalesStatsProblem", endpoint);
        Assert.Contains("color_sales_stats_cancelled", endpoint);
        Assert.Contains("color_sales_stats_database_unavailable", endpoint);
        Assert.Contains("color_sales_stats_unavailable", endpoint);
        Assert.Contains("ResolveAnalyticsCorrelationId(httpContext)", endpoint);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", endpoint);
        Assert.DoesNotContain("detail: ex.Message", endpoint);
        Assert.DoesNotContain("title: \"Greska pri ucitavanju statistike prodaje po boji artikla\"", endpoint);
    }

    [Fact]
    public void ColorProblemHelperPersistsOnlySafeDetailAndTraceId()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var helperStart = source.IndexOf("private static IResult CreateColorSalesStatsProblem", StringComparison.Ordinal);
        var helperEnd = source.IndexOf("private static void AddVendorSalesNivelacijaScopeParameters", helperStart, StringComparison.Ordinal);

        Assert.True(helperStart >= 0);
        Assert.True(helperEnd > helperStart);

        var helper = source[helperStart..helperEnd];
        Assert.Contains("Referentni ID:", helper);
        Assert.Contains("[\"errorCode\"]", helper);
        Assert.Contains("[\"correlationId\"]", helper);
        Assert.DoesNotContain("ex.Message", helper);
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
