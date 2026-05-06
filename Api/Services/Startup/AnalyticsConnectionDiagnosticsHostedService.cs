using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services.Startup;

public sealed class AnalyticsConnectionDiagnosticsHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsConnectionDiagnosticsHostedService> _logger;

    public AnalyticsConnectionDiagnosticsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsConnectionDiagnosticsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            var connectionString = analyticsDb.Database.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning("Analytics DB diagnostics skipped: AnalyticsDbContext connection string is missing.");
                return;
            }

            _logger.LogInformation(
                "Analytics DB configured target: {ConfiguredTarget}",
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
                    to_regclass('public.mv_supplier_decision_score_cache')::text AS supplier_decision_score_cache;
                """;

            await using var reader = await command.ExecuteReaderAsync(stoppingToken);
            if (!await reader.ReadAsync(stoppingToken))
            {
                _logger.LogWarning("Analytics DB diagnostics returned no rows.");
                return;
            }

            _logger.LogInformation(
                "Analytics DB runtime target: database={Database} user={User} schema={Schema} search_path={SearchPath} server={ServerAddr}:{ServerPort} vw_supplier_fullprice_signals={SupplierSignalsView} mv_supplier_decision_score_cache={DecisionScoreCache}",
                ReadString(reader, "database_name"),
                ReadString(reader, "user_name"),
                ReadString(reader, "schema_name"),
                ReadString(reader, "search_path"),
                ReadString(reader, "server_addr"),
                ReadInt32(reader, "server_port"),
                ReadString(reader, "supplier_fullprice_signals") ?? "<missing>",
                ReadString(reader, "supplier_decision_score_cache") ?? "<missing>");
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
