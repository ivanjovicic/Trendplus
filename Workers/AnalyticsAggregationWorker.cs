using Application.Artikli.Common.Interfaces;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Workers;

/// <summary>
/// Background worker that periodically refreshes pre-aggregated analytics tables.
/// This dramatically improves analytics query performance by pre-computing aggregates.
/// </summary>
public class AnalyticsAggregationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsAggregationWorker> _logger;
    private readonly WorkerHealthService _healthService;
    
    private const string WorkerName = "AnalyticsAggregationWorker";
    
    // Configuration
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);  // Refresh every 5 minutes
    private readonly int _daysToRefresh = 7;  // Refresh last 7 days (for corrections)

    public AnalyticsAggregationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsAggregationWorker> logger,
        WorkerHealthService healthService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthService = healthService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📊 {WorkerName} starting...", WorkerName);
        _healthService.ReportRunning(WorkerName, "Starting up...");

        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _healthService.ReportRunning(WorkerName, "Refreshing analytics...");
                await RefreshAnalyticsAsync(stoppingToken);
                _healthService.ReportHealthy(WorkerName, $"Last refresh: {DateTime.UtcNow:HH:mm:ss}");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("📊 {WorkerName} cancellation requested", WorkerName);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in {WorkerName}", WorkerName);
                _healthService.ReportError(WorkerName, ex);
            }

            try
            {
                await Task.Delay(_refreshInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
        _logger.LogInformation("📊 {WorkerName} stopped", WorkerName);
    }

    private async Task RefreshAnalyticsAsync(CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ITrendplusDbContext>();
        var cache = scope.ServiceProvider.GetService<IAnalyticsCacheService>();

        try
        {
            // Get connection string from DbContext
            var connectionString = db.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("⚠️ No connection string available for analytics refresh");
                return;
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            var today = DateTime.UtcNow.Date;
            var startDate = today.AddDays(-_daysToRefresh);

            _logger.LogInformation("📊 Refreshing analytics for {StartDate} to {EndDate}...", 
                startDate.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"));

            // Refresh each day
            for (var date = startDate; date <= today; date = date.AddDays(1))
            {
                await RefreshDailySummaryAsync(connection, date, ct);
                await RefreshCategorySummaryAsync(connection, date, ct);
                await RefreshSupplierSummaryAsync(connection, date, ct);
                await RefreshGenderSummaryAsync(connection, date, ct);
            }

            // Refresh top products (only for today)
            await RefreshTopProductsAsync(connection, today, ct);

            // === CACHE INVALIDATION ===
            if (cache != null)
            {
                await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix, ct);
                _logger.LogInformation("Analytics cache invalidated after refresh.");
            }

            stopwatch.Stop();
            _logger.LogInformation("✅ Analytics refresh completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to refresh analytics aggregates");
        }
    }

    private async Task RefreshDailySummaryAsync(NpgsqlConnection connection, DateTime date, CancellationToken ct)
    {
        try
        {
            var sql = @"
                INSERT INTO ""AnalyticsDailySummary"" (""Date"", ""TotalRevenue"", ""TotalTransactions"", ""TotalUnits"", ""AvgBasketValue"", ""AvgItemPrice"", ""UpdatedAt"")
                SELECT 
                    @date::DATE,
                    COALESCE(SUM(ps.kolicina * ps.cena), 0),
                    COUNT(DISTINCT p.id),
                    COALESCE(SUM(ps.kolicina), 0),
                    CASE WHEN COUNT(DISTINCT p.id) > 0 
                        THEN COALESCE(SUM(ps.kolicina * ps.cena), 0) / COUNT(DISTINCT p.id)
                        ELSE 0 
                    END,
                    CASE WHEN SUM(ps.kolicina) > 0 
                        THEN COALESCE(SUM(ps.kolicina * ps.cena), 0) / SUM(ps.kolicina)
                        ELSE 0 
                    END,
                    NOW()
                FROM prodaja_zaglavlje p
                JOIN prodaja_stavke ps ON p.id = ps.id_prodaja
                WHERE DATE(p.datum_prodaje) = @date::DATE
                ON CONFLICT (""Date"") DO UPDATE SET
                    ""TotalRevenue"" = EXCLUDED.""TotalRevenue"",
                    ""TotalTransactions"" = EXCLUDED.""TotalTransactions"",
                    ""TotalUnits"" = EXCLUDED.""TotalUnits"",
                    ""AvgBasketValue"" = EXCLUDED.""AvgBasketValue"",
                    ""AvgItemPrice"" = EXCLUDED.""AvgItemPrice"",
                    ""UpdatedAt"" = NOW();";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("date", date);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") // Table doesn't exist
        {
            _logger.LogWarning("⚠️ AnalyticsDailySummary table doesn't exist. Run migration 007 first.");
        }
    }

    private async Task RefreshCategorySummaryAsync(NpgsqlConnection connection, DateTime date, CancellationToken ct)
    {
        try
        {
            // Delete existing records for this date
            var deleteSql = @"DELETE FROM ""AnalyticsCategorySummary"" WHERE ""Date"" = @date::DATE;";
            await using (var deleteCmd = new NpgsqlCommand(deleteSql, connection))
            {
                deleteCmd.Parameters.AddWithValue("date", date);
                await deleteCmd.ExecuteNonQueryAsync(ct);
            }

            // Insert new records
            var insertSql = @"
                INSERT INTO ""AnalyticsCategorySummary"" (""Date"", ""Kategorija"", ""TotalRevenue"", ""TotalUnits"", ""TransactionCount"", ""UpdatedAt"")
                SELECT 
                    @date::DATE,
                    COALESCE(a.""Kategorija"", 'Nepoznato'),
                    COALESCE(SUM(ps.kolicina * ps.cena), 0),
                    COALESCE(SUM(ps.kolicina), 0),
                    COUNT(DISTINCT p.id),
                    NOW()
                FROM prodaja_zaglavlje p
                JOIN prodaja_stavke ps ON p.id = ps.id_prodaja
                JOIN ""Artikli"" a ON ps.id_artikal = a.""Id""
                WHERE DATE(p.datum_prodaje) = @date::DATE
                GROUP BY a.""Kategorija"";";

            await using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("date", date);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogWarning("⚠️ AnalyticsCategorySummary table doesn't exist. Run migration 007 first.");
        }
    }

    private async Task RefreshSupplierSummaryAsync(NpgsqlConnection connection, DateTime date, CancellationToken ct)
    {
        try
        {
            // Delete existing records for this date
            var deleteSql = @"DELETE FROM ""AnalyticsSupplierSummary"" WHERE ""Date"" = @date::DATE;";
            await using (var deleteCmd = new NpgsqlCommand(deleteSql, connection))
            {
                deleteCmd.Parameters.AddWithValue("date", date);
                await deleteCmd.ExecuteNonQueryAsync(ct);
            }

            // Insert new records
            var insertSql = @"
                INSERT INTO ""AnalyticsSupplierSummary"" (""Date"", ""DobavljacId"", ""DobavljacNaziv"", ""TotalRevenue"", ""TotalUnits"", ""TransactionCount"", ""UpdatedAt"")
                SELECT 
                    @date::DATE,
                    d.""Id"",
                    COALESCE(d.""Naziv"", 'Nepoznato'),
                    COALESCE(SUM(ps.kolicina * ps.cena), 0),
                    COALESCE(SUM(ps.kolicina), 0),
                    COUNT(DISTINCT p.id),
                    NOW()
                FROM prodaja_zaglavlje p
                JOIN prodaja_stavke ps ON p.id = ps.id_prodaja
                JOIN ""Artikli"" a ON ps.id_artikal = a.""Id""
                LEFT JOIN ""Dobavljaci"" d ON a.""IDDobavljac"" = d.""Id""
                WHERE DATE(p.datum_prodaje) = @date::DATE
                GROUP BY d.""Id"", d.""Naziv"";";

            await using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("date", date);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogWarning("⚠️ AnalyticsSupplierSummary table doesn't exist. Run migration 007 first.");
        }
    }

    private async Task RefreshGenderSummaryAsync(NpgsqlConnection connection, DateTime date, CancellationToken ct)
    {
        try
        {
            // Delete existing records for this date
            var deleteSql = @"DELETE FROM ""AnalyticsGenderSummary"" WHERE ""Date"" = @date::DATE;";
            await using (var deleteCmd = new NpgsqlCommand(deleteSql, connection))
            {
                deleteCmd.Parameters.AddWithValue("date", date);
                await deleteCmd.ExecuteNonQueryAsync(ct);
            }

            // Insert new records
            var insertSql = @"
                INSERT INTO ""AnalyticsGenderSummary"" (""Date"", ""Pol"", ""TotalRevenue"", ""TotalUnits"", ""UpdatedAt"")
                SELECT 
                    @date::DATE,
                    COALESCE(a.""Pol"", 'Neodređeno'),
                    COALESCE(SUM(ps.kolicina * ps.cena), 0),
                    COALESCE(SUM(ps.kolicina), 0),
                    NOW()
                FROM prodaja_zaglavlje p
                JOIN prodaja_stavke ps ON p.id = ps.id_prodaja
                JOIN ""Artikli"" a ON ps.id_artikal = a.""Id""
                WHERE DATE(p.datum_prodaje) = @date::DATE
                GROUP BY a.""Pol"";";

            await using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("date", date);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogWarning("⚠️ AnalyticsGenderSummary table doesn't exist. Run migration 007 first.");
        }
    }

    private async Task RefreshTopProductsAsync(NpgsqlConnection connection, DateTime date, CancellationToken ct)
    {
        try
        {
            // Delete existing records for this date
            var deleteSql = @"DELETE FROM ""AnalyticsTopProducts"" WHERE ""Date"" = @date::DATE;";
            await using (var deleteCmd = new NpgsqlCommand(deleteSql, connection))
            {
                deleteCmd.Parameters.AddWithValue("date", date);
                await deleteCmd.ExecuteNonQueryAsync(ct);
            }

            // Insert top 50 products for today
            var insertSql = @"
                INSERT INTO ""AnalyticsTopProducts"" (""Date"", ""ProductId"", ""ProductName"", ""TotalRevenue"", ""TotalUnits"", ""Rank"", ""UpdatedAt"")
                SELECT 
                    @date::DATE,
                    a.""Id"",
                    a.""Naziv"",
                    COALESCE(SUM(ps.kolicina * ps.cena), 0) as total_revenue,
                    COALESCE(SUM(ps.kolicina), 0) as total_units,
                    ROW_NUMBER() OVER (ORDER BY SUM(ps.kolicina * ps.cena) DESC) as rank,
                    NOW()
                FROM prodaja_zaglavlje p
                JOIN prodaja_stavke ps ON p.id = ps.id_prodaja
                JOIN ""Artikli"" a ON ps.id_artikal = a.""Id""
                WHERE DATE(p.datum_prodaje) = @date::DATE
                GROUP BY a.""Id"", a.""Naziv""
                ORDER BY total_revenue DESC
                LIMIT 50;";

            await using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("date", date);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogWarning("⚠️ AnalyticsTopProducts table doesn't exist. Run migration 007 first.");
        }
    }
}
