using Xunit;

namespace Api.Tests;

public sealed class ShoeTypeSalesSafeErrorTests
{
    [Fact]
    public void ShoeTypeEndpointUsesSafeTraceableProblemContract()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var start = source.IndexOf("app.MapGet(\"/api/analytics/shoe-type-sales-stats\",", StringComparison.Ordinal);
        var end = source.IndexOf(".WithName(\"GetShoeTypeSalesStats\")", start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        var endpoint = source[start..end];
        Assert.Contains("CreateShoeTypeSalesStatsProblem", endpoint);
        Assert.Contains("shoe_type_sales_stats_unavailable", endpoint);
        Assert.Contains("ResolveAnalyticsCorrelationId(httpContext)", endpoint);
        Assert.Contains("CorrelationId={CorrelationId}", endpoint);
        Assert.DoesNotContain("detail: ex.Message", endpoint);
        Assert.DoesNotContain("Greska pri ucitavanju statistike prodaje po tipu obuce", endpoint);
    }

    [Fact]
    public void ShoeTypeProblemHelperReturnsOnlySafeDetailAndTraceId()
    {
        var source = ReadRepoFile("Api/Endpoints/AllEndpoints.cs");
        var helperStart = source.IndexOf("private static IResult CreateShoeTypeSalesStatsProblem", StringComparison.Ordinal);
        var helperEnd = source.IndexOf("internal static string ApplyColorSalesCacheMetadata", helperStart, StringComparison.Ordinal);

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
