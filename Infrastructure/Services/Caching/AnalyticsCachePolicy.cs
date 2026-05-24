namespace Infrastructure.Services.Caching;

/// <summary>
/// Standardized cache policy for analytics endpoints used in pilot/sales scenarios.
/// </summary>
public static class AnalyticsCachePolicy
{
    public static readonly AnalyticsCachePolicyEntry DashboardBootstrap = new(
        Ttl: TimeSpan.FromMinutes(2),
        StaleAfter: TimeSpan.FromMinutes(1));

    public static readonly AnalyticsCachePolicyEntry ProductDecisionCenter = new(
        Ttl: TimeSpan.FromMinutes(4),
        StaleAfter: TimeSpan.FromMinutes(2));

    public static readonly AnalyticsCachePolicyEntry SupplierScorecard = new(
        Ttl: TimeSpan.FromMinutes(5),
        StaleAfter: TimeSpan.FromMinutes(3));

    public static readonly AnalyticsCachePolicyEntry Inventory = new(
        Ttl: TimeSpan.FromMinutes(2),
        StaleAfter: TimeSpan.FromMinutes(1));

    public static readonly AnalyticsCachePolicyEntry DataQuality = new(
        Ttl: TimeSpan.FromMinutes(3),
        StaleAfter: TimeSpan.FromMinutes(2));

    public static readonly AnalyticsCachePolicyEntry PrePost = new(
        Ttl: TimeSpan.FromMinutes(5),
        StaleAfter: TimeSpan.FromMinutes(3));

    public static AnalyticsCachePolicyEntry ResolveByFamily(string family)
    {
        var normalized = (family ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "dashboard" or "dashboard-bootstrap" => DashboardBootstrap,
            "product-decision-center" or "products" => ProductDecisionCenter,
            "supplier-scorecard" or "supplier-decision-hub" => SupplierScorecard,
            "inventory" => Inventory,
            "data-quality" => DataQuality,
            "pre-post" or "pre-nivelacija" => PrePost,
            _ => CacheExpiration.Medium.WithStaleAfter(TimeSpan.FromMinutes(2))
        };
    }

    public static string ResolveFamilyPrefix(string family)
    {
        var normalized = (family ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "dashboard" or "dashboard-bootstrap" => "analytics:dashboard",
            "product-decision-center" or "products" => "analytics:product-decision-center",
            "supplier-scorecard" or "supplier-decision-hub" => "analytics:supplier-decision-hub",
            "inventory" => "analytics:inventory",
            "data-quality" => "analytics:validation",
            "pre-post" or "pre-nivelacija" => "analytics:pre-nivelacija-priority",
            _ => AnalyticsCacheKeys.Prefix
        };
    }
}

public sealed record AnalyticsCachePolicyEntry(TimeSpan Ttl, TimeSpan StaleAfter);

public static class AnalyticsCachePolicyEntryExtensions
{
    public static AnalyticsCachePolicyEntry WithStaleAfter(this TimeSpan ttl, TimeSpan staleAfter)
        => new(ttl, staleAfter);
}
