using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Workers;

public sealed class NightlyAnalyticsRefreshWorker : BackgroundService
{
    private const string WorkerName = "NightlyAnalyticsRefreshWorker";
    private const string RefreshJobKey = "nightly_analytics_refresh";
    private const string RefreshJobName = "Nightly analytics refresh";

    // Advisory lock keys (prevents multiple instances from running the refresh in parallel).
    private const int LockKey1 = 20260227;
    private const int LockKey2 = 17017;

    private static readonly Regex SafeIdent = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NightlyAnalyticsRefreshWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly AnalyticsRefreshRunRecorder _refreshRunRecorder;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly WorkerRuntimePolicyService _runtimePolicyService;
    private readonly NightlyAnalyticsRefreshOptions _options;

    private DateOnly? _lastAttemptDateUtc;
    private DateTime? _lastSuccessUtc;

    public NightlyAnalyticsRefreshWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NightlyAnalyticsRefreshWorker> logger,
        WorkerHealthService healthService,
        AnalyticsRefreshRunRecorder refreshRunRecorder,
        WorkerRuntimeControlService controlService,
        WorkerRuntimePolicyService runtimePolicyService,
        IOptions<NightlyAnalyticsRefreshOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthService = healthService;
        _refreshRunRecorder = refreshRunRecorder;
        _controlService = controlService;
        _runtimePolicyService = runtimePolicyService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[nightly] {WorkerName} starting...", WorkerName);
        _healthService.ReportRunning(WorkerName, "Starting up...");

        var paused = false;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _healthService.ReportStopped(WorkerName, "Cancelled during startup delay");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled || !_options.Enabled)
            {
                if (!paused)
                {
                    var reason = !_controlService.IsEnabled
                        ? "Pauziran - workers switch je isključen."
                        : "Pauziran - NightlyAnalyticsRefresh je disabled u konfiguraciji.";
                    _logger.LogInformation("{WorkerName} paused. Reason: {Reason}", WorkerName, reason);
                    _healthService.ReportStopped(WorkerName, reason);
                    paused = true;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PauseCheckSeconds)), stoppingToken);
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
                    var reason = policy.PauseReason ?? "Pauziran - worker policy disabled execution.";
                    _logger.LogInformation("{WorkerName} paused. Reason: {Reason}", WorkerName, reason);
                    _healthService.ReportStopped(WorkerName, reason);
                    paused = true;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PauseCheckSeconds)), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            if (policy.ManualRunRequested && !string.IsNullOrWhiteSpace(policy.ManualRunToken))
            {
                manualRunRequested = await _runtimePolicyService.TryConsumeManualRunRequestAsync(
                    WorkerName,
                    policy.ManualRunToken,
                    stoppingToken);

                if (!manualRunRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PauseCheckSeconds)), stoppingToken);
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
                _logger.LogInformation("{WorkerName} resumed (workers switch ON).", WorkerName);
                _healthService.ReportRunning(WorkerName, "Nastavljen rad nakon uključivanja workers switch-a.");
                paused = false;
            }

            var runAtUtc = ParseRunAtUtc(_options.RunAtUtc);
            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);
            var scheduledTodayUtc = nowUtc.Date.Add(runAtUtc);

            var windowEndUtc = _options.CatchUpIfMissed
                ? scheduledTodayUtc.AddHours(Math.Max(0, _options.CatchUpMaxHours))
                : scheduledTodayUtc.AddMinutes(Math.Max(1, _options.GracePeriodMinutes));

            var shouldAttemptToday =
                _lastAttemptDateUtc != todayUtc
                && nowUtc >= scheduledTodayUtc
                && nowUtc <= windowEndUtc;

            if (manualRunRequested || shouldAttemptToday)
            {
                _lastAttemptDateUtc = todayUtc;
                await RunNightlyRefreshAsync(
                    manualRunRequested ? "manual" : "nightly",
                    stoppingToken);
            }

            var nextRunUtc = nowUtc < scheduledTodayUtc
                ? scheduledTodayUtc
                : scheduledTodayUtc.AddDays(1);

            var lastSuccessLabel = _lastSuccessUtc.HasValue
                ? _lastSuccessUtc.Value.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                : "n/a";

            _healthService.ReportHealthy(
                WorkerName,
                $"Idle. Next run (UTC): {nextRunUtc:yyyy-MM-dd HH:mm:ss}Z | Last success: {lastSuccessLabel}");

            var heartbeat = manualRunRequested
                ? TimeSpan.FromSeconds(Math.Max(1, _options.PauseCheckSeconds))
                : TimeSpan.FromSeconds(Math.Max(30, _options.HeartbeatSeconds));
            var remaining = nextRunUtc - DateTime.UtcNow;
            var delay = remaining > TimeSpan.Zero && remaining < heartbeat ? remaining : heartbeat;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
        _logger.LogInformation("[nightly] {WorkerName} stopped", WorkerName);
    }

    private async Task RunNightlyRefreshAsync(string triggeredBy, CancellationToken ct)
    {
        var startedAtUtc = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        var refreshedObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failedObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var errors = new List<string>();
        var correlationId = Activity.Current?.Id;
        var runId = await _refreshRunRecorder.StartRunAsync(
            jobKey: RefreshJobKey,
            jobName: RefreshJobName,
            triggeredBy: triggeredBy,
            processMode: "worker",
            workerName: WorkerName,
            correlationId: correlationId,
            ct);

        _healthService.ReportRunning(WorkerName, "Nightly refresh started...");
        _logger.LogInformation("[nightly] Nightly analytics refresh started at {StartedAtUtc:O}", startedAtUtc);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var trendplusDb = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            var connectionString = trendplusDb.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                var msg = "Missing Trendplus DB connection string.";
                _logger.LogError("[nightly] {Message}", msg);
                _healthService.ReportError(WorkerName, new InvalidOperationException(msg));
                await _refreshRunRecorder.MarkFailedAsync(
                    runId,
                    "missing_connection_string",
                    msg,
                    failedObjects,
                    correlationId,
                    ct);
                return;
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            var acquired = await TryAcquireLockAsync(connection, ct);
            if (!acquired)
            {
                var skipMessage = "Skipped (another instance is running nightly refresh).";
                _logger.LogInformation("[nightly] Skipping refresh because another instance holds advisory lock.");
                _healthService.ReportHealthy(WorkerName, skipMessage);
                await _refreshRunRecorder.MarkPartialAsync(
                    runId,
                    refreshedObjects,
                    failedObjects,
                    skipMessage,
                    correlationId,
                    ct);
                return;
            }

            try
            {
                await RefreshMaterializedViewsAsync(
                    connection,
                    _options.MaterializedViewsToRefresh,
                    warnings,
                    errors,
                    refreshedObjects,
                    failedObjects,
                    sourceLabel: "trendplus",
                    ct: ct);

                await RefreshAnalyticsMaterializedViewsAsync(
                    scope.ServiceProvider,
                    trendplusConnectionString: connectionString,
                    trendplusConnection: connection,
                    warnings: warnings,
                    errors: errors,
                    refreshedObjects: refreshedObjects,
                    failedObjects: failedObjects,
                    ct: ct);

                await RefreshOpenTrainingMaterializedViewsAsync(
                    scope.ServiceProvider,
                    trendplusConnectionString: connectionString,
                    trendplusConnection: connection,
                    warnings: warnings,
                    errors: errors,
                    refreshedObjects: refreshedObjects,
                    failedObjects: failedObjects,
                    ct: ct);

                if (_options.VacuumAnalyzeTargets.Count == 0)
                {
                    warnings.Add("No vacuum targets configured.");
                }

                foreach (var target in _options.VacuumAnalyzeTargets)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!TryParseRelation(target, out var schema, out var rel, out var quoted))
                    {
                        warnings.Add($"Invalid VACUUM identifier: {target}");
                        continue;
                    }

                    var vacuumable = await IsVacuumableRelationAsync(connection, schema, rel, ct);
                    if (!vacuumable)
                    {
                        warnings.Add($"Skipping VACUUM (not found or not vacuumable): {schema}.{rel}");
                        continue;
                    }

                    _healthService.ReportRunning(WorkerName, $"VACUUM ANALYZE {schema}.{rel}...");
                    var vacuumSw = Stopwatch.StartNew();
                    try
                    {
                        var vacuumSql = $"VACUUM ANALYZE {quoted};";
                        await ExecuteNonQueryAsync(connection, vacuumSql, _options.CommandTimeoutSeconds, ct);
                        vacuumSw.Stop();
                        _logger.LogInformation(
                            "[nightly] Vacuumed {Relation} in {DurationMs}ms",
                            $"{schema}.{rel}",
                            vacuumSw.Elapsed.TotalMilliseconds);
                    }
                    catch (PostgresException ex)
                    {
                        vacuumSw.Stop();
                        var relationName = $"{schema}.{rel}";
                        failedObjects.Add(relationName);
                        errors.Add($"VACUUM failed for {relationName}: {ex.MessageText}");
                        _logger.LogError(ex, "[nightly] VACUUM failed for {Relation}", relationName);
                    }
                }
            }
            finally
            {
                await ReleaseLockAsync(connection, ct);
            }

            sw.Stop();

            if (errors.Count > 0)
            {
                var message = $"Nightly refresh finished with {errors.Count} errors; {warnings.Count} warnings. Duration: {sw.Elapsed.TotalSeconds:0}s";
                _logger.LogError("[nightly] {Message}", message);
                _healthService.ReportError(WorkerName, new InvalidOperationException(message));
                await _refreshRunRecorder.MarkFailedAsync(
                    runId,
                    "refresh_failed",
                    BuildStatusSummary(message, warnings, errors),
                    failedObjects,
                    correlationId,
                    ct);
                return;
            }

            _lastSuccessUtc = DateTime.UtcNow;

            if (_options.QueueSupplierRankingTraining)
            {
                try
                {
                    await QueueSupplierRankingTrainingAsync(scope.ServiceProvider, warnings, ct);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Supplier ranking training queue failed: {ex.Message}");
                    _logger.LogWarning(ex, "[nightly] Failed to queue supplier ranking training run after nightly refresh.");
                }
            }

            var okMessage = $"Nightly refresh OK. Duration: {sw.Elapsed.TotalSeconds:0}s"
                            + (warnings.Count > 0 ? $" | Warnings: {string.Join("; ", warnings.Distinct())}" : string.Empty);

            try
            {
                var cacheAdmin = scope.ServiceProvider.GetService<AnalyticsCacheAdminService>();
                if (cacheAdmin is not null)
                {
                    var cacheState = await cacheAdmin.ClearFamiliesAsync(AnalyticsCachePolicy.CoreFamilies, ct);
                    _logger.LogInformation(
                        "[nightly] Analytics cache invalidation completed. ReportCacheVersion={ReportCacheVersion} LastAnalyticsClearAtUtc={LastAnalyticsCacheClearAtUtc:O} LastReportClearAtUtc={LastReportCacheClearAtUtc:O}",
                        cacheState.ReportCacheVersion,
                        cacheState.LastAnalyticsCacheClearAtUtc,
                        cacheState.LastReportCacheClearAtUtc);
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Analytics cache invalidation failed: {ex.Message}");
                _logger.LogWarning(ex, "[nightly] Analytics cache invalidation failed after successful refresh.");
            }

            _logger.LogInformation("[nightly] {Message}", okMessage);
            _healthService.ReportHealthy(WorkerName, okMessage);

            if (warnings.Count > 0)
            {
                await _refreshRunRecorder.MarkPartialAsync(
                    runId,
                    refreshedObjects,
                    failedObjects,
                    BuildStatusSummary(okMessage, warnings, errors),
                    correlationId,
                    ct);
            }
            else
            {
                await _refreshRunRecorder.MarkSucceededAsync(
                    runId,
                    refreshedObjects,
                    correlationId,
                    ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            await _refreshRunRecorder.MarkFailedAsync(
                runId,
                "cancelled",
                "Nightly refresh was cancelled.",
                failedObjects,
                correlationId,
                CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var message = $"Nightly refresh failed unexpectedly. Duration: {sw.Elapsed.TotalSeconds:0}s";
            _logger.LogError(ex, "[nightly] {Message}", message);
            _healthService.ReportError(WorkerName, ex);
            await _refreshRunRecorder.MarkFailedAsync(
                runId,
                "unexpected_error",
                BuildStatusSummary($"{message} {ex.Message}", warnings, errors),
                failedObjects,
                correlationId,
                ct);
        }
    }

    private async Task RefreshOpenTrainingMaterializedViewsAsync(
        IServiceProvider serviceProvider,
        string trendplusConnectionString,
        NpgsqlConnection trendplusConnection,
        List<string> warnings,
        List<string> errors,
        ISet<string> refreshedObjects,
        ISet<string> failedObjects,
        CancellationToken ct)
    {
        var targets = _options.OpenTrainingMaterializedViewsToRefresh
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
            return;

        var openTrainingDb = serviceProvider.GetRequiredService<OpenProductTrainingDbContext>();
        var openTrainingConnectionString = openTrainingDb.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(openTrainingConnectionString))
        {
            warnings.Add("OpenTraining connection string missing; skipping OpenTraining MV refresh.");
            return;
        }

        if (string.Equals(
            trendplusConnectionString.Trim(),
            openTrainingConnectionString.Trim(),
            StringComparison.OrdinalIgnoreCase))
        {
            var trendplusSet = new HashSet<string>(_options.MaterializedViewsToRefresh, StringComparer.OrdinalIgnoreCase);
            var sameConnectionTargets = targets.Where(x => !trendplusSet.Contains(x)).ToList();
            if (sameConnectionTargets.Count == 0)
                return;

            await RefreshMaterializedViewsAsync(
                trendplusConnection,
                sameConnectionTargets,
                warnings,
                errors,
                refreshedObjects,
                failedObjects,
                sourceLabel: "open_training",
                ct: ct);
            return;
        }

        await using var openConnection = new NpgsqlConnection(openTrainingConnectionString);
        await openConnection.OpenAsync(ct);

        var acquired = await TryAcquireLockAsync(openConnection, ct);
        if (!acquired)
        {
            warnings.Add("Skipped OpenTraining MV refresh because another instance holds advisory lock.");
            return;
        }

        try
        {
            await RefreshMaterializedViewsAsync(
                openConnection,
                targets,
                warnings,
                errors,
                refreshedObjects,
                failedObjects,
                sourceLabel: "open_training",
                ct: ct);
        }
        finally
        {
            await ReleaseLockAsync(openConnection, ct);
        }
    }

    private async Task RefreshAnalyticsMaterializedViewsAsync(
        IServiceProvider serviceProvider,
        string trendplusConnectionString,
        NpgsqlConnection trendplusConnection,
        List<string> warnings,
        List<string> errors,
        ISet<string> refreshedObjects,
        ISet<string> failedObjects,
        CancellationToken ct)
    {
        var analyticsDb = serviceProvider.GetRequiredService<AnalyticsDbContext>();
        var analyticsConnectionString = analyticsDb.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(analyticsConnectionString))
        {
            warnings.Add("Analytics connection string missing; skipping analytics MV refresh.");
            return;
        }

        if (string.Equals(
            trendplusConnectionString.Trim(),
            analyticsConnectionString.Trim(),
            StringComparison.OrdinalIgnoreCase))
        {
            await RefreshMaterializedViewsAsync(
                trendplusConnection,
                _options.MaterializedViewsToRefresh,
                warnings,
                errors,
                refreshedObjects,
                failedObjects,
                sourceLabel: "analytics",
                ct: ct);

            await RefreshMaterializedViewsAsync(
                trendplusConnection,
                _options.IntelligenceMaterializedViewsToRefresh,
                warnings,
                errors,
                refreshedObjects,
                failedObjects,
                sourceLabel: "analytics_intel",
                ct: ct);
            return;
        }

        await using var analyticsConnection = new NpgsqlConnection(analyticsConnectionString);
        await analyticsConnection.OpenAsync(ct);

        var acquired = await TryAcquireLockAsync(analyticsConnection, ct);
        if (!acquired)
        {
            warnings.Add("Skipped analytics MV refresh because another instance holds advisory lock.");
            return;
        }

        try
        {
            await RefreshMaterializedViewsAsync(
                analyticsConnection,
                _options.MaterializedViewsToRefresh,
                warnings,
                errors,
                refreshedObjects,
                failedObjects,
                sourceLabel: "analytics",
                ct: ct);

            await RefreshMaterializedViewsAsync(
                analyticsConnection,
                _options.IntelligenceMaterializedViewsToRefresh,
                warnings,
                errors,
                refreshedObjects,
                failedObjects,
                sourceLabel: "analytics_intel",
                ct: ct);
        }
        finally
        {
            await ReleaseLockAsync(analyticsConnection, ct);
        }
    }

    private async Task RefreshMaterializedViewsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<string> materializedViews,
        List<string> warnings,
        List<string> errors,
        ISet<string> refreshedObjects,
        ISet<string> failedObjects,
        string sourceLabel,
        CancellationToken ct)
    {
        if (materializedViews.Count == 0)
        {
            warnings.Add($"No materialized views configured for refresh ({sourceLabel}).");
            return;
        }

        foreach (var mv in materializedViews)
        {
            ct.ThrowIfCancellationRequested();
            var relationName = NormalizeRelationName(mv);

            if (!TryParseRelation(mv, out var schema, out var rel, out var quoted))
            {
                warnings.Add($"Invalid MV identifier ({sourceLabel}): {mv}");
                failedObjects.Add(relationName);
                continue;
            }

            var exists = await IsMaterializedViewAsync(connection, schema, rel, ct);
            if (!exists)
            {
                warnings.Add($"Missing MV ({sourceLabel}): {schema}.{rel}");
                failedObjects.Add(relationName);
                continue;
            }

            _healthService.ReportRunning(WorkerName, $"Refreshing {sourceLabel}:{schema}.{rel}...");
            var refreshSw = Stopwatch.StartNew();
            try
            {
                var canRefreshConcurrently = _options.RefreshConcurrently
                    && await CanRefreshMaterializedViewConcurrentlyAsync(connection, schema, rel, ct);
                var refreshSql = canRefreshConcurrently
                    ? $"REFRESH MATERIALIZED VIEW CONCURRENTLY {quoted};"
                    : $"REFRESH MATERIALIZED VIEW {quoted};";
                var refreshMode = canRefreshConcurrently ? "CONCURRENTLY" : "blocking";

                if (_options.RefreshConcurrently && !canRefreshConcurrently)
                {
                    warnings.Add($"Concurrent refresh unavailable ({sourceLabel}) for {schema}.{rel}; falling back to blocking refresh in worker process.");
                }

                await ExecuteNonQueryAsync(connection, refreshSql, _options.CommandTimeoutSeconds, ct);
                refreshSw.Stop();
                refreshedObjects.Add(relationName);
                _logger.LogInformation(
                    "[nightly] Refreshed {Scope}/{Relation} using {RefreshMode} in {DurationMs}ms",
                    sourceLabel,
                    $"{schema}.{rel}",
                    refreshMode,
                    refreshSw.Elapsed.TotalMilliseconds);
            }
            catch (PostgresException ex) when (ex.SqlState == "0A000")
            {
                refreshSw.Stop();
                failedObjects.Add(relationName);
                errors.Add($"Refresh failed ({sourceLabel}) for {schema}.{rel}: {ex.MessageText}");
                _logger.LogError(ex, "[nightly] Refresh failed ({Scope}) for {Relation}", sourceLabel, $"{schema}.{rel}");
            }
            catch (PostgresException ex)
            {
                refreshSw.Stop();
                failedObjects.Add(relationName);
                errors.Add($"Refresh failed ({sourceLabel}) for {schema}.{rel}: {ex.MessageText}");
                _logger.LogError(ex, "[nightly] Refresh failed ({Scope}) for {Relation}", sourceLabel, $"{schema}.{rel}");
            }
        }
    }

    private static async Task QueueSupplierRankingTrainingAsync(
        IServiceProvider serviceProvider,
        List<string> warnings,
        CancellationToken ct)
    {
        var openTrainingDb = serviceProvider.GetRequiredService<OpenProductTrainingDbContext>();
        var connectionString = openTrainingDb.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            warnings.Add("OpenTraining connection string missing; supplier ranking training not queued.");
            return;
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string existsSql = """
            SELECT COUNT(*)
            FROM training_run
            WHERE model_type = @modelType
              AND created_at >= date_trunc('day', NOW() AT TIME ZONE 'UTC')
              AND status IN ('queued', 'running', 'succeeded');
            """;

        await using (var exists = new NpgsqlCommand(existsSql, conn))
        {
            exists.Parameters.AddWithValue("modelType", OpenTrainingModelCatalog.SupplierRankingModelType);
            var count = Convert.ToInt32(await exists.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            if (count > 0)
                return;
        }

        const string insertSql = """
            INSERT INTO training_run (
                model_type,
                feature_view_name,
                status,
                params_json,
                notes
            )
            VALUES (
                @modelType,
                @featureViewName,
                'queued',
                @params::jsonb,
                @notes
            );
            """;

        await using var insert = new NpgsqlCommand(insertSql, conn);
        insert.Parameters.AddWithValue("modelType", OpenTrainingModelCatalog.SupplierRankingModelType);
        insert.Parameters.AddWithValue("featureViewName", OpenTrainingModelCatalog.SupplierRankingFeatureViewName);
        insert.Parameters.AddWithValue("params", JsonSerializer.Serialize(new
        {
            prediction_view = "vw_supplier_ranking_inference_v1"
        }));
        insert.Parameters.AddWithValue("notes", $"Queued by {WorkerName} after nightly analytics refresh.");
        await insert.ExecuteNonQueryAsync(ct);
    }

    private static string BuildStatusSummary(
        string message,
        IReadOnlyCollection<string> warnings,
        IReadOnlyCollection<string> errors)
    {
        var summary = message;
        if (warnings.Count > 0)
        {
            summary += $" | Warnings: {string.Join("; ", warnings.Take(5))}";
        }

        if (errors.Count > 0)
        {
            summary += $" | Errors: {string.Join("; ", errors.Take(5))}";
        }

        return summary;
    }

    private static string NormalizeRelationName(string value)
    {
        if (!TryParseRelation(value, out var schema, out var rel, out _))
        {
            return value.Trim();
        }

        return string.Equals(schema, "public", StringComparison.OrdinalIgnoreCase)
            ? rel
            : $"{schema}.{rel}";
    }

    private static TimeSpan ParseRunAtUtc(string value)
    {
        if (TimeSpan.TryParseExact(value, "hh\\:mm", CultureInfo.InvariantCulture, out var ts))
            return ts;

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out ts))
            return new TimeSpan(ts.Hours, ts.Minutes, 0);

        return new TimeSpan(0, 10, 0);
    }

    private static bool TryParseRelation(string value, out string schema, out string name, out string quoted)
    {
        schema = "public";
        name = string.Empty;
        quoted = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            name = parts[0];
        }
        else if (parts.Length == 2)
        {
            schema = parts[0];
            name = parts[1];
        }
        else
        {
            return false;
        }

        if (!SafeIdent.IsMatch(schema) || !SafeIdent.IsMatch(name))
            return false;

        quoted = $"\"{schema}\".\"{name}\"";
        return true;
    }

    private static async Task<bool> IsMaterializedViewAsync(NpgsqlConnection connection, string schema, string name, CancellationToken ct)
    {
        const string sql = """
            SELECT 1
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = @schema
              AND c.relname = @name
              AND c.relkind = 'm'
            LIMIT 1;
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("name", name);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    private static async Task<bool> CanRefreshMaterializedViewConcurrentlyAsync(
        NpgsqlConnection connection,
        string schema,
        string name,
        CancellationToken ct)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_class mv
                JOIN pg_namespace ns ON ns.oid = mv.relnamespace
                JOIN pg_index idx ON idx.indrelid = mv.oid
                WHERE ns.nspname = @schema
                  AND mv.relname = @name
                  AND mv.relkind = 'm'
                  AND idx.indisunique
                  AND idx.indisvalid
                  AND idx.indpred IS NULL
                  AND idx.indexprs IS NULL
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("name", name);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static async Task<bool> IsVacuumableRelationAsync(NpgsqlConnection connection, string schema, string name, CancellationToken ct)
    {
        const string sql = """
            SELECT 1
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = @schema
              AND c.relname = @name
              AND c.relkind IN ('r','m','p')
            LIMIT 1;
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("name", name);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        string sql,
        int timeoutSeconds,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = Math.Max(0, timeoutSeconds);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> TryAcquireLockAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = "SELECT pg_try_advisory_lock(@k1, @k2);";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("k1", LockKey1);
        cmd.Parameters.AddWithValue("k2", LockKey2);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = "SELECT pg_advisory_unlock(@k1, @k2);";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("k1", LockKey1);
        cmd.Parameters.AddWithValue("k2", LockKey2);
        await cmd.ExecuteScalarAsync(ct);
    }
}


