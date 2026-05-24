using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

public sealed class AnalyticsCacheAdminService
{
    private readonly IAnalyticsCacheService _cache;
    private readonly ILogger<AnalyticsCacheAdminService> _logger;

    private DateTime? _lastClearAtUtc;
    private string? _lastClearFamily;

    public AnalyticsCacheAdminService(
        IAnalyticsCacheService cache,
        ILogger<AnalyticsCacheAdminService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public AnalyticsCacheClearState GetState()
        => new(_lastClearAtUtc, _lastClearFamily);

    public async Task<AnalyticsCacheClearState> ClearAsync(string? family, CancellationToken ct = default)
    {
        var normalizedFamily = string.IsNullOrWhiteSpace(family) ? "all" : family.Trim().ToLowerInvariant();
        var prefix = normalizedFamily == "all"
            ? AnalyticsCacheKeys.Prefix
            : AnalyticsCachePolicy.ResolveFamilyPrefix(normalizedFamily);

        await _cache.RemoveByPrefixAsync(prefix, ct);

        _lastClearAtUtc = DateTime.UtcNow;
        _lastClearFamily = normalizedFamily;

        _logger.LogInformation(
            "Analytics cache clear completed. Family={Family} Prefix={Prefix} AtUtc={AtUtc:O}",
            normalizedFamily,
            prefix,
            _lastClearAtUtc.Value);

        return new AnalyticsCacheClearState(_lastClearAtUtc, _lastClearFamily);
    }
}

public sealed record AnalyticsCacheClearState(DateTime? LastClearAtUtc, string? LastClearFamily);
