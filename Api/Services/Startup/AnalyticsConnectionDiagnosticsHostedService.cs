using Infrastructure.Analytics;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services.Startup;

public sealed class AnalyticsConnectionDiagnosticsHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnalyticsConnectionDiagnosticsHostedService> _logger;

    public AnalyticsConnectionDiagnosticsHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AnalyticsConnectionDiagnosticsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempts = Math.Max(
            1,
            _configuration.GetValue<int?>("StartupTasks:AnalyticsDiagnosticsAttempts") ?? 8);
        var delaySeconds = Math.Max(
            1,
            _configuration.GetValue<int?>("StartupTasks:AnalyticsDiagnosticsRetryDelaySeconds") ?? 15);

        try
        {
            for (var attempt = 1; attempt <= attempts && !stoppingToken.IsCancellationRequested; attempt++)
            {
                var hasMissingSupplierDecisionObjects = await LogDiagnosticsAsync(stoppingToken);
                if (!hasMissingSupplierDecisionObjects)
                {
                    return;
                }

                if (attempt < attempts)
                {
                    _logger.LogInformation(
                        "Analytics DB diagnostics found missing supplier decision objects. Retrying in {DelaySeconds}s ({Attempt}/{Attempts}).",
                        delaySeconds,
                        attempt,
                        attempts);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Analytics DB diagnostics failed.");
        }
    }

    private async Task<bool> LogDiagnosticsAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var connectionString = analyticsDb.Database.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Analytics DB diagnostics skipped: AnalyticsDbContext connection string is missing.");
            return false;
        }

        _logger.LogInformation(
            "Analytics DB context target: {ContextTarget}",
            SummarizeConnection(connectionString));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(stoppingToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                current_database() AS database_name,
                current_user AS user_name,
                current_schema() AS schema_name,
                current_setting('search_path', true) AS search_path,
                inet_server_addr()::text AS server_addr,
                inet_server_port() AS server_port,
                to_regclass('public.vw_supplier_fullprice_signals')::text AS supplier_fullprice_signals,
                to_regclass('public.vw_supplier_decision_score')::text AS supplier_decision_score,
                to_regclass('public.vw_supplier_recommendations')::text AS supplier_recommendations,
                to_regclass('public.mv_supplier_markdown_dependency_cache')::text AS supplier_markdown_dependency_cache,
                to_regclass('public.mv_supplier_decision_score_cache')::text AS supplier_decision_score_cache,
                to_regclass('public.mv_supplier_recommendations_cache')::text AS supplier_recommendations_cache,
                to_regclass('public.supplier_ml_predictions')::text AS supplier_ml_predictions,
                to_regclass('public.vw_supplier_ml_latest_predictions')::text AS supplier_ml_latest_predictions;
            """;

        await using var reader = await command.ExecuteReaderAsync(stoppingToken);
        if (!await reader.ReadAsync(stoppingToken))
        {
            _logger.LogWarning("Analytics DB diagnostics returned no rows.");
            return false;
        }

        var supplierSignalsView = ReadString(reader, "supplier_fullprice_signals") ?? "<missing>";
        var decisionScoreView = ReadString(reader, "supplier_decision_score") ?? "<missing>";
        var recommendationsView = ReadString(reader, "supplier_recommendations") ?? "<missing>";
        var markdownDependencyCache = ReadString(reader, "supplier_markdown_dependency_cache") ?? "<missing>";
        var decisionScoreCache = ReadString(reader, "supplier_decision_score_cache") ?? "<missing>";
        var recommendationsCache = ReadString(reader, "supplier_recommendations_cache") ?? "<missing>";
        var supplierMlPredictions = ReadString(reader, "supplier_ml_predictions") ?? "<missing>";
        var supplierMlLatestPredictions = ReadString(reader, "supplier_ml_latest_predictions") ?? "<missing>";

        _logger.LogInformation(
            "Analytics DB runtime target: database={Database} user={User} schema={Schema} search_path={SearchPath} server={ServerAddr}:{ServerPort} vw_supplier_fullprice_signals={SupplierSignalsView} vw_supplier_decision_score={DecisionScoreView} vw_supplier_recommendations={RecommendationsView} mv_supplier_markdown_dependency_cache={MarkdownDependencyCache} mv_supplier_decision_score_cache={DecisionScoreCache} mv_supplier_recommendations_cache={RecommendationsCache} optional_supplier_ml_predictions={SupplierMlPredictions} optional_vw_supplier_ml_latest_predictions={SupplierMlLatestPredictions}",
            ReadString(reader, "database_name"),
            ReadString(reader, "user_name"),
            ReadString(reader, "schema_name"),
            ReadString(reader, "search_path"),
            ReadString(reader, "server_addr"),
            ReadInt32(reader, "server_port"),
            supplierSignalsView,
            decisionScoreView,
            recommendationsView,
            markdownDependencyCache,
            decisionScoreCache,
            recommendationsCache,
            supplierMlPredictions,
            supplierMlLatestPredictions);

        var missingCoreObjects = supplierSignalsView == "<missing>"
            || decisionScoreView == "<missing>"
            || recommendationsView == "<missing>"
            || markdownDependencyCache == "<missing>"
            || decisionScoreCache == "<missing>"
            || recommendationsCache == "<missing>";

        // The same connection cannot execute a second command while a data reader is active.
        // Diagnose both windowed caches through the endpoint's canonical MV capability contract,
        // not to_regclass alone: a present but unpopulated/stale-column MV is NOT ready.
        await reader.DisposeAsync();
        var score90 = await PostgresMaterializedViewCapabilityReader.InspectAsync(
            connection,
            "mv_supplier_decision_score_cache_90d",
            SupplierDecisionMaterializedViewContract.DecisionScoreRequiredColumns,
            cancellationToken: stoppingToken);
        var score180 = await PostgresMaterializedViewCapabilityReader.InspectAsync(
            connection,
            "mv_supplier_decision_score_cache_180d",
            SupplierDecisionMaterializedViewContract.DecisionScoreRequiredColumns,
            cancellationToken: stoppingToken);

        if (HasUnavailableSupplierWindowedCache(score90, score180))
        {
            _logger.LogWarning(
                "Supplier decision windowed cache capability: 90d={Window90Status} missingColumns={Window90MissingColumns}; 180d={Window180Status} missingColumns={Window180MissingColumns}. These optional views are unavailable; do not substitute all-time data. This diagnostic does not trigger a rebuild.",
                score90.ErrorCode ?? "READY",
                string.Join(",", score90.MissingColumns),
                score180.ErrorCode ?? "READY",
                string.Join(",", score180.MissingColumns));
        }
        else
        {
            _logger.LogInformation(
                "Supplier decision windowed cache capability: 90d=READY 180d=READY.");
        }

        // Keep the retry decision confined to the core supplier objects. Missing optional
        // windowed MVs are visible in logs, but must not force eight more diagnostic retries
        // during a Render cold start when web-side SQL 029 repair is intentionally disabled.
        return missingCoreObjects;
    }

    internal static bool HasUnavailableSupplierWindowedCache(
        MaterializedViewCapabilityResult score90,
        MaterializedViewCapabilityResult score180) =>
        !score90.IsReady || !score180.IsReady;

    private static string SummarizeConnection(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "<unknown-host>" : builder.Host;
            var database = string.IsNullOrWhiteSpace(builder.Database) ? "<unknown-db>" : builder.Database;
            var username = string.IsNullOrWhiteSpace(builder.Username) ? "<unknown-user>" : builder.Username;
            return $"{host}:{builder.Port}/{database} user={username}";
        }
        catch
        {
            return "<unparseable>";
        }
    }

    private static string? ReadString(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadInt32(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}
