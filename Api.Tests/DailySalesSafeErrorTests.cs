using Xunit;

namespace Api.Tests;

public sealed class DailySalesSafeErrorTests
{
    [Fact]
    public void DailySalesEndpointUsesSafeTraceableProblemContracts()
    {
        var source = ReadRepoFile("Api/Endpoints/DailySalesStatsEndpoints.cs");
        var start = source.IndexOf("app.MapGet(\"/api/analytics/daily-sales\",", StringComparison.Ordinal);
        var end = source.IndexOf(".WithName(\"GetDailySalesStats\")", start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        var endpoint = source[start..end];
        Assert.Contains("CreateDailySalesStatsProblem", endpoint);
        Assert.Contains("daily_sales_stats_cancelled", endpoint);
        Assert.Contains("daily_sales_stats_timeout", endpoint);
        Assert.Contains("daily_sales_stats_database_unavailable", endpoint);
        Assert.Contains("daily_sales_stats_unavailable", endpoint);
        Assert.Contains("AllEndpoints.ResolveAnalyticsCorrelationId(httpContext)", endpoint);
        Assert.Contains("CorrelationId={CorrelationId}", endpoint);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", endpoint);
        Assert.DoesNotContain("detail: ex.Message", endpoint);
    }

    [Fact]
    public void DailySalesProblemHelperReturnsOnlySafeDetailAndTraceId()
    {
        var source = ReadRepoFile("Api/Endpoints/DailySalesStatsEndpoints.cs");
        var helperStart = source.IndexOf("private static IResult CreateDailySalesStatsProblem", StringComparison.Ordinal);
        var helperEnd = source.IndexOf("private static DateTime? NormalizeUtcDate", helperStart, StringComparison.Ordinal);

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
