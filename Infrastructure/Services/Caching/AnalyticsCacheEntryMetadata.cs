namespace Infrastructure.Services.Caching;

public sealed class AnalyticsCacheEntryMetadata
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Family { get; set; } = "general";
    public string Provider { get; set; } = "memory";
}
