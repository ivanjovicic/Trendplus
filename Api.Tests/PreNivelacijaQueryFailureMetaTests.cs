using Infrastructure.Services.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaQueryFailureMetaTests
{
    [Fact]
    public async Task QueryFailure_IsNotCachedAndNextRequestCanSucceed()
    {
        var cache = CreateCache();
        var factoryCalls = 0;

        await Assert.ThrowsAsync<PreNivelacijaQueryFailedException>(() => cache.GetOrSetAsync(
            "pre-nivelacija-query-failure",
            () =>
            {
                factoryCalls++;
                return Task.FromException<CacheProbe>(new PreNivelacijaQueryFailedException(
                    salesQueryFailed: true,
                    markdownQueryFailed: false,
                    new InvalidOperationException("database unavailable")));
            }));

        var recovered = await cache.GetOrSetAsync(
            "pre-nivelacija-query-failure",
            () =>
            {
                factoryCalls++;
                return Task.FromResult(new CacheProbe("recovered"));
            });

        Assert.Equal(2, factoryCalls);
        Assert.Equal("recovered", recovered.Value);
    }

    [Fact]
    public async Task RequestCancellation_IsNotCachedAndNextRequestCanSucceed()
    {
        var cache = CreateCache();
        using var cancellation = new CancellationTokenSource();
        var factoryCalls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetOrSetAsync(
            "pre-nivelacija-cancelled-query",
            () =>
            {
                factoryCalls++;
                cancellation.Cancel();
                return Task.FromCanceled<CacheProbe>(cancellation.Token);
            },
            ct: cancellation.Token));

        var recovered = await cache.GetOrSetAsync(
            "pre-nivelacija-cancelled-query",
            () =>
            {
                factoryCalls++;
                return Task.FromResult(new CacheProbe("recovered"));
            });

        Assert.Equal(2, factoryCalls);
        Assert.Equal("recovered", recovered.Value);
    }

    [Fact]
    public void QueryFailureException_PreservesFailureSourceForUncachedMapping()
    {
        var source = new InvalidOperationException("database unavailable");
        var exception = new PreNivelacijaQueryFailedException(
            salesQueryFailed: true,
            markdownQueryFailed: false,
            source);

        Assert.True(exception.SalesQueryFailed);
        Assert.False(exception.MarkdownQueryFailed);
        Assert.Same(source, exception.InnerException);
    }

    [Fact]
    public void BuildQueryFailureMeta_SalesFailure_IsErrorNotSuccessZero()
    {
        var meta = PreNivelacijaPriorityEndpoints.BuildQueryFailureMeta(salesQueryFailed: true, markdownQueryFailed: false);

        Assert.False(meta.Success);
        Assert.Equal("pre_nivelacija_sales_unavailable", meta.ErrorCode);
        Assert.Contains("nije dostupna", meta.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildQueryFailureMeta_MarkdownFailure_IsErrorNotSuccessZero()
    {
        var meta = PreNivelacijaPriorityEndpoints.BuildQueryFailureMeta(salesQueryFailed: false, markdownQueryFailed: true);

        Assert.False(meta.Success);
        Assert.Equal("pre_nivelacija_markdown_unavailable", meta.ErrorCode);
    }

    private static HybridCacheService CreateCache() => new(
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<HybridCacheService>.Instance);

    private sealed record CacheProbe(string Value);
}
