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

    public async IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureOpenedAsync(ct);

        if (!AccessImportService.TryGetQuotedTableIdentifier(table, out var quotedTable, out var failureReason))
            throw new ArgumentException($"Invalid Access table '{table}': {failureReason}", nameof(table));

        using var cmd = new OdbcCommand($"SELECT * FROM {quotedTable}", _connection);
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
