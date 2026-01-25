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
    /// Proverava da li Redis dostupan.
    /// </summary>
    bool IsRedisAvailable { get; }
}

/// <summary>
/// Cache key konstante za analytics.
/// </summary>
public static class AnalyticsCacheKeys
{
    public const string Prefix = "analytics:";
    
    // Sales Summary
    public static string SalesSummary(DateTime? from, DateTime? to) => 
        $"{Prefix}summary:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Daily Sales
    public static string DailySales(DateTime? from, DateTime? to) => 
        $"{Prefix}daily:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Top Products
    public static string TopProducts(int top, DateTime? from, DateTime? to) => 
        $"{Prefix}top:{top}:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Category Data
    public static string CategoryData(DateTime? from, DateTime? to) => 
        $"{Prefix}category:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Gender Data
    public static string GenderData(DateTime? from, DateTime? to) => 
        $"{Prefix}gender:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Supplier Data
    public static string SupplierData(DateTime? from, DateTime? to) => 
        $"{Prefix}supplier:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Inventory
    public static string Inventory(int threshold) => 
        $"{Prefix}inventory:{threshold}";
    
    // Quick Insights
    public static string QuickInsights(DateTime? from, DateTime? to) => 
        $"{Prefix}insights:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Comparison
    public static string Comparison(DateTime? from, DateTime? to) => 
        $"{Prefix}comparison:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";
    
    // Health check
    public const string Health = $"{Prefix}health";

    // Transaction Stats
    public static string TransactionStats(DateTime? from, DateTime? to) => 
        $"{Prefix}transaction-stats:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";

    // By Payment
    public static string ByPayment(DateTime? from, DateTime? to) => 
        $"{Prefix}by-payment:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";

    // By Weekday
    public static string ByWeekday(DateTime? from, DateTime? to) => 
        $"{Prefix}by-weekday:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";

    // By Hour
    public static string ByHour(DateTime? from, DateTime? to) => 
        $"{Prefix}by-hour:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";

    // Category Trends
    public static string CategoryTrends(DateTime? from, DateTime? to) => 
        $"{Prefix}category-trends:{from?.ToString("yyyyMMdd") ?? "all"}:{to?.ToString("yyyyMMdd") ?? "all"}";

    // Reorder Suggestions
    public static string ReorderSuggestions => $"{Prefix}reorder-suggestions";
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
