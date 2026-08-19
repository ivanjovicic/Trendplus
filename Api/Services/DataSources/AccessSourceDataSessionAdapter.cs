using System.Runtime.CompilerServices;
using Api.Services.Access;

namespace Api.Services.DataSources;

/// <summary>
/// Compatibility adapter: Access Windows/CLI sessions satisfy <see cref="ISourceDataSession"/>
/// without changing Access ODBC/CLI internals or existing import consumers.
/// </summary>
public sealed class AccessSourceDataSessionAdapter : ISourceDataSession
{
    private readonly IAccessDataReaderSession _inner;

    public AccessSourceDataSessionAdapter(IAccessDataReaderSession inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>Underlying Access session for gradual consumer migration.</summary>
    public IAccessDataReaderSession AccessSession => _inner;

    public string Provider => "access";

    public string Mode => _inner.Mode;

    public string SourceIdentity => _inner.SourceFilePath;

    public SourceCapabilities Capabilities => new(
        SchemaDiscovery: true,
        ExactRowCount: true,
        PredicatePushdown: _inner.SupportsPredicatePushdown,
        IdCursor: true,
        TimestampCursor: true,
        CompositeTimestampIdCursor: true,
        Cancellation: true,
        Cdc: false);

    public Task TestConnectionAsync(CancellationToken ct = default)
        => _inner.GetTablesAsync(includeTemporaryTables: false, ct);

    public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
        => _inner.GetTablesAsync(includeTemporaryTables, ct);

    public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
        => _inner.GetColumnsAsync(table, ct);

    public async Task<SourceRowCountResult> TryGetRowCountAsync(string table, CancellationToken ct = default)
    {
        var access = await _inner.TryGetExactRowCountAsync(table, ct);
        return MapRowCount(access);
    }

    public IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
        => ReadRowsAsync(table, query: null, ct);

    public async IAsyncEnumerable<SourceDataRow> ReadRowsAsync(
        string table,
        SourceReadQuery? query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var accessQuery = query is null ? null : MapQuery(query);
        await foreach (var row in _inner.ReadRowsAsync(table, accessQuery, ct))
        {
            yield return MapRow(row);
        }
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    public static SourceReadQuery ToSourceQuery(AccessReadQuery query)
        => new()
        {
            CursorMode = query.CursorMode,
            CursorTimestampUtc = query.CursorTimestampUtc,
            CursorId = query.CursorId,
            CursorTieBreakerId = query.CursorTieBreakerId,
            OverlapSeconds = query.OverlapSeconds,
            TimestampAliases = query.TimestampAliases,
            IdAliases = query.IdAliases
        };

    public static AccessReadQuery ToAccessQuery(SourceReadQuery query)
        => MapQuery(query);

    public static SourceRowCountResult MapRowCount(AccessRowCountResult result)
        => new(result.Count, result.Mode);

    private static AccessReadQuery MapQuery(SourceReadQuery query)
        => new()
        {
            CursorMode = query.CursorMode,
            CursorTimestampUtc = query.CursorTimestampUtc,
            CursorId = query.CursorId,
            CursorTieBreakerId = query.CursorTieBreakerId,
            OverlapSeconds = query.OverlapSeconds,
            TimestampAliases = query.TimestampAliases,
            IdAliases = query.IdAliases
        };

    private static SourceDataRow MapRow(AccessDataRow row)
    {
        var schema = new SourceDataSchema(row.Columns);
        var snapshot = row.ToDictionary();
        var values = new object?[row.Columns.Count];
        for (var i = 0; i < row.Columns.Count; i++)
        {
            values[i] = snapshot.TryGetValue(row.Columns[i], out var value) ? value : null;
        }

        return new SourceDataRow(schema, values);
    }
}
