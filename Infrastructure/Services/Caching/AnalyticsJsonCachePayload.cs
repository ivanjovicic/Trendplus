namespace Infrastructure.Services.Caching;

/// <summary>
/// Generic JSON payload stored for analytics cache entries (Supplier Sales and similar routes).
/// </summary>
public sealed class AnalyticsJsonCachePayload
{
    public string Json { get; init; } = string.Empty;
}
