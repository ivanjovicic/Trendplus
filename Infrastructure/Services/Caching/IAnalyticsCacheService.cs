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

    private static string NormalizeDataScope(string? dataScope)
    {
        var normalized = (dataScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "all" or "existing" or "imported" ? normalized : "all";
    }

    private static string FormatInstant(DateTime? value)
    {
        if (!value.HasValue)
            return "all";

        var normalized = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();

        return normalized.ToString("yyyyMMddHHmm");
    }

    private static string FormatTicks(DateTime? value) =>
        value.HasValue ? value.Value.Ticks.ToString() : string.Empty;

    private static string FormatNullable(int? value) =>
        value.HasValue ? value.Value.ToString() : string.Empty;

    private static string FormatNullable(long? value) =>
        value.HasValue ? value.Value.ToString() : string.Empty;

    private static string FilterSuffix(int? storeId, int? supplierId, string? dataScope = null) =>
        $"store:{(storeId.HasValue ? storeId.Value.ToString() : "all")}:supplier:{(supplierId.HasValue ? supplierId.Value.ToString() : "all")}:scope:{NormalizeDataScope(dataScope)}";
    
    // Sales Summary
    public static string SalesSummary(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}summary:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";
    
    // Daily Sales
    public static string DailySales(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}daily:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";
    
    // Top Products
    public static string TopProducts(int top, DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}top:{top}:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";
    
    // Category Data
    public static string CategoryData(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}category:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";
    
    // Gender Data
    public static string GenderData(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}gender:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";
    
    // Supplier Data
    public static string SupplierData(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}supplier:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";
    
    // Inventory
    public static string Inventory(int threshold) => 
        $"{Prefix}inventory:{threshold}";
    
    // Quick Insights
    public static string QuickInsights(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}insights:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";
    
    // Comparison
    public static string Comparison(DateTime? from, DateTime? to) => 
        $"{Prefix}comparison:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Health check
    public const string Health = $"{Prefix}health";

    // Transaction Stats
    public static string TransactionStats(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}transaction-stats:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";

    // By Payment
    public static string ByPayment(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}by-payment:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";

    // By Weekday
    public static string ByWeekday(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}by-weekday:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";

    // By Hour
    public static string ByHour(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}by-hour:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";

    // Category Trends
    public static string CategoryTrends(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) => 
        $"{Prefix}category-trends:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";

    // Reorder Suggestions
    public static string ReorderSuggestions(int? supplierId = null) =>
        $"{Prefix}reorder-suggestions:supplier:{(supplierId.HasValue ? supplierId.Value.ToString() : "all")}";

    // Dashboard Advanced Snapshot
    public static string DashboardAdvanced(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) =>
        $"{Prefix}dashboard-advanced:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";

    // Top Products (advanced tabs)
    public static string TopProductsAdvanced(int top, DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) =>
        $"{Prefix}top-advanced:{top}:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";

    public static string SupplierFilters(DateTime? from, DateTime? to, int? storeId = null, string? dataScope = null) =>
        $"{Prefix}filters:suppliers:{FormatInstant(from)}:{FormatInstant(to)}:store:{(storeId.HasValue ? storeId.Value.ToString() : "all")}:scope:{NormalizeDataScope(dataScope)}";

    public const string Stores = $"{Prefix}filters:stores";

    // Keeps the historical heavy-endpoint key dimensions: period, store, season, data scope, and active snapshot batch.
    public static string SupplierSalesStats(DateTime? from, DateTime? to, int? storeId = null, int? sezonaId = null, string? dataScope = null, long? activeSnapshotBatchId = null) =>
        $"{Prefix}supplier-sales-stats:{FormatTicks(from)}:{FormatTicks(to)}:{FormatNullable(storeId)}:{FormatNullable(sezonaId)}:{NormalizeDataScope(dataScope)}:snap:{FormatNullable(activeSnapshotBatchId)}";

    public static string ShoeTypeSalesStats(DateTime? from, DateTime? to, int? storeId = null, int? sezonaId = null, string? dataScope = null, long? activeSnapshotBatchId = null) =>
        $"{Prefix}shoe-type-sales-stats:{FormatTicks(from)}:{FormatTicks(to)}:{FormatNullable(storeId)}:{FormatNullable(sezonaId)}:{NormalizeDataScope(dataScope)}:snap:{FormatNullable(activeSnapshotBatchId)}";

    public static string SalesDataWindow(int? storeId = null, string? dataScope = null) =>
        $"{Prefix}data-window:store:{(storeId.HasValue ? storeId.Value.ToString() : "all")}:scope:{NormalizeDataScope(dataScope)}";

    public static string Metadata(string cacheKey) => $"{cacheKey}:metadata";

    public static string DashboardBootstrap(DateTime? from, DateTime? to, int? storeId = null, int? supplierId = null, string? dataScope = null) =>
        $"{Prefix}dashboard-bootstrap:{FormatInstant(from)}:{FormatInstant(to)}:{FilterSuffix(storeId, supplierId, dataScope)}";

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
        $"{Prefix}validation:negative-qty:{FormatInstant(from)}:{FormatInstant(to)}";
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

    /// <summary>Teški analytics ekrani - 20 minuta da se smanji broj skupih recompute-ova.</summary>
    public static readonly TimeSpan HeavyAnalytics = TimeSpan.FromMinutes(20);
    
    /// <summary>Veoma dugo - 1 sat (za historijske podatke)</summary>
    public static readonly TimeSpan VeryLong = TimeSpan.FromHours(1);
    
    /// <summary>Dnevni cache - za aggregirane dnevne podatke</summary>
    public static readonly TimeSpan Daily = TimeSpan.FromHours(24);
}
