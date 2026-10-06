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

    [Fact(DisplayName = "Pre-post pair loopback query escapes round-trip date offsets")]
    public void PrePostPair_LoopbackQuery_EscapesDateOffsets()
    {
        var from = new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.FromHours(2)).DateTime;
        var withOffset = DateTime.SpecifyKind(from, DateTimeKind.Local);
        var query = Api.Services.VendorSalesNivelacijaInternalHttpLoader.BuildQueryString(
            vendorId: null,
            eventDate: null,
            from: withOffset,
            to: new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            category: null,
            includeInactive: false,
            maxRows: 5000,
            storeId: 7,
            dataScope: "existing",
            includeEnrichment: false);

        Assert.DoesNotContain("+", query);
        Assert.Contains("to=2026-10-05T00%3A00%3A00.0000000Z", query);
        var parsed = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query);
        Assert.Equal(withOffset.ToString("O", System.Globalization.CultureInfo.InvariantCulture), parsed["from"].ToString());
        Assert.Equal("7", parsed["storeId"].ToString());
        Assert.Equal("existing", parsed["dataScope"].ToString());
    }

    [Fact(DisplayName = "Pre-post pair previous-leg failure does not leak raw exception text")]
    public void PrePostPair_PreviousLegError_IsSafeSerbianCopy()
    {
        var source = File.ReadAllText(FindRepoFile("Api/Endpoints/AllEndpoints.cs"));
        Assert.DoesNotContain("previousError = ex.Message", source);
        Assert.Contains("previousError = $\"zahtev nije uspeo; referentni ID: {correlationId}\";", source);
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
