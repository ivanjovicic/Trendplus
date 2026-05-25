using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

public sealed class AnalyticsCacheAdminService
{
    private const string SharedStateKey = "analytics:admin:clear-state";
    private const string SharedReportVersionKey = "analytics:admin:report-cache-version";

    private readonly IAnalyticsCacheService _cache;
    private readonly ILogger<AnalyticsCacheAdminService> _logger;
    private readonly IDistributedCache? _distributedCache;

    private DateTime? _lastClearAtUtc;
    private string? _lastClearFamily;
    private DateTime? _lastAnalyticsCacheClearAtUtc;
    private DateTime? _lastReportCacheClearAtUtc;
    private int _reportCacheVersion = 1;

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
                        _lastAnalyticsCacheClearAtUtc = parsed.LastAnalyticsCacheClearAtUtc;
                        _lastReportCacheClearAtUtc = parsed.LastReportCacheClearAtUtc;
                        _reportCacheVersion = Math.Max(1, parsed.ReportCacheVersion);
                        return parsed with { IsShared = true, Storage = "redis", Warning = null };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read analytics cache clear state from distributed cache. Falling back to process-local state.");
            }
        }

        return BuildCurrentState();
    }

    public async Task<int> GetReportCacheVersionAsync(CancellationToken ct = default)
    {
        if (CanUseDistributedClearState())
        {
            try
            {
                var raw = await _distributedCache!.GetStringAsync(SharedReportVersionKey, ct);
                if (int.TryParse(raw, out var parsed) && parsed > 0)
                {
                    _reportCacheVersion = parsed;
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read report cache version token from distributed cache.");
            }
        }

        return Math.Max(1, _reportCacheVersion);
    }

    public async Task<int> BumpReportCacheVersionAsync(CancellationToken ct = default)
    {
        var nextVersion = (await GetReportCacheVersionAsync(ct)) + 1;
        _reportCacheVersion = nextVersion;

        if (CanUseDistributedClearState())
        {
            try
            {
                await _distributedCache!.SetStringAsync(SharedReportVersionKey, nextVersion.ToString(), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist report cache version token to distributed cache.");
            }
        }

        return nextVersion;
    }

    public (string CacheMode, bool IsDistributed) ResolveCacheMode()
    {
        if (_cache is DisabledAnalyticsCacheService)
        {
            return ("disabled", false);
        }

        if (_cache.IsRedisEnabled && _cache.IsRedisAvailable)
        {
            return ("redis", true);
        }

        if (_cache is InMemoryCacheService || _cache is HybridCacheService)
        {
            return ("in-memory", false);
        }

        return ("unknown", false);
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
        if (normalizedFamily == "all" || !string.Equals(normalizedFamily, AnalyticsCachePolicy.ReportsFamily, StringComparison.OrdinalIgnoreCase))
        {
            _lastAnalyticsCacheClearAtUtc = _lastClearAtUtc;
        }

        if (normalizedFamily == "all" || string.Equals(normalizedFamily, AnalyticsCachePolicy.ReportsFamily, StringComparison.OrdinalIgnoreCase))
        {
            _lastReportCacheClearAtUtc = _lastClearAtUtc;
            _reportCacheVersion = await BumpReportCacheVersionAsync(ct);
        }

        var state = BuildCurrentState();

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
            "Analytics cache clear completed. Family={Family} Prefix={Prefix} AtUtc={AtUtc:O} Shared={IsShared} Storage={Storage} ReportVersion={ReportVersion}",
            normalizedFamily,
            prefix,
            _lastClearAtUtc.Value,
            state.IsShared,
            state.Storage,
            state.ReportCacheVersion);

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

        var includesAll = normalizedFamilies.Any(f => string.Equals(f, "all", StringComparison.OrdinalIgnoreCase));
        var includesReports = includesAll || normalizedFamilies.Any(f => string.Equals(f, AnalyticsCachePolicy.ReportsFamily, StringComparison.OrdinalIgnoreCase));
        var includesAnalytics = includesAll || normalizedFamilies.Any(f => !string.Equals(f, AnalyticsCachePolicy.ReportsFamily, StringComparison.OrdinalIgnoreCase));

        if (includesAnalytics)
        {
            _lastAnalyticsCacheClearAtUtc = _lastClearAtUtc;
        }

        if (includesReports)
        {
            _lastReportCacheClearAtUtc = _lastClearAtUtc;
            _reportCacheVersion = await BumpReportCacheVersionAsync(ct);
        }

        var canUseDistributedState = CanUseDistributedClearState();
        var state = BuildCurrentState(canUseDistributedState);

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
            "Analytics cache clear completed for multiple families. Families={Families} AtUtc={AtUtc:O} Shared={IsShared} Storage={Storage} ReportVersion={ReportVersion}",
            normalizedFamilies,
            _lastClearAtUtc.Value,
            state.IsShared,
            state.Storage,
            state.ReportCacheVersion);

        return state;
    }

    public bool IsSharedCacheConfigured() => CanUseDistributedClearState();

    public string? GetTopologyWarning() => CanUseDistributedClearState() ? null : BuildNonSharedWarning();

    private bool CanUseDistributedClearState() =>
        _distributedCache is not null && _cache.IsRedisEnabled && _cache.IsRedisAvailable;

    private AnalyticsCacheClearState BuildCurrentState(bool? forceShared = null)
    {
        var shared = forceShared ?? CanUseDistributedClearState();
        var storage = shared ? "redis" : "memory";
        return new AnalyticsCacheClearState(
            LastClearAtUtc: _lastClearAtUtc,
            LastClearFamily: _lastClearFamily,
            IsShared: shared,
            Storage: storage,
            Warning: shared ? null : BuildNonSharedWarning(),
            LastAnalyticsCacheClearAtUtc: _lastAnalyticsCacheClearAtUtc,
            LastReportCacheClearAtUtc: _lastReportCacheClearAtUtc,
            ReportCacheVersion: Math.Max(1, _reportCacheVersion));
    }

    private static string BuildNonSharedWarning() =>
        "Cache nije distribuiran; može biti nekonzistentan između instanci.";
}

public sealed record AnalyticsCacheClearState(
    DateTime? LastClearAtUtc,
    string? LastClearFamily,
    bool IsShared,
    string Storage,
    string? Warning,
    DateTime? LastAnalyticsCacheClearAtUtc,
    DateTime? LastReportCacheClearAtUtc,
    int ReportCacheVersion);
