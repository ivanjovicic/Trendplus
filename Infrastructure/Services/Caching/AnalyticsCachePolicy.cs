namespace Infrastructure.Services.Caching;

/// <summary>
/// Standardized cache policy for analytics endpoints used in pilot/sales scenarios.
/// </summary>
public static class AnalyticsCachePolicy
{
    public const string DashboardFamily = "dashboard";
    public const string ProductDecisionCenterFamily = "product-decision-center";
    public const string SupplierDecisionHubFamily = "supplier-decision-hub";
    public const string InventoryFamily = "inventory";
    public const string DataQualityFamily = "data-quality";
    public const string PrePostFamily = "pre-post";
    public const string PreNivelacijaPrioritetiFamily = "pre-nivelacija-prioriteti";
    public const string ColorSalesFamily = "color-sales";
    public const string ReportsFamily = "reports";

    public static readonly string[] CoreFamilies =
    [
        DashboardFamily,
        ProductDecisionCenterFamily,
        SupplierDecisionHubFamily,
        InventoryFamily,
        DataQualityFamily,
        PrePostFamily,
        PreNivelacijaPrioritetiFamily,
        ColorSalesFamily,
        ReportsFamily
    ];

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

    public static readonly AnalyticsCachePolicyEntry ColorSalesStats = new(
        Ttl: TimeSpan.FromMinutes(5),
        StaleAfter: TimeSpan.FromMinutes(3));

    public static AnalyticsCachePolicyEntry ResolveByFamily(string family)
    {
        var normalized = (family ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            DashboardFamily or "dashboard-bootstrap" => DashboardBootstrap,
            ProductDecisionCenterFamily or "products" => ProductDecisionCenter,
            "supplier-scorecard" or SupplierDecisionHubFamily => SupplierScorecard,
            InventoryFamily => Inventory,
            DataQualityFamily => DataQuality,
            PrePostFamily or "pre-nivelacija" or PreNivelacijaPrioritetiFamily => PrePost,
            ColorSalesFamily or "color-sales-stats" => ColorSalesStats,
            ReportsFamily => CacheExpiration.Long.WithStaleAfter(TimeSpan.FromMinutes(10)),
            _ => CacheExpiration.Medium.WithStaleAfter(TimeSpan.FromMinutes(2))
        };
    }

    public static string ResolveFamilyPrefix(string family)
    {
        var normalized = (family ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            DashboardFamily or "dashboard-bootstrap" => "analytics:dashboard",
            ProductDecisionCenterFamily or "products" => "analytics:product-decision-center",
            "supplier-scorecard" or SupplierDecisionHubFamily => "analytics:supplier-decision-hub",
            InventoryFamily => "analytics:inventory",
            DataQualityFamily => "analytics:data-quality",
            PrePostFamily => "analytics:pre-post",
            "pre-nivelacija" or PreNivelacijaPrioritetiFamily => "analytics:pre-nivelacija-prioriteti",
            ColorSalesFamily or "color-sales-stats" => "analytics:color-sales",
            ReportsFamily => AnalyticsCacheKeys.ReportNamespace,
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
