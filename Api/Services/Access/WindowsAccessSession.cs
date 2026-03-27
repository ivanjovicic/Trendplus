using System.Data;
using System.Data.Odbc;
using Api.Config;

namespace Api.Services.Access;

public sealed class WindowsAccessSession : IAccessDataReaderSession
{
    private readonly string _sourceFilePath;
    private readonly AccessImportOptions _options;
    private readonly ILogger _logger;
    private readonly OdbcConnection _connection;
    private readonly SemaphoreSlim _metadataLimiter;
    private readonly Dictionary<string, IReadOnlyList<string>> _columnCache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string>? _tableCache;
    private bool _disposed;

    public WindowsAccessSession(string sourceFilePath, AccessImportOptions options, ILogger logger)
    {
        _sourceFilePath = sourceFilePath;
        _options = options;
        _logger = logger;
        _metadataLimiter = new SemaphoreSlim(Math.Max(1, options.MaxMetadataParallelism));
        _connection = new OdbcConnection(
            AccessImportService.BuildAccessOdbcConnectionString(
                sourceFilePath,
                isWindows: true,
                driverPath: null));
    }

    public string Mode => "windows";

    public string SourceFilePath => _sourceFilePath;
    public bool SupportsPredicatePushdown => true;

    public async Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_tableCache is not null)
            return FilterVisibleTables(_tableCache, includeTemporaryTables);

        await _metadataLimiter.WaitAsync(ct);
        try
        {
            if (_tableCache is not null)
                return FilterVisibleTables(_tableCache, includeTemporaryTables);

            await EnsureOpenedAsync(ct);
            var schema = _connection.GetSchema("Tables");
            var tables = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (schema is not null)
            {
                foreach (DataRow row in schema.Rows)
                {
                    if (!AccessImportService.CheckIsUserTable(row, schema))
                        continue;

                    var tableName = AccessImportService.ResolveTableName(row, schema);
                    if (string.IsNullOrWhiteSpace(tableName))
                        continue;

                    if (seen.Add(tableName))
                        tables.Add(tableName);
                }
            }

            _tableCache = tables;
            return FilterVisibleTables(tables, includeTemporaryTables);
        }
        finally
        {
            _metadataLimiter.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_columnCache.TryGetValue(table, out var cached))
            return cached;

        await _metadataLimiter.WaitAsync(ct);
        try
        {
            if (_columnCache.TryGetValue(table, out cached))
                return cached;

            await EnsureOpenedAsync(ct);
            if (!AccessImportService.TryGetQuotedTableIdentifier(table, out var quotedTable, out var failureReason))
                throw new ArgumentException($"Invalid Access table '{table}': {failureReason}", nameof(table));

            using var cmd = new OdbcCommand($"SELECT * FROM {quotedTable} WHERE 1=0", _connection);
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly | CommandBehavior.SequentialAccess, ct);

            var columns = new List<string>();
            if (reader is not null)
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    if (!string.IsNullOrWhiteSpace(name))
                        columns.Add(name);
                }
            }

            _columnCache[table] = columns;
            return columns;
        }
        finally
        {
            _metadataLimiter.Release();
        }
    }

    public async Task<AccessRowCountResult> TryGetExactRowCountAsync(string table, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureOpenedAsync(ct);
        if (!AccessImportService.TryGetQuotedTableIdentifier(table, out var quotedTable, out var failureReason))
            throw new ArgumentException($"Invalid Access table '{table}': {failureReason}", nameof(table));

        using var cmd = new OdbcCommand($"SELECT COUNT(*) FROM {quotedTable}", _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        var count = result switch
        {
            null or DBNull => 0,
            int value => value,
            long value => value > int.MaxValue ? int.MaxValue : (int)value,
            decimal value => value > int.MaxValue ? int.MaxValue : (int)value,
            _ => AccessImportService.ConvertToInt(result) ?? 0
        };

        return AccessRowCountResult.Exact(count);
    }

    public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
        => ReadRowsAsync(table, query: null, ct);

    public async IAsyncEnumerable<AccessDataRow> ReadRowsAsync(
        string table,
        AccessReadQuery? query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureOpenedAsync(ct);

        if (!AccessImportService.TryGetQuotedTableIdentifier(table, out var quotedTable, out var failureReason))
            throw new ArgumentException($"Invalid Access table '{table}': {failureReason}", nameof(table));

        var sql = await BuildSelectSqlAsync(table, quotedTable, query, ct);
        using var cmd = new OdbcCommand(sql.CommandText, _connection);
        for (var i = 0; i < sql.Parameters.Count; i++)
        {
            var value = sql.Parameters[i];
            if (value is DateTime dtValue)
                cmd.Parameters.Add($"@p{i}", OdbcType.DateTime).Value = dtValue;
            else
                cmd.Parameters.AddWithValue($"@p{i}", value);
        }

        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        if (reader is null)
            yield break;

        var columns = new List<string>(reader.FieldCount);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            columns.Add(string.IsNullOrWhiteSpace(name) ? $"col_{i}" : name);
        }

        _columnCache[table] = columns;
        var schema = new AccessDataSchema(columns);

        while (await reader.ReadAsync(ct))
        {
            var values = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                values[i] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);

            yield return new AccessDataRow(schema, values);
        }
    }

    private async Task<(string CommandText, List<object> Parameters)> BuildSelectSqlAsync(
        string table,
        string quotedTable,
        AccessReadQuery? query,
        CancellationToken ct)
    {
        var columns = await GetColumnsAsync(table, ct);
        return BuildSelectSqlFromColumns(columns, quotedTable, query);
    }

    internal static (string CommandText, List<object> Parameters) BuildSelectSqlFromColumns(
        IReadOnlyList<string> columns,
        string quotedTable,
        AccessReadQuery? query)
    {
        if (query is null)
            return ($"SELECT * FROM {quotedTable}", []);

        var mode = string.IsNullOrWhiteSpace(query.CursorMode)
            ? "id"
            : query.CursorMode.Trim().ToLowerInvariant();
        if (string.Equals(mode, "none", StringComparison.OrdinalIgnoreCase))
            return ($"SELECT * FROM {quotedTable}", []);

        var idColumn = ResolveColumn(columns, query.IdAliases);
        var tsColumn = ResolveColumn(columns, query.TimestampAliases);
        var overlapSeconds = Math.Clamp(query.OverlapSeconds, 0, 3600);
        var effectiveTimestamp = query.CursorTimestampUtc?.AddSeconds(-overlapSeconds);
        var parameters = new List<object>();

        string whereSql;
        string orderSql;

        switch (mode)
        {
            case "timestamp":
                if (!effectiveTimestamp.HasValue || string.IsNullOrWhiteSpace(tsColumn))
                    return ($"SELECT * FROM {quotedTable}", []);

                parameters.Add(effectiveTimestamp.Value);
                whereSql = $"{AccessImportService.QuoteAccessIdentifier(tsColumn)} > ?";
                orderSql = $" ORDER BY {AccessImportService.QuoteAccessIdentifier(tsColumn)}";
                break;

            case "timestamp_then_id":
                if (effectiveTimestamp.HasValue && !string.IsNullOrWhiteSpace(tsColumn))
                {
                    if (!string.IsNullOrWhiteSpace(idColumn))
                    {
                        var tieBreakerId = query.CursorTieBreakerId ?? query.CursorId;
                        if (tieBreakerId.HasValue)
                        {
                            parameters.Add(effectiveTimestamp.Value);
                            parameters.Add(effectiveTimestamp.Value);
                            parameters.Add(tieBreakerId.Value);
                            whereSql =
                                $"({AccessImportService.QuoteAccessIdentifier(tsColumn)} > ? OR ({AccessImportService.QuoteAccessIdentifier(tsColumn)} = ? AND {AccessImportService.QuoteAccessIdentifier(idColumn)} > ?))";
                            orderSql =
                                $" ORDER BY {AccessImportService.QuoteAccessIdentifier(tsColumn)}, {AccessImportService.QuoteAccessIdentifier(idColumn)}";
                            break;
                        }
                    }

                    parameters.Add(effectiveTimestamp.Value);
                    whereSql = $"{AccessImportService.QuoteAccessIdentifier(tsColumn)} > ?";
                    orderSql = $" ORDER BY {AccessImportService.QuoteAccessIdentifier(tsColumn)}";
                    break;
                }

                if (!query.CursorId.HasValue || string.IsNullOrWhiteSpace(idColumn))
                    return ($"SELECT * FROM {quotedTable}", []);

                parameters.Add(query.CursorId.Value);
                whereSql = $"{AccessImportService.QuoteAccessIdentifier(idColumn)} > ?";
                orderSql = $" ORDER BY {AccessImportService.QuoteAccessIdentifier(idColumn)}";
                break;

            case "id_or_composite":
            case "id":
            default:
                if (!query.CursorId.HasValue || string.IsNullOrWhiteSpace(idColumn))
                    return ($"SELECT * FROM {quotedTable}", []);

                parameters.Add(query.CursorId.Value);
                whereSql = $"{AccessImportService.QuoteAccessIdentifier(idColumn)} > ?";
                orderSql = $" ORDER BY {AccessImportService.QuoteAccessIdentifier(idColumn)}";
                break;
        }

        return ($"SELECT * FROM {quotedTable} WHERE {whereSql}{orderSql}", parameters);
    }

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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _metadataLimiter.Dispose();
        await _connection.DisposeAsync();
    }

    private async Task EnsureOpenedAsync(CancellationToken ct)
    {
        if (_connection.State == ConnectionState.Open)
            return;

        await _connection.OpenAsync(ct);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsAccessSession));
    }

    private static IReadOnlyList<string> FilterVisibleTables(IEnumerable<string> tables, bool includeTemporaryTables)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filtered = new List<string>();

        foreach (var table in tables)
        {
            if (string.IsNullOrWhiteSpace(table))
                continue;

            if (table.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!includeTemporaryTables && AccessImportService.Normalize(table).Contains("privremena", StringComparison.Ordinal))
                continue;

            if (seen.Add(table))
                filtered.Add(table);
        }

        return filtered;
    }
}
