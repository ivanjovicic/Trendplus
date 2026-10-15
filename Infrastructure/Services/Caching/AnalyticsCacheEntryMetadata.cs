namespace Infrastructure.Services.Caching;

public sealed class AnalyticsCacheEntryMetadata
{
    // No implicit "now": a missing creation time must stay detectable (default) instead of looking fresh.
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DataRefreshAtUtc { get; set; }
    public string Family { get; set; } = "general";
    public string Provider { get; set; } = "memory";
}
