using System.Collections.ObjectModel;

namespace Api.Services.DataSources;

/// <summary>
/// Provider-neutral read-only source session.
/// Access remains available through <see cref="Access.IAccessDataReaderSession"/> and the Access adapter.
/// </summary>
public interface ISourceDataSession : IAsyncDisposable
{
    /// <summary>Stable provider id, e.g. <c>access</c>.</summary>
    string Provider { get; }

    /// <summary>Provider mode / dialect path, e.g. Access <c>windows</c> or <c>cli</c>.</summary>
    string Mode { get; }

    /// <summary>Opaque source identity (Access file path today).</summary>
    string SourceIdentity { get; }

    SourceCapabilities Capabilities { get; }

    Task TestConnectionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default);
    Task<SourceRowCountResult> TryGetRowCountAsync(string table, CancellationToken ct = default);
    IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, CancellationToken ct = default);
    IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, SourceReadQuery? query, CancellationToken ct = default);
}

/// <summary>
/// Explicit capability flags. Prefer these over provider-name or mode-string switches.
/// </summary>
public sealed record SourceCapabilities(
    bool SchemaDiscovery = true,
    bool ExactRowCount = true,
    bool PredicatePushdown = false,
    bool IdCursor = true,
    bool TimestampCursor = true,
    bool CompositeTimestampIdCursor = true,
    bool Cancellation = true,
    bool Cdc = false);

public sealed class SourceReadQuery
{
    public string CursorMode { get; init; } = "id";
    public DateTime? CursorTimestampUtc { get; init; }
    public long? CursorId { get; init; }
    public long? CursorTieBreakerId { get; init; }
    public int OverlapSeconds { get; init; }
    public int? MaxRows { get; init; }
    public IReadOnlyList<string> TimestampAliases { get; init; } = [];
    public IReadOnlyList<string> IdAliases { get; init; } = [];
}

public sealed record SourceRowCountResult(int Count, string Mode)
{
    public static SourceRowCountResult Exact(int count) => new(Math.Max(0, count), "exact");
    public static SourceRowCountResult Sampled(int count) => new(Math.Max(0, count), "sampled");
    public static SourceRowCountResult Unknown() => new(0, "unknown");

    public bool IsExact => string.Equals(Mode, "exact", StringComparison.OrdinalIgnoreCase);
}

public sealed class SourceDataRow
{
    private readonly SourceDataSchema _schema;
    private readonly object?[] _values;

    public SourceDataRow(SourceDataSchema schema, object?[] values)
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

public sealed class SourceDataSchema
{
    private readonly ReadOnlyCollection<string> _columns;
    private readonly Dictionary<string, int> _normalizedOrdinals;
    private readonly Func<string, string> _normalize;

    public SourceDataSchema(IEnumerable<string> columns, Func<string, string>? normalize = null)
    {
        _normalize = normalize ?? AccessImportService.Normalize;
        var list = columns.ToList();
        _columns = new ReadOnlyCollection<string>(list);
        _normalizedOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < list.Count; i++)
        {
            var normalized = _normalize(list[i]);
            if (!string.IsNullOrWhiteSpace(normalized))
                _normalizedOrdinals.TryAdd(normalized, i);
        }
    }

    public IReadOnlyList<string> Columns => _columns;

    public bool TryGetValue(string alias, object?[] values, out object? value)
    {
        value = null;
        var normalized = _normalize(alias);
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
