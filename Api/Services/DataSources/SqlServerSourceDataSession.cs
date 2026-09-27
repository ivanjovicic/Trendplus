
using System.Data; using System.Runtime.CompilerServices; using Api.Services; using global::Microsoft.Data.SqlClient;  namespace Api.Services.DataSources;  public sealed class SqlServerSourceDataSession : ISourceDataSession {     private readonly SqlConnection _connection;     private readonly ILogger _logger;     private readonly SemaphoreSlim _metadataLimiter;     private readonly Dictionary<string, IReadOnlyList<SourceColumnDefinition>> _columnDefinitionCache = new(StringComparer.OrdinalIgnoreCase);     private IReadOnlyList<string>? _tableCache;     private readonly int _commandTimeoutSeconds;     private readonly string _sourceIdentity;     private bool _disposed;      public SqlServerSourceDataSession(         string connectionString,         ILogger logger,         int commandTimeoutSeconds = 30,         int maxMetadataParallelism = 4)     {         ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);         _logger = logger ?? throw new ArgumentNullException(nameof(logger));         _connection = new SqlConnection(BuildReadOnlyConnectionString(connectionString));         _metadataLimiter = new SemaphoreSlim(Math.Max(1, maxMetadataParallelism));         _commandTimeoutSeconds = Math.Clamp(commandTimeoutSeconds, 1, 3600);         _sourceIdentity = BuildSourceIdentity(connectionString);     }      public string Provider => "sqlserver";      public string Mode => "read-only";      public string SourceIdentity => _sourceIdentity;      public SourceCapabilities Capabilities => new(         SchemaDiscovery: true,         ExactRowCount: true,         PredicatePushdown: true,         IdCursor: true,         TimestampCursor: true,         CompositeTimestampIdCursor: true,         Cancellation: true,         Cdc: false);      public async Task TestConnectionAsync(CancellationToken ct = default)     {         ThrowIfDisposed();         _logger.LogInformation(             "SQL Server source connection test started. Provider: {Provider}. Mode: {Mode}. SourceIdentity: {SourceIdentity}.",             Provider,             Mode,             SourceIdentity);          await EnsureOpenedAsync(ct);         using var cmd = CreateCommand("SELECT 1");         _ = await cmd.ExecuteScalarAsync(ct);          _logger.LogInformation(             "SQL Server source connection test succeeded. SourceIdentity: {SourceIdentity}.",             SourceIdentity);     }      public async Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)     {         ThrowIfDisposed();         if (_tableCache is not null)             return FilterVisibleTables(_tableCache, includeTemporaryTables);          await _metadataLimiter.WaitAsync(ct);         try         {             if (_tableCache is not null)                 return FilterVisibleTables(_tableCache, includeTemporaryTables);              await EnsureOpenedAsync(ct);             const string sql =                 """                 SELECT s.name AS schema_name, o.name AS object_name                 FROM sys.objects AS o                 INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id                 WHERE o.type IN ('U', 'V')                   AND o.is_ms_shipped = 0                   AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')                 ORDER BY s.name, o.name;                 """;              using var cmd = CreateCommand(sql);             using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);              var tables = new List<string>();             while (await reader.ReadAsync(ct))             {                 var schema = await reader.IsDBNullAsync(0, ct) ? null : reader.GetString(0);                 var name = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);                 if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(name))                     continue;                  tables.Add($"{QuoteIdentifier(schema)}.{QuoteIdentifier(name)}");             }              _tableCache = tables;             return FilterVisibleTables(tables, includeTemporaryTables);         }         finally         {             _metadataLimiter.Release();         }     }      public async Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)     {         var definitions = await GetColumnDefinitionsAsync(table, ct);         return definitions.Select(column => column.Name).ToArray();     }      public async Task<IReadOnlyList<SourceColumnDefinition>> GetColumnDefinitionsAsync(string table, CancellationToken ct = default)     {         ThrowIfDisposed();         if (_columnDefinitionCache.TryGetValue(table, out var cached))             return cached;          await _metadataLimiter.WaitAsync(ct);         try         {             if (_columnDefinitionCache.TryGetValue(table, out cached))                 return cached;              var identifier = ParseTableIdentifier(table);             await EnsureOpenedAsync(ct);              const string sql =                 """                 SELECT                     c.COLUMN_NAME,                     c.DATA_TYPE,                     CASE                         WHEN c.IS_NULLABLE = 'YES' THEN CAST(1 AS bit)                         WHEN c.IS_NULLABLE = 'NO' THEN CAST(0 AS bit)                         ELSE NULL                     END AS IS_NULLABLE,                     c.ORDINAL_POSITION                 FROM INFORMATION_SCHEMA.COLUMNS AS c                 WHERE c.TABLE_SCHEMA = @schema                   AND c.TABLE_NAME = @table                 ORDER BY c.ORDINAL_POSITION;                 """;              using var cmd = CreateCommand(sql);             cmd.Parameters.AddWithValue("@schema", identifier.Schema);             cmd.Parameters.AddWithValue("@table", identifier.ObjectName);             using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);              var columns = new List<SourceColumnDefinition>();             while (await reader.ReadAsync(ct))             {                 if (await reader.IsDBNullAsync(0, ct))                     continue;                  var column = reader.GetString(0);                 if (string.IsNullOrWhiteSpace(column))                     continue;                  var sourceType = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);                 var isNullable = await reader.IsDBNullAsync(2, ct) ? null : (bool?)reader.GetBoolean(2);                 var ordinal = await reader.IsDBNullAsync(3, ct) ? columns.Count : Convert.ToInt32(reader.GetValue(3)) - 1;                  columns.Add(new SourceColumnDefinition(column, sourceType, isNullable, Math.Max(0, ordinal)));             }              _columnDefinitionCache[table] = columns;             return columns;         }         finally         {             _metadataLimiter.Release();         }     }      public async Task<SourceRowCountResult> TryGetRowCountAsync(string table, CancellationToken ct = default)     {         ThrowIfDisposed();         var identifier = ParseTableIdentifier(table);         await EnsureOpenedAsync(ct);          using var cmd = CreateCommand($"SELECT COUNT_BIG(*) FROM {identifier.QuotedIdentifier}");         var result = await cmd.ExecuteScalarAsync(ct);         var count = result switch         {             null or DBNull => 0,             int value => value,             long value => value > int.MaxValue ? int.MaxValue : (int)value,             decimal value => value > int.MaxValue ? int.MaxValue : (int)value,             _ => AccessImportService.ConvertToInt(result) ?? 0         };          return SourceRowCountResult.Exact(count);     }      public IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, CancellationToken ct = default)         => ReadRowsAsync(table, query: null, ct);      public async IAsyncEnumerable<SourceDataRow> ReadRowsAsync(         string table,         SourceReadQuery? query,         [EnumeratorCancellation] CancellationToken ct = default)     {         ThrowIfDisposed();         var identifier = ParseTableIdentifier(table);         await EnsureOpenedAsync(ct);          var sql = await BuildSelectSqlAsync(table, identifier.QuotedIdentifier, query, ct);         await using var cmd = CreateCommand(sql.CommandText);         foreach (var (parameterName, value) in sql.Parameters)             cmd.Parameters.Add(CreateParameter(parameterName, value));          _logger.LogInformation(             "SQL Server source row stream started. SourceIdentity: {SourceIdentity}. TableName: {TableName}. QueryMode: {QueryMode}.",             SourceIdentity,             identifier.QuotedIdentifier,             query?.CursorMode ?? "full");          using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);         if (reader is null)             yield break;          var columns = new List<string>(reader.FieldCount);         for (var i = 0; i < reader.FieldCount; i++)         {             var name = reader.GetName(i);             columns.Add(string.IsNullOrWhiteSpace(name) ? $"col_{i}" : name);         }          _columnDefinitionCache[table] = columns             .Select((column, index) => new SourceColumnDefinition(column, null, null, index))             .ToArray();         var schema = new SourceDataSchema(columns);          while (await reader.ReadAsync(ct))         {             var values = new object?[reader.FieldCount];             for (var i = 0; i < reader.FieldCount; i++)                 values[i] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);              yield return new SourceDataRow(schema, values);         }     }      internal static SqlServerSqlFragment BuildSelectSqlFromColumns(
        IReadOnlyList<string> columns,
        string quotedTable,
        SourceReadQuery? query)
    {
        var parameters = new List<(string Name, object Value)>();
        var select = BuildSelectList(query, parameters);
        if (query is null)
            return FullScan(select, quotedTable, columns, parameters);

        var mode = string.IsNullOrWhiteSpace(query.CursorMode)
            ? "id"
            : query.CursorMode.Trim().ToLowerInvariant();
        if (string.Equals(mode, "none", StringComparison.OrdinalIgnoreCase))
            return FullScan(select, quotedTable, columns, parameters);

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
                whereSql = $"{QuoteIdentifier(tsColumn)} > @p0";
                orderSqlClause = $" ORDER BY {QuoteIdentifier(tsColumn)}";
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
                                $"({QuoteIdentifier(tsColumn)} > @p0 OR ({QuoteIdentifier(tsColumn)} = @p1 AND {QuoteIdentifier(idColumn)} > @p2))";
                            orderSqlClause =
                                $" ORDER BY {QuoteIdentifier(tsColumn)}, {QuoteIdentifier(idColumn)}";
                            break;
                        }
                    }

                    parameters.Add(("@p0", effectiveTimestamp.Value));
                    whereSql = $"{QuoteIdentifier(tsColumn)} > @p0";
                    orderSqlClause = $" ORDER BY {QuoteIdentifier(tsColumn)}";
                    break;
                }

                if (!query.CursorId.HasValue || string.IsNullOrWhiteSpace(idColumn))
                    return FullScan(select, quotedTable, columns, parameters);

                parameters.Add(("@p0", query.CursorId.Value));
                whereSql = $"{QuoteIdentifier(idColumn)} > @p0";
                orderSqlClause = $" ORDER BY {QuoteIdentifier(idColumn)}";
                break;

            case "id_or_composite":
            case "id":
            default:
                if (!query.CursorId.HasValue || string.IsNullOrWhiteSpace(idColumn))
                    return FullScan(select, quotedTable, columns, parameters);

                parameters.Add(("@p0", query.CursorId.Value));
                whereSql = $"{QuoteIdentifier(idColumn)} > @p0";
                orderSqlClause = $" ORDER BY {QuoteIdentifier(idColumn)}";
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
            : $" ORDER BY {QuoteIdentifier(orderColumn)}";
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

    internal static string QuoteIdentifier(string identifier)     {         ArgumentException.ThrowIfNullOrWhiteSpace(identifier);         return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";     }      internal static bool TryGetQuotedTableIdentifier(string? tableName, out string quotedIdentifier, out string failureReason)     {         quotedIdentifier = string.Empty;         failureReason = string.Empty;         if (string.IsNullOrWhiteSpace(tableName))         {             failureReason = "table name is empty";             return false;         }          if (!TryParseIdentifierParts(tableName, out var parts, out failureReason))             return false;          if (parts.Count == 1)             parts = ["dbo", parts[0]];          if (parts.Count != 2)         {             failureReason = "table identifier must have one or two parts";             return false;         }          quotedIdentifier = $"{QuoteIdentifier(parts[0])}.{QuoteIdentifier(parts[1])}";         return true;     }      public async ValueTask DisposeAsync()     {         if (_disposed)             return;          _disposed = true;         _metadataLimiter.Dispose();         await _connection.DisposeAsync();     }      private static IReadOnlyList<string> FilterVisibleTables(IEnumerable<string> tables, bool includeTemporaryTables)     {         var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);         var filtered = new List<string>();         foreach (var table in tables)         {             if (string.IsNullOrWhiteSpace(table))                 continue;              if (!includeTemporaryTables && table.Contains(".[#", StringComparison.Ordinal))                 continue;              if (seen.Add(table))                 filtered.Add(table);         }          return filtered;     }      private static string? ResolveColumn(IReadOnlyList<string> columns, IReadOnlyList<string> normalizedAliases)     {         if (columns.Count == 0 || normalizedAliases.Count == 0)             return null;          var normalizedToSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);         for (var i = 0; i < columns.Count; i++)         {             var source = columns[i];             var normalized = AccessImportService.Normalize(source);             if (!string.IsNullOrWhiteSpace(normalized) && !normalizedToSource.ContainsKey(normalized))                 normalizedToSource[normalized] = source;         }          for (var i = 0; i < normalizedAliases.Count; i++)         {             var alias = AccessImportService.Normalize(normalizedAliases[i]);             if (!string.IsNullOrWhiteSpace(alias) && normalizedToSource.TryGetValue(alias, out var source))                 return source;         }          return null;     }      private async Task<SqlServerSqlFragment> BuildSelectSqlAsync(
        string table,
        string quotedTable,
        SourceReadQuery? query,
        CancellationToken ct)
    {
        var columns = await GetColumnsAsync(table, ct);
        return BuildSelectSqlFromColumns(columns, quotedTable, query);
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

    private SqlCommand CreateCommand(string sql)
        => new(sql, _connection)
        {
            CommandTimeout = _commandTimeoutSeconds,
            CommandType = CommandType.Text
        };

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

    private TableIdentifier ParseTableIdentifier(string table)     {         if (!TryGetQuotedTableIdentifier(table, out var quotedIdentifier, out var failureReason))             throw new ArgumentException($"Invalid SQL Server table '{table}': {failureReason}", nameof(table));          if (!TryParseIdentifierParts(table, out var parts, out failureReason))             throw new ArgumentException($"Invalid SQL Server table '{table}': {failureReason}", nameof(table));          if (parts.Count == 1)             parts = ["dbo", parts[0]];          return new TableIdentifier(parts[0], parts[1], quotedIdentifier);     }      private static bool TryParseIdentifierParts(string value, out List<string> parts, out string failureReason)     {         parts = [];         failureReason = string.Empty;         var span = value.AsSpan().Trim();         if (span.IsEmpty)         {             failureReason = "table name is empty";             return false;         }          var current = new System.Text.StringBuilder();         var index = 0;         while (index < span.Length)         {             while (index < span.Length && char.IsWhiteSpace(span[index]))                 index++;              if (index >= span.Length)                 break;              current.Clear();             if (span[index] == '[')             {                 index++;                 while (index < span.Length)                 {                     if (span[index] == ']')                     {                         if (index + 1 < span.Length && span[index + 1] == ']')                         {                             current.Append(']');                             index += 2;                             continue;                         }                          index++;                         break;                     }                      current.Append(span[index]);                     index++;                 }             }             else             {                 while (index < span.Length && span[index] != '.')                 {                     current.Append(span[index]);                     index++;                 }             }              var part = current.ToString().Trim();             if (string.IsNullOrWhiteSpace(part))             {                 failureReason = "identifier contains an empty part";                 return false;             }              if (part.IndexOf('\0') >= 0)             {                 failureReason = "identifier contains a null byte";                 return false;             }              parts.Add(part);             while (index < span.Length && char.IsWhiteSpace(span[index]))                 index++;              if (index >= span.Length)                 break;              if (span[index] != '.')             {                 failureReason = "identifier contains invalid separator characters";                 return false;             }              index++;         }          if (parts.Count is 1 or 2)             return true;          failureReason = "table identifier must have one or two parts";         return false;     }      private static string BuildSourceIdentity(string connectionString)     {         var builder = new SqlConnectionStringBuilder(connectionString);         var dataSource = string.IsNullOrWhiteSpace(builder.DataSource) ? "unknown-host" : builder.DataSource;         var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "default" : builder.InitialCatalog;         return $"sqlserver://{dataSource}/{database}";     }      private void LogFailure(string operation, Exception exception)
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

    private void ThrowIfDisposed()     {         if (_disposed)             throw new ObjectDisposedException(nameof(SqlServerSourceDataSession));     }      private sealed record TableIdentifier(string Schema, string ObjectName, string QuotedIdentifier); }


internal readonly record struct SqlServerSqlFragment(
    string CommandText,
    IReadOnlyList<(string Name, object Value)> Parameters);
