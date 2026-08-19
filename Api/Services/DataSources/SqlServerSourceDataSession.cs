using System.Data;
using System.Globalization;
using Api.Services.Access;
using Microsoft.Data.SqlClient;

namespace Api.Services.DataSources;

/// <summary>
/// Read-only SQL Server proof connector for provider-neutral source contract validation.
/// </summary>
public sealed class SqlServerSourceDataSession : ISourceDataSession
{
    private readonly string _connectionString;
    private readonly string _sourceIdentity;

    public SqlServerSourceDataSession(string connectionString, int commandTimeoutSeconds = 30)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        _connectionString = builder.ConnectionString;
        _sourceIdentity = BuildSourceIdentity(builder);
        CommandTimeoutSeconds = Math.Max(1, commandTimeoutSeconds);
    }

    public int CommandTimeoutSeconds { get; }

    public string Provider => "sqlserver";

    public string Mode => "readonly";

    public string SourceIdentity => _sourceIdentity;

    public SourceCapabilities Capabilities => new(
        SchemaDiscovery: true,
        ExactRowCount: true,
        PredicatePushdown: true,
        IdCursor: true,
        TimestampCursor: true,
        CompositeTimestampIdCursor: true,
        Cancellation: true,
        Cdc: false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public async Task TestConnectionAsync(CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        using var command = CreateCommand(connection, "SELECT 1");
        await command.ExecuteScalarAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        using var command = CreateCommand(
            connection,
            """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """);

        using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        var tables = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);

            if (!includeTemporaryTables && table.StartsWith("#", StringComparison.Ordinal))
                continue;

            tables.Add($"{schema}.{table}");
        }

        return tables;
    }

    public async Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
    {
        var reference = ParseTableReference(table);

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        using var command = CreateCommand(
            connection,
            """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """);
        AddStringParameter(command, "@schema", reference.Schema);
        AddStringParameter(command, "@table", reference.Name);

        using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        var columns = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            var column = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(column))
                columns.Add(column);
        }

        return columns;
    }

    public async Task<SourceRowCountResult> TryGetRowCountAsync(string table, CancellationToken ct = default)
    {
        var reference = ParseTableReference(table);

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        using var command = CreateCommand(
            connection,
            $"SELECT COUNT_BIG(1) FROM {QuoteQualifiedIdentifier(reference)}");
        var result = await command.ExecuteScalarAsync(ct);

        var count = result switch
        {
            null or DBNull => 0,
            int value => value,
            long value => value > int.MaxValue ? int.MaxValue : (int)value,
            decimal value => value > int.MaxValue ? int.MaxValue : (int)value,
            _ => Convert.ToInt64(result, CultureInfo.InvariantCulture) > int.MaxValue
                ? int.MaxValue
                : (int)Convert.ToInt64(result, CultureInfo.InvariantCulture)
        };

        return SourceRowCountResult.Exact(count);
    }

    public IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
        => ReadRowsAsync(table, query: null, ct);

    public async IAsyncEnumerable<SourceDataRow> ReadRowsAsync(
        string table,
        SourceReadQuery? query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var reference = ParseTableReference(table);
        var columns = await GetColumnsAsync(table, ct);
        var sql = BuildSelectSql(columns, reference, query);

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        using var command = CreateCommand(connection, sql.CommandText);
        foreach (var parameter in sql.Parameters)
            command.Parameters.Add(parameter);

        using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        if (reader is null)
            yield break;

        var schema = new SourceDataSchema(columns);
        while (await reader.ReadAsync(ct))
        {
            var values = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                values[i] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);

            yield return new SourceDataRow(schema, values);
        }
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    private SqlCommand CreateCommand(SqlConnection connection, string commandText)
        => new(commandText, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

    private static void AddStringParameter(SqlCommand command, string name, string value)
    {
        command.Parameters.Add(new SqlParameter(name, SqlDbType.NVarChar, 128) { Value = value });
    }

    private static (string CommandText, List<SqlParameter> Parameters) BuildSelectSql(
        IReadOnlyList<string> columns,
        TableReference reference,
        SourceReadQuery? query)
    {
        var tableSql = QuoteQualifiedIdentifier(reference);
        var selectList = columns.Count == 0
            ? "*"
            : string.Join(", ", columns.Select(QuoteSqlServerIdentifier));
        var fullOrderBy = columns.Count == 0
            ? string.Empty
            : $" ORDER BY {string.Join(", ", columns.Select(QuoteSqlServerIdentifier))}";

        if (query is null)
            return ($"SELECT {selectList} FROM {tableSql}{fullOrderBy}", []);

        var mode = NormalizeMode(query.CursorMode);
        if (string.Equals(mode, "none", StringComparison.OrdinalIgnoreCase))
            return ($"SELECT {selectList} FROM {tableSql}{fullOrderBy}", []);

        var idColumn = ResolveColumn(columns, query.IdAliases);
        var timestampColumn = ResolveColumn(columns, query.TimestampAliases);
        var overlapSeconds = Math.Clamp(query.OverlapSeconds, 0, 3600);
        var effectiveTimestamp = query.CursorTimestampUtc?.AddSeconds(-overlapSeconds);

        if (string.Equals(mode, "timestamp", StringComparison.OrdinalIgnoreCase))
        {
            if (effectiveTimestamp.HasValue && !string.IsNullOrWhiteSpace(timestampColumn))
            {
                var parameters = new List<SqlParameter>
                {
                    CreateDateTimeParameter("@p0", effectiveTimestamp.Value)
                };

                return (
                    $"SELECT {selectList} FROM {tableSql} WHERE {QuoteSqlServerIdentifier(timestampColumn)} > @p0 ORDER BY {QuoteSqlServerIdentifier(timestampColumn)}",
                    parameters);
            }

            return ($"SELECT {selectList} FROM {tableSql}{fullOrderBy}", []);
        }

        if (string.Equals(mode, "timestamp_then_id", StringComparison.OrdinalIgnoreCase))
        {
            if (effectiveTimestamp.HasValue && !string.IsNullOrWhiteSpace(timestampColumn))
            {
                if (!string.IsNullOrWhiteSpace(idColumn))
                {
                    var tieBreakerId = query.CursorTieBreakerId ?? query.CursorId;
                    if (tieBreakerId.HasValue)
                    {
                        var parameters = new List<SqlParameter>
                        {
                            CreateDateTimeParameter("@p0", effectiveTimestamp.Value),
                            CreateDateTimeParameter("@p1", effectiveTimestamp.Value),
                            CreateBigIntParameter("@p2", tieBreakerId.Value)
                        };

                        return (
                            $"SELECT {selectList} FROM {tableSql} WHERE ({QuoteSqlServerIdentifier(timestampColumn)} > @p0 OR ({QuoteSqlServerIdentifier(timestampColumn)} = @p1 AND {QuoteSqlServerIdentifier(idColumn)} > @p2)) ORDER BY {QuoteSqlServerIdentifier(timestampColumn)}, {QuoteSqlServerIdentifier(idColumn)}",
                            parameters);
                    }
                }

                var fallbackParameters = new List<SqlParameter>
                {
                    CreateDateTimeParameter("@p0", effectiveTimestamp.Value)
                };

                return (
                    $"SELECT {selectList} FROM {tableSql} WHERE {QuoteSqlServerIdentifier(timestampColumn)} > @p0 ORDER BY {QuoteSqlServerIdentifier(timestampColumn)}",
                    fallbackParameters);
            }

            if (query.CursorId.HasValue && !string.IsNullOrWhiteSpace(idColumn))
            {
                var parameters = new List<SqlParameter>
                {
                    CreateBigIntParameter("@p0", query.CursorId.Value)
                };

                return (
                    $"SELECT {selectList} FROM {tableSql} WHERE {QuoteSqlServerIdentifier(idColumn)} > @p0 ORDER BY {QuoteSqlServerIdentifier(idColumn)}",
                    parameters);
            }

            return ($"SELECT {selectList} FROM {tableSql}{fullOrderBy}", []);
        }

        if (string.Equals(mode, "id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "id_or_composite", StringComparison.OrdinalIgnoreCase))
        {
            if (query.CursorId.HasValue && !string.IsNullOrWhiteSpace(idColumn))
            {
                var parameters = new List<SqlParameter>
                {
                    CreateBigIntParameter("@p0", query.CursorId.Value)
                };

                return (
                    $"SELECT {selectList} FROM {tableSql} WHERE {QuoteSqlServerIdentifier(idColumn)} > @p0 ORDER BY {QuoteSqlServerIdentifier(idColumn)}",
                    parameters);
            }
        }

        return ($"SELECT {selectList} FROM {tableSql}{fullOrderBy}", []);
    }

    private static SqlParameter CreateDateTimeParameter(string name, DateTime value)
        => new(name, SqlDbType.DateTime2) { Value = value };

    private static SqlParameter CreateBigIntParameter(string name, long value)
        => new(name, SqlDbType.BigInt) { Value = value };

    private static string NormalizeMode(string? mode)
        => string.IsNullOrWhiteSpace(mode) ? "id" : mode.Trim().ToLowerInvariant();

    private static string? ResolveColumn(IReadOnlyList<string> columns, IReadOnlyList<string> aliases)
    {
        if (columns.Count == 0 || aliases.Count == 0)
            return null;

        var normalizedToSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
        {
            var source = columns[i];
            var normalized = AccessImportService.Normalize(source);
            if (!string.IsNullOrWhiteSpace(normalized) && !normalizedToSource.ContainsKey(normalized))
                normalizedToSource[normalized] = source;
        }

        for (var i = 0; i < aliases.Count; i++)
        {
            var alias = AccessImportService.Normalize(aliases[i]);
            if (!string.IsNullOrWhiteSpace(alias) && normalizedToSource.TryGetValue(alias, out var source))
                return source;
        }

        return null;
    }

    private static string QuoteQualifiedIdentifier(TableReference reference)
        => $"{QuoteSqlServerIdentifier(reference.Schema)}.{QuoteSqlServerIdentifier(reference.Name)}";

    private static string QuoteSqlServerIdentifier(string identifier)
        => $"[{identifier.Replace("]", "]]")}]";

    private static TableReference ParseTableReference(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("Table name is empty.", nameof(table));

        var trimmed = table.Trim();
        if (trimmed.IndexOfAny(['\0', '\r', '\n', ';']) >= 0)
            throw new ArgumentException("Table name contains prohibited characters.", nameof(table));

        var parts = trimmed.Split('.', 2, StringSplitOptions.None);
        if (parts.Length == 1)
            return new TableReference("dbo", UnquoteIdentifier(parts[0]));

        return new TableReference(UnquoteIdentifier(parts[0]), UnquoteIdentifier(parts[1]));
    }

    private static string UnquoteIdentifier(string identifier)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']')
            return trimmed[1..^1].Replace("]]", "]");

        return trimmed;
    }

    private static string BuildSourceIdentity(SqlConnectionStringBuilder builder)
    {
        var server = string.IsNullOrWhiteSpace(builder.DataSource) ? "unknown" : builder.DataSource.Trim();
        var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "default" : builder.InitialCatalog.Trim();
        return $"sqlserver://{server}/{database}";
    }

    private sealed record TableReference(string Schema, string Name);
}
