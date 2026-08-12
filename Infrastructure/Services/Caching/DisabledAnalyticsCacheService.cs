namespace Infrastructure.Services.Caching;

/// <summary>
/// No-op cache provider for explicit cache disable mode.
/// </summary>
public sealed class DisabledAnalyticsCacheService : IAnalyticsCacheService
{
    public bool IsRedisAvailable => false;
    public bool IsRedisEnabled => false;

    public CacheFootprintSnapshot GetFootprintSnapshot()
        => new("disabled", false, false, 0);

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        => await factory();

    public void SetRedisEnabled(bool enabled)
    {
    }
}
