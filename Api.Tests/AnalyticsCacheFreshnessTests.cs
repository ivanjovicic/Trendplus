using System.Text.Json;
using Infrastructure.Services.Caching;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsCacheFreshnessTests
{
    [Fact]
    public void ColorSalesCacheMetadata_UsesSourceRefreshAndMarksOldDataStale()
    {
        var cacheCreatedAtUtc = DateTime.UtcNow.AddMinutes(-4);
        var dataRefreshAtUtc = DateTime.UtcNow.AddHours(-2);
        var json = "{\"meta\":{\"success\":true,\"dataQualityStatus\":\"good\"}}";
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = cacheCreatedAtUtc,
            DataRefreshAtUtc = dataRefreshAtUtc,
            Family = AnalyticsCachePolicy.ColorSalesFamily
        };

        var result = AllEndpoints.ApplyColorSalesCacheMetadata(
            json,
            metadata,
            AnalyticsCachePolicy.ColorSalesStats);

        using var document = JsonDocument.Parse(result);
        var meta = document.RootElement.GetProperty("meta");
        Assert.Equal(dataRefreshAtUtc, meta.GetProperty("lastRefreshAtUtc").GetDateTime());
        Assert.Equal(cacheCreatedAtUtc, meta.GetProperty("cacheCreatedAtUtc").GetDateTime());
        Assert.True(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal("STALE_CACHE", meta.GetProperty("warningCode").GetString());
        Assert.Equal("good", meta.GetProperty("dataQualityStatus").GetString());
    }

    [Fact]
    public void ColorSalesCacheMetadata_MissingMetadataFailsClosedAsStale()
    {
        var json = "{\"meta\":{\"success\":true}}";
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = DateTime.UnixEpoch,
            DataRefreshAtUtc = null,
            Family = AnalyticsCachePolicy.ColorSalesFamily
        };

        var result = AllEndpoints.ApplyColorSalesCacheMetadata(
            json,
            metadata,
            AnalyticsCachePolicy.ColorSalesStats);

        using var document = JsonDocument.Parse(result);
        var meta = document.RootElement.GetProperty("meta");
        Assert.True(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal("STALE_CACHE", meta.GetProperty("warningCode").GetString());
        Assert.Equal("warning", meta.GetProperty("dataQualityStatus").GetString());
    }

    [Fact]
    public void ColorSalesCacheMetadata_RejectsPayloadWithoutMetaObject()
    {
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = DateTime.UtcNow,
            Family = AnalyticsCachePolicy.ColorSalesFamily
        };

        Assert.Throws<InvalidOperationException>(() => AllEndpoints.ApplyColorSalesCacheMetadata(
            "{\"colors\":[]}",
            metadata,
            AnalyticsCachePolicy.ColorSalesStats));
    }

    [Fact]
    public void ApplyStaleCacheWarning_UsesDataRefreshInsteadOfCacheCreation()
    {
        var cacheCreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var dataRefreshAtUtc = DateTime.UtcNow.AddHours(-2);
        var meta = new AnalyticsResponseMetaDto { Success = true };
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = cacheCreatedAtUtc,
            DataRefreshAtUtc = dataRefreshAtUtc
        };

        CachedAnalyticsEndpoints.ApplyStaleCacheWarning(
            meta,
            metadata,
            AnalyticsCachePolicy.DashboardBootstrap);

        Assert.Equal(dataRefreshAtUtc, meta.LastRefreshAtUtc);
        Assert.Equal(cacheCreatedAtUtc, meta.CacheCreatedAtUtc);
        Assert.True(meta.IsPartial);
        Assert.Equal("STALE_CACHE", meta.WarningCode);
    }

    [Fact]
    public void ApplyStaleCacheWarning_LeavesMissingDataRefreshUnknown()
    {
        var cacheCreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var meta = new AnalyticsResponseMetaDto { Success = true };
        var metadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = cacheCreatedAtUtc,
            DataRefreshAtUtc = null
        };

        CachedAnalyticsEndpoints.ApplyStaleCacheWarning(
            meta,
            metadata,
            AnalyticsCachePolicy.DashboardBootstrap);

        Assert.Null(meta.LastRefreshAtUtc);
        Assert.Equal(cacheCreatedAtUtc, meta.CacheCreatedAtUtc);
    }

    [Fact]
    public void LegacyCacheMetadata_DoesNotInventDataRefreshTimestamp()
    {
        var legacyJson = $"{{\"createdAtUtc\":\"{DateTime.UtcNow.AddHours(-2):O}\",\"family\":\"dashboard\",\"provider\":\"memory\"}}";
        var metadata = JsonSerializer.Deserialize<AnalyticsCacheEntryMetadata>(legacyJson);

        Assert.NotNull(metadata);
        Assert.Null(metadata!.DataRefreshAtUtc);
    }
}
