using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsCacheStatusEndpointAliasTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public AnalyticsCacheStatusEndpointAliasTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CacheStatus_CanonicalAlias_ReturnsSamePayloadAsLegacyRoute()
    {
        var client = _factory.CreateClient();

        var legacy = await GetJsonRootAsync(client, "/api/analytics/cached/cache/status");
        var canonical = await GetJsonRootAsync(client, "/api/analytics/cache/status");

        var legacyProperties = legacy.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var canonicalProperties = canonical.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(legacyProperties, canonicalProperties);

        foreach (var property in canonical.EnumerateObject())
        {
            Assert.True(legacy.TryGetProperty(property.Name, out var legacyValue));
            Assert.Equal(legacyValue.GetRawText(), property.Value.GetRawText());
        }

        Assert.True(canonical.TryGetProperty("cacheMode", out _));
        Assert.True(canonical.TryGetProperty("isDistributed", out _));
        Assert.True(canonical.TryGetProperty("reportCacheVersion", out _));
        Assert.True(canonical.TryGetProperty("lastAnalyticsCacheClearAtUtc", out _));
        Assert.True(canonical.TryGetProperty("lastReportCacheClearAtUtc", out _));
        Assert.True(canonical.TryGetProperty("warning", out _));
    }

    private static async Task<JsonElement> GetJsonRootAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload;
    }
}
