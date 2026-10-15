using System.Threading;
using System.Text.Json;
using Application.Analytics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

public sealed class AnalyticsCacheAdminService
{
    private const string SharedStateKey = "analytics:admin:clear-state";
    private const string SharedReportVersionKey = "analytics:admin:report-cache-version";

    private readonly IAnalyticsCacheService _cache;
    private readonly ILogger<AnalyticsCacheAdminService> _logger;
    private readonly IDistributedCache? _distributedCache;
    private readonly OperationsAnalyticsIntegrityRegistry? _integrityRegistry;
    private readonly IServiceScopeFactory? _scopeFactory;

    private DateTime? _lastClearAtUtc;
    private string? _lastClearFamily;
    private DateTime? _lastAnalyticsCacheClearAtUtc;
    private DateTime? _lastReportCacheClearAtUtc;
    private int _reportCacheVersion = 1;
    private readonly SemaphoreSlim _reportVersionLock = new(1, 1);

    public AnalyticsCacheAdminService(
        IAnalyticsCacheService cache,
        IDistributedCache? distributedCache,
        ILogger<AnalyticsCacheAdminService> logger,
        OperationsAnalyticsIntegrityRegistry? integrityRegistry = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        _cache = cache;
        _distributedCache = distributedCache;
        _logger = logger;
        _integrityRegistry = integrityRegistry;
        _scopeFactory = scopeFactory;
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
        // Serialized inside the process so concurrent clears never read the same version and both write N+1.
        // Across instances two bumps can still collapse into one value; that is harmless because every bump runs
        // after its data commit and ClearAsync also bumps the report generation root with a unique token.
        await _reportVersionLock.WaitAsync(ct);
        try
        {
            return await BumpReportCacheVersionCoreAsync(ct);
        }
        finally
        {
            _reportVersionLock.Release();
        }
    }

    private async Task<int> BumpReportCacheVersionCoreAsync(CancellationToken ct)
    {
        var nextVersion = Math.Max(await GetReportCacheVersionAsync(ct), _reportCacheVersion) + 1;
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

    public async Task<AnalyticsCacheClearState> ClearAsync(
        string? family,
        CancellationToken ct = default,
        bool scheduleIntegrityProbe = true)
    {
        // Unknown families are rejected; only an explicit "all" (or no family) clears the whole analytics namespace.
        var normalizedFamily = AnalyticsCachePolicy.TryNormalizeFamily(family)
            ?? throw new ArgumentException($"Unknown analytics cache family '{family}'.", nameof(family));
        var prefixes = AnalyticsCachePolicy.ResolveFamilyPrefixes(normalizedFamily);
        foreach (var familyPrefix in prefixes)
        {
            await _cache.RemoveByPrefixAsync(familyPrefix, ct);
        }
        var prefix = string.Join(",", prefixes);

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

        _integrityRegistry?.MarkUnverified(
            "cache_clear",
            $"Analytics cache family '{normalizedFamily}' cleared; bounded Operations integrity probe will reconcile live facts.",
            OperationsAnalyticsIntegrityFamilies.ResolveAffected(normalizedFamily));

        if (scheduleIntegrityProbe)
        {
            ScheduleBoundedIntegrityProbe(normalizedFamily);
        }

        return state;
    }

    private void ScheduleBoundedIntegrityProbe(string? family = null)
    {
        if (_scopeFactory is null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var integrityService = scope.ServiceProvider.GetRequiredService<IOperationsAnalyticsIntegrityService>();
                await integrityService.MarkUnverifiedAsync(
                    "cache_clear",
                    "Analytics cache was cleared; durable integrity evidence remains unverified until the bounded probe completes.",
                    family);
                await integrityService.RunBoundedProbeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Background Operations integrity probe after cache clear did not complete.");
            }
        });
    }

    public async Task<AnalyticsCacheClearState> ClearFamiliesAsync(
        IEnumerable<string> families,
        CancellationToken ct = default,
        bool scheduleIntegrityProbe = true)
    {
        var requestedFamilies = families
            .Where(static family => !string.IsNullOrWhiteSpace(family))
            .ToArray();
        if (requestedFamilies.Length == 0)
        {
            throw new ArgumentException("At least one analytics cache family is required; pass \"all\" explicitly to clear everything.", nameof(families));
        }

        var normalizedFamilies = requestedFamilies
            .Select(static family => AnalyticsCachePolicy.TryNormalizeFamily(family)
                ?? throw new ArgumentException($"Unknown analytics cache family '{family}'.", nameof(families)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Bump every real prefix once even when families share prefixes (pre-post and nivelacija).
        foreach (var familyPrefix in normalizedFamilies.SelectMany(AnalyticsCachePolicy.ResolveFamilyPrefixes).Distinct(StringComparer.Ordinal))
        {
            await _cache.RemoveByPrefixAsync(familyPrefix, ct);
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

        if (includesAnalytics)
        {
            _integrityRegistry?.MarkUnverified(
                "cache_clear",
                $"Analytics cache families '{_lastClearFamily}' cleared; bounded Operations integrity probe will reconcile live facts.",
                OperationsAnalyticsIntegrityFamilies.ResolveAffected(string.Join(',', normalizedFamilies)));
            if (scheduleIntegrityProbe)
            {
                ScheduleBoundedIntegrityProbe(string.Join(',', normalizedFamilies));
            }
        }

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
