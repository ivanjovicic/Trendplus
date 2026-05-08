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
    private readonly WorkerRuntimeControlService _controlService;
    private readonly WorkerRuntimePolicyService _runtimePolicyService;
    
    private const string WorkerName = "AnalyticsAggregationWorker";
    private const int CommandTimeoutSeconds = 300;
    
    // Configuration
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);  // Refresh every 5 minutes
    private readonly int _daysToRefresh = 7;  // Refresh last 7 days (for corrections)

    public AnalyticsAggregationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsAggregationWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        WorkerRuntimePolicyService runtimePolicyService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _runtimePolicyService = runtimePolicyService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📊 {WorkerName} starting...", WorkerName);
        _healthService.ReportRunning(WorkerName, "Starting up...");

        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        var paused = false;
        var pauseCheckInterval = TimeSpan.FromSeconds(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled)
            {
                if (!paused)
                {
                    _logger.LogInformation("📊 {WorkerName} paused (global workers switch OFF).", WorkerName);
                    _healthService.ReportStopped(WorkerName, "Pauziran - workers switch je iskljucen.");
                    paused = true;
                }

                try
                {
                    await Task.Delay(pauseCheckInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            var policy = await _runtimePolicyService.GetPolicyAsync(WorkerName, stoppingToken);
            var manualRunRequested = false;
            if (!policy.CanRunNow)
            {
                if (!paused)
                {
                    _logger.LogInformation("{WorkerName} paused. Reason: {Reason}", WorkerName, policy.PauseReason ?? "Worker policy disabled execution.");
                    _healthService.ReportStopped(WorkerName, policy.PauseReason ?? "Pauziran - worker policy disabled execution.");
                    paused = true;
                }

                try
                {
                    await Task.Delay(pauseCheckInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            if (!policy.IsScheduleEnabled && policy.ManualRunRequested && !string.IsNullOrWhiteSpace(policy.ManualRunToken))
            {
                manualRunRequested = await _runtimePolicyService.TryConsumeManualRunRequestAsync(
                    WorkerName,
                    policy.ManualRunToken,
                    stoppingToken);

                if (!manualRunRequested)
                {
                    try
                    {
                        await Task.Delay(pauseCheckInterval, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    continue;
                }
            }

            if (paused)
            {
                _logger.LogInformation("📊 {WorkerName} resumed (global workers switch ON).", WorkerName);
                _healthService.ReportRunning(WorkerName, "Nastavljen rad nakon ukljucivanja workers switch-a.");
                paused = false;
            }

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
                var delay = manualRunRequested ? pauseCheckInterval : _refreshInterval;
                await Task.Delay(delay, stoppingToken);
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
            await LogDataQualitySnapshotAsync(connection, ct);

            // Intentionally avoid broad analytics cache purge every 5 minutes.
            // Full-prefix invalidation (`analytics:`) causes frequent cold starts
            // on heavy endpoints (for example dashboard bootstrap fan-in). We
            // rely on endpoint TTLs and targeted invalidation in data-import paths.
            if (cache != null)
            {
                _logger.LogDebug("Skipped broad analytics cache invalidation after periodic refresh.");
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
                INSERT INTO ""AnalyticsDailySummary"" (
                    ""Date"", ""TotalRevenue"", ""TotalTransactions"", ""TotalUnits"", ""AvgBasketValue"", ""AvgItemPrice"", ""BasketStdDev"", ""ItemPriceStdDev"", ""EffectiveTransactionCount"", ""DataConfidence"", ""UpdatedAt""
                )
                SELECT 
                    @date::DATE,
                    COALESCE(SUM(ps.kolicina * ps.cena), 0) AS TotalRevenue,
                    COUNT(DISTINCT p.id) AS TotalTransactions,
                    COALESCE(SUM(ps.kolicina), 0) AS TotalUnits,
                    CASE WHEN COUNT(DISTINCT p.id) > 0 
                        THEN COALESCE(SUM(ps.kolicina * ps.cena), 0) / COUNT(DISTINCT p.id)
                        ELSE 0 
                    END AS AvgBasketValue,
                    CASE WHEN SUM(ps.kolicina) > 0 
                        THEN COALESCE(SUM(ps.kolicina * ps.cena), 0) / SUM(ps.kolicina)
                        ELSE 0 
                    END AS AvgItemPrice,
                    COALESCE(STDDEV_POP(ps.kolicina * ps.cena), 0) AS BasketStdDev,
                    COALESCE(STDDEV_POP(ps.cena), 0) AS ItemPriceStdDev,
                    COUNT(DISTINCT p.id) * 0.95 AS EffectiveTransactionCount, -- Adjusted for confidence
                    CASE 
                        WHEN COUNT(DISTINCT p.id) > 0 AND SUM(ps.kolicina) > 0 THEN 1.0
                        ELSE 0.5 -- Lower confidence for partial data
                    END AS DataConfidence,
                    NOW()
                FROM prodaja_zaglavlje p
                JOIN prodaja_stavke ps ON p.id = ps.id_prodaja
                WHERE p.datum_prodaje >= @date_from
                  AND p.datum_prodaje < @date_to
                ON CONFLICT (""Date"") DO UPDATE SET
                    ""TotalRevenue"" = EXCLUDED.""TotalRevenue"",
                    ""TotalTransactions"" = EXCLUDED.""TotalTransactions"",
                    ""TotalUnits"" = EXCLUDED.""TotalUnits"",
                    ""AvgBasketValue"" = EXCLUDED.""AvgBasketValue"",
                    ""AvgItemPrice"" = EXCLUDED.""AvgItemPrice"",
                    ""BasketStdDev"" = EXCLUDED.""BasketStdDev"",
                    ""ItemPriceStdDev"" = EXCLUDED.""ItemPriceStdDev"",
                    ""EffectiveTransactionCount"" = EXCLUDED.""EffectiveTransactionCount"",
                    ""DataConfidence"" = EXCLUDED.""DataConfidence"",
                    ""UpdatedAt"" = NOW();";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("date", date.Date);
            cmd.Parameters.AddWithValue("date_from", date.Date);
            cmd.Parameters.AddWithValue("date_to", date.Date.AddDays(1));
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
                deleteCmd.CommandTimeout = CommandTimeoutSeconds;
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
                WHERE p.datum_prodaje >= @date_from
                  AND p.datum_prodaje < @date_to
                GROUP BY a.""Kategorija"";";

            await using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("date", date.Date);
            cmd.Parameters.AddWithValue("date_from", date.Date);
            cmd.Parameters.AddWithValue("date_to", date.Date.AddDays(1));
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
                deleteCmd.CommandTimeout = CommandTimeoutSeconds;
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
                WHERE p.datum_prodaje >= @date_from
                  AND p.datum_prodaje < @date_to
                GROUP BY d.""Id"", d.""Naziv"";";

            await using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("date", date.Date);
            cmd.Parameters.AddWithValue("date_from", date.Date);
            cmd.Parameters.AddWithValue("date_to", date.Date.AddDays(1));
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
                deleteCmd.CommandTimeout = CommandTimeoutSeconds;
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
                WHERE p.datum_prodaje >= @date_from
                  AND p.datum_prodaje < @date_to
                GROUP BY a.""Pol"";";

            await using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("date", date.Date);
            cmd.Parameters.AddWithValue("date_from", date.Date);
            cmd.Parameters.AddWithValue("date_to", date.Date.AddDays(1));
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
                deleteCmd.CommandTimeout = CommandTimeoutSeconds;
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
                WHERE p.datum_prodaje >= @date_from
                  AND p.datum_prodaje < @date_to
                GROUP BY a.""Id"", a.""Naziv""
                ORDER BY total_revenue DESC
                LIMIT 50;";

            await using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("date", date.Date);
            cmd.Parameters.AddWithValue("date_from", date.Date);
            cmd.Parameters.AddWithValue("date_to", date.Date.AddDays(1));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogWarning("⚠️ AnalyticsTopProducts table doesn't exist. Run migration 007 first.");
        }
    }

    private async Task LogDataQualitySnapshotAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        try
        {
            const string sql = """
                WITH sales_30d AS (
                    SELECT
                        ps.id_artikal AS artikal_id,
                        COALESCE(SUM(ps.kolicina * ps.cena), 0) AS sales_30d
                    FROM prodaja_stavke ps
                    JOIN prodaja_zaglavlje p ON p.id = ps.id_prodaja
                    WHERE p.datum_prodaje >= NOW() - INTERVAL '30 day'
                    GROUP BY ps.id_artikal
                ),
                quality_flags AS (
                    SELECT
                        a."Id" AS product_id,
                        CASE
                            WHEN a."IDDobavljac" IS NULL OR d."Id" IS NULL THEN 'MISSING_SUPPLIER'
                            WHEN a."IDTipObuce" IS NULL OR t."Id" IS NULL THEN 'MISSING_SHOE_TYPE'
                            WHEN NULLIF(BTRIM(a."Naziv"), '') IS NULL
                                 OR (a."IDDobavljac" IS NOT NULL AND NULLIF(BTRIM(d."Naziv"), '') IS NULL)
                                 OR (a."IDTipObuce" IS NOT NULL AND NULLIF(BTRIM(t."Naziv"), '') IS NULL)
                                THEN 'INVALID_NAME'
                            ELSE 'OK'
                        END AS data_quality_flag,
                        COALESCE(s.sales_30d, 0) AS sales_30d
                    FROM "Artikli" a
                    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
                    LEFT JOIN "TipoviObuce" t ON a."IDTipObuce" = t."Id"
                    LEFT JOIN sales_30d s ON s.artikal_id = a."Id"
                )
                SELECT
                    data_quality_flag,
                    COUNT(*) AS issue_count,
                    COALESCE(SUM(sales_30d), 0) AS affected_sales_30d
                FROM quality_flags
                GROUP BY data_quality_flag
                ORDER BY data_quality_flag;
                """;

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.CommandTimeout = CommandTimeoutSeconds;

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var salesImpact = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var totalProducts = 0;

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var flag = reader.GetString(0);
                var count = reader.GetInt32(1);
                var sales = reader.GetDecimal(2);

                counts[flag] = count;
                salesImpact[flag] = sales;
                totalProducts += count;
            }

            var missingSupplier = counts.GetValueOrDefault("MISSING_SUPPLIER");
            var missingShoeType = counts.GetValueOrDefault("MISSING_SHOE_TYPE");
            var invalidName = counts.GetValueOrDefault("INVALID_NAME");
            var problematic = missingSupplier + missingShoeType + invalidName;
            var issueRatio = totalProducts > 0 ? (decimal)problematic / totalProducts : 0m;

            _logger.LogInformation(
                "Data quality snapshot: total={TotalProducts}, missingSupplier={MissingSupplier}, missingShoeType={MissingShoeType}, invalidName={InvalidName}, affectedSales30d={AffectedSales30d}",
                totalProducts,
                missingSupplier,
                missingShoeType,
                invalidName,
                salesImpact.GetValueOrDefault("MISSING_SUPPLIER")
                + salesImpact.GetValueOrDefault("MISSING_SHOE_TYPE")
                + salesImpact.GetValueOrDefault("INVALID_NAME"));

            if (issueRatio > 0.05m)
            {
                _logger.LogWarning(
                    "Data quality warning: {ProblematicProducts} problematic products out of {TotalProducts} ({IssueRatio:P1})",
                    problematic,
                    totalProducts,
                    issueRatio);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogWarning("⚠️ Data quality snapshot skipped because one of the source tables does not exist yet.");
        }
    }
}
