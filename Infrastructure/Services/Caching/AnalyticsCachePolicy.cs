using System;
using System.Collections.Generic;
using System.Linq;

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
    public const string NivelacijaFamily = "nivelacija";
    public const string PreNivelacijaPrioritetiFamily = "pre-nivelacija-prioriteti";
    public const string ColorSalesFamily = "color-sales";
    public const string ReportsFamily = "reports";
    public const string SupplierSalesFamily = "supplier-sales";
    public const string ShoeTypeSalesFamily = "shoe-type-sales";
    public const string AllFamilies = "all";

    public static readonly string[] CoreFamilies =
    [
        DashboardFamily,
        ProductDecisionCenterFamily,
        SupplierDecisionHubFamily,
        InventoryFamily,
        DataQualityFamily,
        PrePostFamily,
        NivelacijaFamily,
        PreNivelacijaPrioritetiFamily,
        ColorSalesFamily,
        SupplierSalesFamily,
        ShoeTypeSalesFamily,
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
            PrePostFamily or NivelacijaFamily or "vendor-sales-nivelacija" or "pre-nivelacija" or PreNivelacijaPrioritetiFamily => PrePost,
            ColorSalesFamily or "color-sales-stats" => ColorSalesStats,
            ReportsFamily => CacheExpiration.Long.WithStaleAfter(TimeSpan.FromMinutes(10)),
            _ => CacheExpiration.Medium.WithStaleAfter(TimeSpan.FromMinutes(2))
        };
    }

    private const string K = AnalyticsCacheKeys.Prefix;

    /// <summary>
    /// Every real key prefix written by the screens of a family. Each entry must be the exact start of keys produced by
    /// <see cref="AnalyticsCacheKeys"/> (or a literal key in the endpoints); AnalyticsCacheFamilyPrefixContractTests pins this.
    /// Invalidating a family bumps the generation of every listed prefix, so all of its keys become unreachable at once.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> FamilyPrefixes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [DashboardFamily] =
        [
            AnalyticsCacheKeys.DashboardBootstrapPrefix,
            AnalyticsCacheKeys.DashboardAdvancedPrefix,
            AnalyticsCacheKeys.SalesSummaryPrefix,
            AnalyticsCacheKeys.DailySalesPrefix,
            AnalyticsCacheKeys.TopProductsPrefix,
            AnalyticsCacheKeys.TopProductsAdvancedPrefix,
            AnalyticsCacheKeys.CategoryDataPrefix,
            AnalyticsCacheKeys.GenderDataPrefix,
            AnalyticsCacheKeys.SupplierDataPrefix,
            K + "insights:",
            K + "comparison:",
            K + "transaction-stats:",
            K + "by-payment:",
            K + "by-weekday:",
            K + "by-hour:",
            K + "category-trends:",
            K + "filters:suppliers:",
            AnalyticsCacheKeys.Stores,
            K + "data-window:"
        ],
        [ProductDecisionCenterFamily] = [K + "product-decision-center:"],
        [SupplierDecisionHubFamily] = [K + "supplier-decision-hub:"],
        [InventoryFamily] =
        [
            K + "inventory:",
            K + "inventory-forecast:",
            K + "inventory-forecast-backtest:",
            K + "inventory-size-curve:",
            K + "inventory-alerts:",
            K + "inventory-insights:",
            K + "inventory-store-comparison:",
            K + "inventory-dataset:",
            K + "rebalance-suggestions:",
            K + "reorder-suggestions:"
        ],
        [DataQualityFamily] = [K + "data-quality:", K + "validation:"],
        // The Pre/Post nivelacija screen reads the vendor-sales-nivelacija route, so both families own its keys.
        [PrePostFamily] = [K + "pre-post:", K + "vendor-sales-nivelacija:"],
        [NivelacijaFamily] = [K + "vendor-sales-nivelacija:", K + "vendor-sales-nivelacija-options:"],
        [PreNivelacijaPrioritetiFamily] = [K + "pre-nivelacija-priority:"],
        [ColorSalesFamily] = [K + "color-sales-stats:"],
        [SupplierSalesFamily] = [K + "supplier-sales-stats:"],
        [ShoeTypeSalesFamily] = [K + "shoe-type-sales-stats:"],
        [ReportsFamily] = [AnalyticsCacheKeys.ReportNamespace]
    };

    private static readonly IReadOnlyDictionary<string, string> FamilyAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard-bootstrap"] = DashboardFamily,
        ["products"] = ProductDecisionCenterFamily,
        ["supplier-scorecard"] = SupplierDecisionHubFamily,
        ["vendor-sales-nivelacija"] = NivelacijaFamily,
        ["pre-nivelacija"] = PreNivelacijaPrioritetiFamily,
        ["color-sales-stats"] = ColorSalesFamily,
        ["supplier-sales-stats"] = SupplierSalesFamily,
        ["shoe-type-sales-stats"] = ShoeTypeSalesFamily
    };

    /// <summary>Returns the canonical family name, "all", or null for an unknown family.</summary>
    public static string? TryNormalizeFamily(string? family)
    {
        var normalized = (family ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized == AllFamilies)
            return AllFamilies;
        if (FamilyAliases.TryGetValue(normalized, out var canonical))
            return canonical;
        return FamilyPrefixes.ContainsKey(normalized) ? normalized : null;
    }

    public static bool IsKnownFamily(string? family) => TryNormalizeFamily(family) is not null;

    /// <summary>All real key prefixes of a family. Unknown families throw instead of widening to the whole namespace.</summary>
    public static IReadOnlyList<string> ResolveFamilyPrefixes(string family)
    {
        var normalized = TryNormalizeFamily(family)
            ?? throw new ArgumentException($"Unknown analytics cache family '{family}'.", nameof(family));
        return normalized == AllFamilies ? [AnalyticsCacheKeys.Prefix] : FamilyPrefixes[normalized];
    }

    /// <summary>Primary (first) prefix of a family; kept for existing callers. Use <see cref="ResolveFamilyPrefixes"/> to invalidate.</summary>
    public static string ResolveFamilyPrefix(string family) => ResolveFamilyPrefixes(family)[0];

    /// <summary>Distinct prefixes whose generations are tracked, including the namespace root and observability prefixes.</summary>
    public static IReadOnlyList<string> GenerationRoots { get; } = FamilyPrefixes.Values
        .SelectMany(static prefixes => prefixes)
        .Append(AnalyticsCacheKeys.Prefix)
        .Append(AnalyticsCacheKeys.ObservabilityLogsPrefix)
        .Append(AnalyticsCacheKeys.ObservabilityPerformancePrefix)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

public sealed record AnalyticsCachePolicyEntry(TimeSpan Ttl, TimeSpan StaleAfter);

public static class AnalyticsCachePolicyEntryExtensions
{
    public static AnalyticsCachePolicyEntry WithStaleAfter(this TimeSpan ttl, TimeSpan staleAfter)
        => new(ttl, staleAfter);
}
