using System.Collections.ObjectModel;

namespace Api.Services.Access;

public interface IAccessDataReaderSession : IAsyncDisposable
{
    string Mode { get; }
    string SourceFilePath { get; }
    bool SupportsPredicatePushdown { get; }

    Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default);
    Task<AccessRowCountResult> TryGetExactRowCountAsync(string table, CancellationToken ct = default);
    IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, CancellationToken ct = default);
    IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, AccessReadQuery? query, CancellationToken ct = default);
}

public sealed class AccessReadQuery
{
    public string CursorMode { get; init; } = "id";
    public DateTime? CursorTimestampUtc { get; init; }
    public long? CursorId { get; init; }
    public long? CursorTieBreakerId { get; init; }
    public int OverlapSeconds { get; init; }
    public IReadOnlyList<string> TimestampAliases { get; init; } = [];
    public IReadOnlyList<string> IdAliases { get; init; } = [];
}

public sealed record AccessRowCountResult(int Count, string Mode)
{
    public static AccessRowCountResult Exact(int count) => new(Math.Max(0, count), "exact");
    public static AccessRowCountResult Sampled(int count) => new(Math.Max(0, count), "sampled");
    public static AccessRowCountResult Unknown() => new(0, "unknown");

    public bool IsExact => string.Equals(Mode, "exact", StringComparison.OrdinalIgnoreCase);
}

public sealed class AccessDataRow
{
    private readonly AccessDataSchema _schema;
    private readonly object?[] _values;

    public AccessDataRow(AccessDataSchema schema, object?[] values)
    {
        _schema = schema;
        _values = values;
    }

    public IReadOnlyList<string> Columns => _schema.Columns;

    public bool TryGetValue(string alias, out object? value)
        => _schema.TryGetValue(alias, _values, out value);

    public bool TryGetValueNormalized(string normalizedAlias, out object? value)
        => _schema.TryGetValueNormalizedAlias(normalizedAlias, _values, out value);

    public IReadOnlyDictionary<string, object?> ToDictionary()
    {
        var snapshot = new Dictionary<string, object?>(_schema.Columns.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _schema.Columns.Count; i++)
        {
            snapshot[_schema.Columns[i]] = i < _values.Length ? _values[i] : null;
        }

        return snapshot;
    }
}

public sealed class AccessDataSchema
{
    private readonly ReadOnlyCollection<string> _columns;
    private readonly Dictionary<string, int> _normalizedOrdinals;

    public AccessDataSchema(IEnumerable<string> columns)
    {
        var list = columns.ToList();
        _columns = new ReadOnlyCollection<string>(list);
        _normalizedOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < list.Count; i++)
        {
            var normalized = AccessImportService.Normalize(list[i]);
            if (!string.IsNullOrWhiteSpace(normalized))
                _normalizedOrdinals.TryAdd(normalized, i);
        }
    }

    public IReadOnlyList<string> Columns => _columns;

    public bool TryGetValue(string alias, object?[] values, out object? value)
    {
        value = null;
        var normalized = AccessImportService.Normalize(alias);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return TryGetValueNormalizedAlias(normalized, values, out value);
    }

    public bool TryGetValueNormalizedAlias(string normalizedAlias, object?[] values, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(normalizedAlias))
            return false;

        if (!_normalizedOrdinals.TryGetValue(normalizedAlias, out var ordinal))
            return false;

        if (ordinal < 0 || ordinal >= values.Length)
            return false;

        value = values[ordinal];
        return true;
    }
}
