using System.Globalization;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Workers;

public sealed class ObservedInventoryDailyCaptureWorker : BackgroundService
{
    public const string WorkerName = "ObservedInventoryDailyCaptureWorker";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ObservedInventoryDailyCaptureWorker> _logger;
    private readonly WorkerHealthService _health;
    private readonly WorkerRuntimeControlService _control;
    private readonly WorkerRuntimePolicyService _policy;
    private readonly ObservedInventoryDailyCaptureOptions _options;
    private DateOnly? _failedAttemptDate;

    public ObservedInventoryDailyCaptureWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ObservedInventoryDailyCaptureWorker> logger,
        WorkerHealthService health,
        WorkerRuntimeControlService control,
        WorkerRuntimePolicyService policy,
        IOptions<ObservedInventoryDailyCaptureOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _health = health;
        _control = control;
        _policy = policy;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
        var runAt = TimeSpan.ParseExact(_options.RunAtLocalTime, "hh\\:mm", CultureInfo.InvariantCulture);
        _health.ReportRunning(WorkerName, _options.Enabled ? "Opt-in scheduler initialized." : "Disabled by configuration.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_options.Enabled || !_control.IsEnabled)
                {
                    _health.ReportStopped(WorkerName, "Disabled by configuration or global worker switch.");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var policy = await _policy.GetPolicyAsync(WorkerName, stoppingToken);
                if (!policy.CanRunNow)
                {
                    _health.ReportStopped(WorkerName, policy.PauseReason ?? "Worker runtime policy is disabled.");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var observedAtUtc = DateTimeOffset.UtcNow;
                var businessDate = GetDueBusinessDate(observedAtUtc, zone, runAt, _failedAttemptDate);
                if (businessDate.HasValue)
                {
                    if (!await HasCurrentSuccessfulRunAsync(businessDate.Value, observedAtUtc, stoppingToken))
                    {
                        _failedAttemptDate = businessDate;
                        var (runId, rowCount, runState) = await CaptureAsync(businessDate.Value, observedAtUtc, stoppingToken);
                        _failedAttemptDate = null;
                        _health.ReportHealthy(WorkerName, $"Capture {runState}: {rowCount} observed rows; run {runId}.");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Observed inventory daily capture iteration failed.");
                _health.ReportError(WorkerName, ex);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _health.ReportStopped(WorkerName, "Graceful shutdown");
    }

    public static DateOnly? GetDueBusinessDate(
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone,
        TimeSpan runAtLocalTime,
        DateOnly? lastAttemptDate)
    {
        var local = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var businessDate = DateOnly.FromDateTime(local.DateTime);
        return local.TimeOfDay >= runAtLocalTime && lastAttemptDate != businessDate
            ? businessDate
            : null;
    }

    private async Task<bool> HasCurrentSuccessfulRunAsync(
        DateOnly businessDate,
        DateTimeOffset observedAtUtc,
        CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1
                  FROM analytics_intel.inventory_observed_daily_capture_run r
                 WHERE r.business_date = @business_date
                   AND r.run_state IN ('succeeded', 'empty')
                   AND NOT EXISTS (
                       SELECT 1
                         FROM "ProductsDim" pd
                        WHERE pd."Timestamp" > COALESCE(r.source_row_updated_through_utc, '-infinity'::timestamptz)
                          AND pd."Timestamp" <= @observed_at_utc
                   )
            );
            """, connection);
        command.Parameters.AddWithValue("business_date", businessDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("observed_at_utc", observedAtUtc);
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private async Task<(Guid RunId, int RowCount, string State)> CaptureAsync(
        DateOnly businessDate,
        DateTimeOffset observedAtUtc,
        CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Analytics database connection is not configured.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using (var start = new NpgsqlCommand("""
            INSERT INTO analytics_intel.inventory_observed_daily_capture_run
                (run_id, business_date, business_time_zone, observed_at_utc, source_system, run_state)
            VALUES (@run_id, @business_date, @time_zone_id, @observed_at_utc, 'products_dim_current', 'running');
            """, connection))
        {
            start.Parameters.AddWithValue("run_id", runId);
            start.Parameters.AddWithValue("business_date", businessDate.ToDateTime(TimeOnly.MinValue));
            start.Parameters.AddWithValue("time_zone_id", _options.TimeZoneId);
            start.Parameters.AddWithValue("observed_at_utc", observedAtUtc);
            await start.ExecuteNonQueryAsync(ct);
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var capture = new NpgsqlCommand(
                "select analytics_intel.capture_observed_inventory_daily(@run_id);",
                connection,
                transaction);
            capture.Parameters.AddWithValue("run_id", runId);
            var rowCount = Convert.ToInt32(await capture.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            await transaction.CommitAsync(ct);

            await using var state = new NpgsqlCommand(
                "select run_state from analytics_intel.inventory_observed_daily_capture_run where run_id = @run_id;",
                connection);
            state.Parameters.AddWithValue("run_id", runId);
            return (runId, rowCount, (string)(await state.ExecuteScalarAsync(ct) ?? "unknown"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var errorCode = ex is PostgresException pg ? pg.SqlState : "capture_failed";
            try
            {
                await using var fail = new NpgsqlCommand("""
                    UPDATE analytics_intel.inventory_observed_daily_capture_run
                       SET run_state = 'failed', error_code = @error_code, completed_at_utc = clock_timestamp()
                     WHERE run_id = @run_id AND run_state = 'running';
                    """, connection);
                fail.Parameters.AddWithValue("run_id", runId);
                fail.Parameters.AddWithValue("error_code", errorCode);
                await fail.ExecuteNonQueryAsync(ct);
            }
            catch (Exception recordException)
            {
                _logger.LogError(recordException, "Could not persist failed inventory capture state for {RunId}.", runId);
            }

            throw;
        }
    }
}
