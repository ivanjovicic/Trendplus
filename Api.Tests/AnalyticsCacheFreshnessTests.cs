using System.Text.Json;
using Infrastructure.Services.Caching;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsCacheFreshnessTests
{
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
