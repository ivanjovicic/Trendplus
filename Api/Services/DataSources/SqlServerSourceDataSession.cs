using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace Api.Services.DataSources;

/// <summary>
/// Read-only SQL Server proof connector against <see cref="ISourceDataSession"/>.
/// Does not persist credentials, accept arbitrary SQL, or perform writes.
/// </summary>
public sealed class SqlServerSourceDataSession : ISourceDataSession
{
    public const string ProviderName = "sqlserver";

    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly int _commandTimeoutSeconds;
    private readonly SqlConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, IReadOnlyList<string>> _columnCache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string>? _tableCache;
    private bool _disposed;

    public SqlServerSourceDataSession(string connectionString, ILogger logger, int commandTimeoutSeconds = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commandTimeoutSeconds = Math.Clamp(commandTimeoutSeconds, 1, 3600);
        _connectionString = BuildReadOnlyConnectionString(connectionString);
        SourceIdentity = SqlServerConnectionDiagnostics.ToSourceIdentity(_connectionString);
        _connection = new SqlConnection(_connectionString);
    }

    public string Provider => ProviderName;

    public string Mode => "read-only";

    public string SourceIdentity { get; }

    public SourceCapabilities Capabilities { get; } = new(
        SchemaDiscovery: true,
        ExactRowCount: true,
        PredicatePushdown: true,
        IdCursor: true,
        TimestampCursor: true,
        CompositeTimestampIdCursor: true,
        Cancellation: true,
        Cdc: false);

    public async Task TestConnectionAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureOpenedAsync(ct);
        await using var command = CreateCommand("SELECT 1");
        _ = await command.ExecuteScalarAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _ = includeTemporaryTables;
        if (_tableCache is not null)
            return _tableCache;

        await _gate.WaitAsync(ct);
        try
        {
            if (_tableCache is not null)
                return _tableCache;

            await EnsureOpenedAsync(ct);
            const string sql = """
                SELECT TABLE_SCHEMA, TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE IN ('BASE TABLE', 'VIEW')
                  AND TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')
                ORDER BY TABLE_SCHEMA, TABLE_NAME
                """;

            await using var command = CreateCommand(sql);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var tables = new List<string>();
            while (await reader.ReadAsync(ct))
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                tables.Add($"{schema}.{name}");
            }

            _tableCache = tables;
            return tables;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailure("schema discovery", ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_columnCache.TryGetValue(table, out var cached))
            return cached;

        if (!SqlServerIdentifier.TryParseTable(table, out var schema, out var name, out var failureReason))
            throw new InvalidOperationException(failureReason);

        await _gate.WaitAsync(ct);
        try
        {
            if (_columnCache.TryGetValue(table, out cached))
                return cached;

            await EnsureOpenedAsync(ct);
            const string sql = """
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @name
                ORDER BY ORDINAL_POSITION
                """;

            await using var command = CreateCommand(sql);
            command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
            command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = name });
            await using var reader = await command.ExecuteReaderAsync(ct);
            var columns = new List<string>();
            while (await reader.ReadAsync(ct))
                columns.Add(reader.GetString(0));

            _columnCache[table] = columns;
            return columns;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not InvalidOperationException)
        {
            LogFailure("column discovery", ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SourceRowCountResult> TryGetRowCountAsync(string table, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (!SqlServerIdentifier.TryQuoteTable(table, out var quotedTable, out var failureReason))
            throw new InvalidOperationException(failureReason);

        await EnsureOpenedAsync(ct);
        await using var command = CreateCommand($"SELECT COUNT_BIG(*) FROM {quotedTable}");
        var result = await command.ExecuteScalarAsync(ct);
        var count = result is long big
            ? big > int.MaxValue ? int.MaxValue : (int)big
            : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
        return SourceRowCountResult.Exact(count);
    }

    public IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
        => ReadRowsAsync(table, query: null, ct);

    public async IAsyncEnumerable<SourceDataRow> ReadRowsAsync(
        string table,
        SourceReadQuery? query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (!SqlServerIdentifier.TryQuoteTable(table, out var quotedTable, out var failureReason))
            throw new InvalidOperationException(failureReason);

        var columns = await GetColumnsAsync(table, ct);
        var fragment = BuildSelectSqlFromColumns(columns, quotedTable, query);

        await EnsureOpenedAsync(ct);
        await using var command = CreateCommand(fragment.CommandText);
        foreach (var (parameterName, value) in fragment.Parameters)
            command.Parameters.Add(CreateParameter(parameterName, value));

        SqlDataReader reader;
        try
        {
            reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess | CommandBehavior.SingleResult,
                ct);
        }
        catch (Exception ex) when (IsCanceled(ex, ct))
        {
            throw new OperationCanceledException("SQL Server source read was canceled.", ex, ct);
        }

        await using (reader)
        {
            var schema = new SourceDataSchema(columns);
            while (true)
            {
                bool hasRow;
                try
                {
                    hasRow = await reader.ReadAsync(ct);
                }
                catch (Exception ex) when (IsCanceled(ex, ct))
                {
                    throw new OperationCanceledException("SQL Server source read was canceled.", ex, ct);
                }

                if (!hasRow)
                    break;

                var values = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var raw = reader.GetValue(i);
                    values[i] = raw is DBNull ? null : raw;
                }

                yield return new SourceDataRow(schema, values);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _gate.Dispose();
        await _connection.DisposeAsync();
    }

    internal static SqlServerSqlFragment BuildSelectSqlFromColumns(
        IReadOnlyList<string> columns,
        string quotedTable,
        SourceReadQuery? query)
    {
        var parameters = new List<(string Name, object Value)>();
        var select = BuildSelectList(query, parameters);
        if (query is null)
        {
            var orderColumn = ResolveDefaultOrderColumn(columns);
            var orderSql = orderColumn is null
                ? string.Empty
                : $" ORDER BY {SqlServerIdentifier.Quote(orderColumn)}";
            return new SqlServerSqlFragment($"{select} {quotedTable}{orderSql}", parameters);
        }

        var mode = string.IsNullOrWhiteSpace(query.CursorMode)
            ? "id"
            : query.CursorMode.Trim().ToLowerInvariant();
        if (string.Equals(mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            var orderColumn = ResolveDefaultOrderColumn(columns);
            var orderSql = orderColumn is null
                ? string.Empty
                : $" ORDER BY {SqlServerIdentifier.Quote(orderColumn)}";
            return new SqlServerSqlFragment($"{select} {quotedTable}{orderSql}", parameters);
        }

        var idColumn = ResolveColumn(columns, query.IdAliases);
        var tsColumn = ResolveColumn(columns, query.TimestampAliases);
        var overlapSeconds = Math.Clamp(query.OverlapSeconds, 0, 3600);
        var effectiveTimestamp = query.CursorTimestampUtc?.AddSeconds(-overlapSeconds);

        string whereSql;
        string orderSqlClause;

        switch (mode)
        {
            case "timestamp":
                if (!effectiveTimestamp.HasValue || string.IsNullOrWhiteSpace(tsColumn))
                    return FullScan(select, quotedTable, columns, parameters);

                parameters.Add(("@p0", effectiveTimestamp.Value));
                whereSql = $"{SqlServerIdentifier.Quote(tsColumn)} > @p0";
                orderSqlClause = $" ORDER BY {SqlServerIdentifier.Quote(tsColumn)}";
                break;

            case "timestamp_then_id":
                if (effectiveTimestamp.HasValue && !string.IsNullOrWhiteSpace(tsColumn))
                {
                    if (!string.IsNullOrWhiteSpace(idColumn))
                    {
                        var tieBreakerId = query.CursorTieBreakerId ?? query.CursorId;
                        if (tieBreakerId.HasValue)
                        {
                            parameters.Add(("@p0", effectiveTimestamp.Value));
                            parameters.Add(("@p1", effectiveTimestamp.Value));
                            parameters.Add(("@p2", tieBreakerId.Value));
                            whereSql =
                                $"({SqlServerIdentifier.Quote(tsColumn)} > @p0 OR ({SqlServerIdentifier.Quote(tsColumn)} = @p1 AND {SqlServerIdentifier.Quote(idColumn)} > @p2))";
                            orderSqlClause =
                                $" ORDER BY {SqlServerIdentifier.Quote(tsColumn)}, {SqlServerIdentifier.Quote(idColumn)}";
                            break;
                        }
                    }

                    parameters.Add(("@p0", effectiveTimestamp.Value));
                    whereSql = $"{SqlServerIdentifier.Quote(tsColumn)} > @p0";
                    orderSqlClause = $" ORDER BY {SqlServerIdentifier.Quote(tsColumn)}";
                    break;
                }

                if (!query.CursorId.HasValue || string.IsNullOrWhiteSpace(idColumn))
                    return FullScan(select, quotedTable, columns, parameters);

                parameters.Add(("@p0", query.CursorId.Value));
                whereSql = $"{SqlServerIdentifier.Quote(idColumn)} > @p0";
                orderSqlClause = $" ORDER BY {SqlServerIdentifier.Quote(idColumn)}";
                break;

            case "id_or_composite":
            case "id":
            default:
                if (!query.CursorId.HasValue || string.IsNullOrWhiteSpace(idColumn))
                    return FullScan(select, quotedTable, columns, parameters);

                parameters.Add(("@p0", query.CursorId.Value));
                whereSql = $"{SqlServerIdentifier.Quote(idColumn)} > @p0";
                orderSqlClause = $" ORDER BY {SqlServerIdentifier.Quote(idColumn)}";
                break;
        }

        return new SqlServerSqlFragment($"{select} {quotedTable} WHERE {whereSql}{orderSqlClause}", parameters);
    }

    private static SqlServerSqlFragment FullScan(
        string select,
        string quotedTable,
        IReadOnlyList<string> columns,
        List<(string Name, object Value)> parameters)
    {
        var orderColumn = ResolveDefaultOrderColumn(columns);
        var orderSql = orderColumn is null
            ? string.Empty
            : $" ORDER BY {SqlServerIdentifier.Quote(orderColumn)}";
        return new SqlServerSqlFragment($"{select} {quotedTable}{orderSql}", parameters);
    }

    private static string BuildSelectList(SourceReadQuery? query, List<(string Name, object Value)> parameters)
    {
        if (query?.MaxRows is int maxRows && maxRows > 0)
        {
            parameters.Add(("@maxRows", maxRows));
            return "SELECT TOP (@maxRows) * FROM";
        }

        return "SELECT * FROM";
    }

    private static string? ResolveDefaultOrderColumn(IReadOnlyList<string> columns)
        => ResolveColumn(columns, ["id"]) ?? (columns.Count > 0 ? columns[0] : null);

    private static string? ResolveColumn(IReadOnlyList<string> columns, IReadOnlyList<string> normalizedAliases)
    {
        if (columns.Count == 0 || normalizedAliases.Count == 0)
            return null;

        var normalizedToSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
        {
            var source = columns[i];
            var normalized = AccessImportService.Normalize(source);
            if (!string.IsNullOrWhiteSpace(normalized) && !normalizedToSource.ContainsKey(normalized))
                normalizedToSource[normalized] = source;
        }

        for (var i = 0; i < normalizedAliases.Count; i++)
        {
            var alias = AccessImportService.Normalize(normalizedAliases[i]);
            if (!string.IsNullOrWhiteSpace(alias) && normalizedToSource.TryGetValue(alias, out var source))
                return source;
        }

        return null;
    }

    private static string BuildReadOnlyConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };

        if (string.IsNullOrWhiteSpace(builder.ApplicationName))
            builder.ApplicationName = "Trendplus.SqlServerSource";

        return builder.ConnectionString;
    }

    private static SqlParameter CreateParameter(string name, object value)
    {
        return value switch
        {
            DateTime dateTime => new SqlParameter(name, SqlDbType.DateTime2) { Value = dateTime },
            long number => new SqlParameter(name, SqlDbType.BigInt) { Value = number },
            int number => new SqlParameter(name, SqlDbType.Int) { Value = number },
            _ => new SqlParameter(name, value)
        };
    }

    private SqlCommand CreateCommand(string commandText)
    {
        return new SqlCommand(commandText, _connection)
        {
            CommandTimeout = _commandTimeoutSeconds,
            CommandType = CommandType.Text
        };
    }

    private async Task EnsureOpenedAsync(CancellationToken ct)
    {
        if (_connection.State == ConnectionState.Open)
            return;

        try
        {
            await _connection.OpenAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailure("open", ex);
            throw new InvalidOperationException(
                $"SQL Server source connection failed ({SqlServerConnectionDiagnostics.Categorize(ex)}).",
                ex);
        }
    }

    private void LogFailure(string operation, Exception exception)
    {
        var category = SqlServerConnectionDiagnostics.Categorize(exception);
        var number = exception is SqlException sql ? sql.Number : 0;
        _logger.LogWarning(
            "SQL Server source {Operation} failed: category={Category} number={Number} identity={Identity}",
            operation,
            category,
            number,
            SourceIdentity);
    }

    private static bool IsCanceled(Exception exception, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return true;

        return exception is SqlException sql
               && sql.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}

internal readonly record struct SqlServerSqlFragment(
    string CommandText,
    IReadOnlyList<(string Name, object Value)> Parameters);
