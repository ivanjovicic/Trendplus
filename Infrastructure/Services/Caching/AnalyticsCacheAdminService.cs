using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

public sealed class AnalyticsCacheAdminService
{
    private const string SharedStateKey = "analytics:admin:clear-state";

    private readonly IAnalyticsCacheService _cache;
    private readonly ILogger<AnalyticsCacheAdminService> _logger;
    private readonly IDistributedCache? _distributedCache;

    private DateTime? _lastClearAtUtc;
    private string? _lastClearFamily;

    public AnalyticsCacheAdminService(
        IAnalyticsCacheService cache,
        IDistributedCache? distributedCache,
        ILogger<AnalyticsCacheAdminService> logger)
    {
        _cache = cache;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<AnalyticsCacheClearState> GetStateAsync(CancellationToken ct = default)
    {
        if (CanUseDistributedClearState())
        {
            try
            {
                var payload = await _distributedCache!.GetStringAsync(SharedStateKey, ct);
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    var parsed = JsonSerializer.Deserialize<AnalyticsCacheClearState>(payload);
                    if (parsed is not null)
                    {
                        _lastClearAtUtc = parsed.LastClearAtUtc;
                        _lastClearFamily = parsed.LastClearFamily;
                        return parsed with { IsShared = true, Storage = "redis", Warning = null };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read analytics cache clear state from distributed cache. Falling back to process-local state.");
            }
        }

        return new AnalyticsCacheClearState(
            _lastClearAtUtc,
            _lastClearFamily,
            IsShared: false,
            Storage: "memory",
            Warning: BuildNonSharedWarning());
    }

    public async Task<AnalyticsCacheClearState> ClearAsync(string? family, CancellationToken ct = default)
    {
        var normalizedFamily = string.IsNullOrWhiteSpace(family) ? "all" : family.Trim().ToLowerInvariant();
        var prefix = normalizedFamily == "all"
            ? AnalyticsCacheKeys.Prefix
            : AnalyticsCachePolicy.ResolveFamilyPrefix(normalizedFamily);

        await _cache.RemoveByPrefixAsync(prefix, ct);

        _lastClearAtUtc = DateTime.UtcNow;
        _lastClearFamily = normalizedFamily;

        var state = new AnalyticsCacheClearState(
            _lastClearAtUtc,
            _lastClearFamily,
            IsShared: CanUseDistributedClearState(),
            Storage: CanUseDistributedClearState() ? "redis" : "memory",
            Warning: CanUseDistributedClearState() ? null : BuildNonSharedWarning());

        if (CanUseDistributedClearState())
        {
            try
            {
                await _distributedCache!.SetStringAsync(
                    SharedStateKey,
                    JsonSerializer.Serialize(state),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist analytics cache clear state to distributed cache. State will remain process-local.");
                state = state with { IsShared = false, Storage = "memory", Warning = BuildNonSharedWarning() };
            }
        }

        _logger.LogInformation(
            "Analytics cache clear completed. Family={Family} Prefix={Prefix} AtUtc={AtUtc:O} Shared={IsShared} Storage={Storage}",
            normalizedFamily,
            prefix,
            _lastClearAtUtc.Value,
            state.IsShared,
            state.Storage);

        return state;
    }

    public async Task<AnalyticsCacheClearState> ClearFamiliesAsync(IEnumerable<string> families, CancellationToken ct = default)
    {
        var normalizedFamilies = families
            .Where(static family => !string.IsNullOrWhiteSpace(family))
            .Select(static family => family.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedFamilies.Length == 0)
        {
            return await ClearAsync("all", ct);
        }

        foreach (var family in normalizedFamilies)
        {
            await _cache.RemoveByPrefixAsync(AnalyticsCachePolicy.ResolveFamilyPrefix(family), ct);
        }

        _lastClearAtUtc = DateTime.UtcNow;
        _lastClearFamily = string.Join(",", normalizedFamilies);

        var canUseDistributedState = CanUseDistributedClearState();
        var state = new AnalyticsCacheClearState(
            _lastClearAtUtc,
            _lastClearFamily,
            IsShared: canUseDistributedState,
            Storage: canUseDistributedState ? "redis" : "memory",
            Warning: canUseDistributedState ? null : BuildNonSharedWarning());

        if (canUseDistributedState)
        {
            try
            {
                await _distributedCache!.SetStringAsync(
                    SharedStateKey,
                    JsonSerializer.Serialize(state),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist analytics cache clear state to distributed cache after multi-family invalidation. State will remain process-local.");
                state = state with { IsShared = false, Storage = "memory", Warning = BuildNonSharedWarning() };
            }
        }

        _logger.LogInformation(
            "Analytics cache clear completed for multiple families. Families={Families} AtUtc={AtUtc:O} Shared={IsShared} Storage={Storage}",
            normalizedFamilies,
            _lastClearAtUtc.Value,
            state.IsShared,
            state.Storage);

        return state;
    }

    public bool IsSharedCacheConfigured() => CanUseDistributedClearState();

    public string? GetTopologyWarning() => CanUseDistributedClearState() ? null : BuildNonSharedWarning();

    private bool CanUseDistributedClearState() =>
        _distributedCache is not null && _cache.IsRedisEnabled && _cache.IsRedisAvailable;

    private static string BuildNonSharedWarning() =>
        "Cache nije distribuiran; može biti nekonzistentan između instanci.";
}

public sealed record AnalyticsCacheClearState(
    DateTime? LastClearAtUtc,
    string? LastClearFamily,
    bool IsShared,
    string Storage,
    string? Warning);
