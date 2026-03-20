using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services.Caching;

/// <summary>
/// Hibridni cache servis za analytics podatke.
/// Koristi In-Memory kao primarni cache, Redis kao sekundarni (ako je dostupan).
/// BESPLATNO: In-Memory je uvek besplatan, Redis je opcioni.
/// </summary>
public interface IAnalyticsCacheService
{
    /// <summary>
    /// Dohvata vrednost iz cache-a. Prvo proverava In-Memory, pa Redis.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    
    /// <summary>
    /// Postavlja vrednost u cache. Uvek u In-Memory, opciono u Redis.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class;
    
    /// <summary>
    /// Briše vrednost iz oba cache-a.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);
    
    /// <summary>
    /// Briše sve ključeve koji počinju sa prefixom.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
    
    /// <summary>
    /// Get or Set pattern - dohvata iz cache ili izvršava factory funkciju.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class;
    
    /// <summary>
    /// Proverava da li Redis dostupan (konekcija).
    /// </summary>
    bool IsRedisAvailable { get; }

    /// <summary>
    /// Da li je Redis ukljucen (rucni toggle).
    /// </summary>
    bool IsRedisEnabled { get; }

    /// <summary>
    /// Rucno ukljuci/iskljuci Redis koriscenje.
    /// </summary>
    void SetRedisEnabled(bool enabled);
}

/// <summary>
/// Cache key konstante za analytics.
/// </summary>
public static class AnalyticsCacheKeys
{
    public const string Prefix = "analytics:";

    private static string FilterSuffix(int? storeId, int? supplierId) =>
        $"store:{(storeId.HasValue ? storeId.Value.ToString() : "all")}:supplier:{(supplierId.HasValue ? supplierId.Value.ToString() : "all")}";
    
    // Sales Summary
    public static string SalesSummary(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}summary:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";
    
    // Daily Sales
    public static string DailySales(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}daily:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";
    
    // Top Products
    public static string TopProducts(int top, DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}top:{top}:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";
    
    // Category Data
    public static string CategoryData(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}category:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";
    
    // Gender Data
    public static string GenderData(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}gender:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";
    
    // Supplier Data
    public static string SupplierData(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}supplier:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";
    
    // Inventory
    public static string Inventory(int threshold) => 
        $"{Prefix}inventory:{threshold}";
    
    // Quick Insights
    public static string QuickInsights(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}insights:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";
    
    // Comparison
    public static string Comparison(DateTime? from, DateTime? to) => 
        $"{Prefix}comparison:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Health check
    public const string Health = $"{Prefix}health";

    // Transaction Stats
    public static string TransactionStats(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}transaction-stats:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";

    // By Payment
    public static string ByPayment(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}by-payment:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";

    // By Weekday
    public static string ByWeekday(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}by-weekday:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";

    // By Hour
    public static string ByHour(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}by-hour:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";

    // Category Trends
    public static string CategoryTrends(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) => 
        $"{Prefix}category-trends:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";

    // Reorder Suggestions
    public static string ReorderSuggestions(int? supplierId = null) =>
        $"{Prefix}reorder-suggestions:supplier:{(supplierId.HasValue ? supplierId.Value.ToString() : "all")}";

    // Dashboard Advanced Snapshot
    public static string DashboardAdvanced(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) =>
        $"{Prefix}dashboard-advanced:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";

    // Top Products (advanced tabs)
    public static string TopProductsAdvanced(int top, DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) =>
        $"{Prefix}top-advanced:{top}:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";

    public static string SupplierFilters(DateTime? from, DateTime? to, int? storeId = null) =>
        $"{Prefix}filters:suppliers:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:store:{(storeId.HasValue ? storeId.Value.ToString() : "all")}";

    public const string Stores = $"{Prefix}filters:stores";

    public static string DashboardBootstrap(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null) =>
        $"{Prefix}dashboard-bootstrap:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}:{FilterSuffix(storeId, supplierId)}";

    public static string InventoryForecast(int? storeId = null, int? supplierId = null, int? skuId = null, string? sizeCode = null, int top = 200) =>
        $"{Prefix}inventory-forecast:{FilterSuffix(storeId, supplierId)}:sku:{(skuId.HasValue ? skuId.Value.ToString() : "all")}:size:{(string.IsNullOrWhiteSpace(sizeCode) ? "all" : sizeCode)}:top:{top}";

    public static string InventorySizeCurve(int? storeId = null, int? supplierId = null, int? skuId = null, int top = 200) =>
        $"{Prefix}inventory-size-curve:{FilterSuffix(storeId, supplierId)}:sku:{(skuId.HasValue ? skuId.Value.ToString() : "all")}:top:{top}";

    public static string RebalanceSuggestions(int? fromStoreId = null, int? toStoreId = null, int? supplierId = null, string? urgency = null, int top = 100) =>
        $"{Prefix}rebalance-suggestions:from:{(fromStoreId.HasValue ? fromStoreId.Value.ToString() : "all")}:to:{(toStoreId.HasValue ? toStoreId.Value.ToString() : "all")}:supplier:{(supplierId.HasValue ? supplierId.Value.ToString() : "all")}:urgency:{(string.IsNullOrWhiteSpace(urgency) ? "all" : urgency)}:top:{top}";

    public static string InventoryAlerts(int? storeId = null, int? supplierId = null, string? severity = null, int top = 100) =>
        $"{Prefix}inventory-alerts:{FilterSuffix(storeId, supplierId)}:severity:{(string.IsNullOrWhiteSpace(severity) ? "all" : severity)}:top:{top}";

    // Validation endpoints
    public const string ValidationCompleteness = $"{Prefix}validation:completeness";
    public const string ValidationFreshness = $"{Prefix}validation:freshness";
    public const string ValidationLostSales = $"{Prefix}validation:lost-sales";
    public static string ValidationNegativeQty(DateTime? from, DateTime? to) =>
        $"{Prefix}validation:negative-qty:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
}

/// <summary>
/// Cache expiration presets.
/// </summary>
public static class CacheExpiration
{
    /// <summary>Kratkoročni podaci - 1 minut</summary>
    public static readonly TimeSpan Short = TimeSpan.FromMinutes(1);
    
    /// <summary>Srednji rok - 5 minuta (default za većinu analytics)</summary>
    public static readonly TimeSpan Medium = TimeSpan.FromMinutes(5);
    
    /// <summary>Duži rok - 15 minuta (za podatke koji se retko menjaju)</summary>
    public static readonly TimeSpan Long = TimeSpan.FromMinutes(15);
    
    /// <summary>Veoma dugo - 1 sat (za historijske podatke)</summary>
    public static readonly TimeSpan VeryLong = TimeSpan.FromHours(1);
    
    /// <summary>Dnevni cache - za aggregirane dnevne podatke</summary>
    public static readonly TimeSpan Daily = TimeSpan.FromHours(24);
}
