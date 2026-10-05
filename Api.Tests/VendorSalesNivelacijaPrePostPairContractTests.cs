using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public sealed class VendorSalesNivelacijaPrePostPairContractTests
{
    [Fact(DisplayName = "Pre-post pair route and enrichment flag are wired")]
    public void PrePostPair_EndpointAndEnrichmentFlag_ExistInSource()
    {
        var source = File.ReadAllText(FindRepoFile("Api/Endpoints/AllEndpoints.cs"));

        Assert.Contains("/api/analytics/vendor-sales-nivelacija/pre-post-pair", source);
        Assert.Contains("bool includeEnrichment = true", source);
        Assert.Contains("includeEnrichment: false", source);
        Assert.Contains("GetVendorSalesNivelacijaPrePostPair", source);
    }

    [Fact(DisplayName = "Nivelacija invalidation clears pre-post family")]
    public void InvalidateNivelacija_ClearsPrePostFamily()
    {
        var source = File.ReadAllText(FindRepoFile("Api/Endpoints/AllEndpoints.cs"));
        Assert.Contains("ClearAsync(AnalyticsCachePolicy.PrePostFamily", source);
        Assert.Contains("await InvalidateNivelacijaAnalyticsCachesAsync(cacheAdmin, ct);", source);
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relativePath}.");
    }
}
