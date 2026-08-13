using Api.Services;
using Api.Services.DataSources;

public sealed class SourceMappingPreviewService
{
    public const int DefaultMaxRows = 25;
    public const int AbsoluteMaxRows = 50;

    private readonly NamedSourceDiscoveryService _discovery;
    private readonly ILogger<SourceMappingPreviewService> _logger;

    public SourceMappingPreviewService(
        NamedSourceDiscoveryService discovery,
        ILogger<SourceMappingPreviewService> logger)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SourceMappingPreviewDto> PreviewAsync(
        string sourceName,
        SourceMappingPreviewRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanonicalSourceEntities.TryGet(request.Entity, out var entity))
            throw new ArgumentException("Entity is not supported for mapping preview.", nameof(request));

        if (!SqlServerIdentifier.TryQuoteTable(request.Table, out _, out var tableFailure))
            throw new ArgumentException(tableFailure, nameof(request));

        await using var session = _discovery.OpenConfigured(sourceName);
        var tables = await session.GetTablesAsync(includeTemporaryTables: false, ct);
        if (!tables.Contains(request.Table, StringComparer.OrdinalIgnoreCase)
            && !tables.Any(table => table.EndsWith("." + request.Table, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Selected table was not found on the source.", nameof(request));
        }

        var columns = await session.GetColumnsAsync(request.Table, ct);
        var fingerprint = SourceSchemaFingerprint.Compute(session.Provider, request.Table, columns);
        var fieldResults = ValidateFields(entity, request.Fields, columns);
        var externalKey = ValidateSelection(
            request.ExternalKeyColumn,
            columns,
            missingReason: "key_column_missing",
            emptyReason: "key_column_required");
        var cursor = ValidateCursor(request, columns);

        var warnings = new List<string>();
        if (fieldResults.Any(field => field.Status != "ok") || externalKey.Status != "ok" || cursor.Status != "ok")
            warnings.Add("Mapping has rejected fields; preview includes only accepted target columns.");

        var accepted = fieldResults
            .Where(field => field.Status == "ok" && !string.IsNullOrWhiteSpace(field.ResolvedSource))
            .ToArray();

        var maxRows = request.MaxRows <= 0 ? DefaultMaxRows : Math.Clamp(request.MaxRows, 1, AbsoluteMaxRows);
        var preview = new List<IReadOnlyDictionary<string, object?>>();
        var rejectedRows = 0;
        if (accepted.Length > 0)
        {
            var query = new SourceReadQuery
            {
                CursorMode = "none",
                MaxRows = Math.Clamp(maxRows * 2, 1, AbsoluteMaxRows),
                IdAliases = string.IsNullOrWhiteSpace(request.CursorIdColumn) ? ["id"] : [request.CursorIdColumn]
            };

            await foreach (var row in session.ReadRowsAsync(request.Table, query, ct))
            {
                if (preview.Count >= maxRows)
                    break;

                if (!TryProjectRow(row, accepted, externalKey, out var projected))
                {
                    rejectedRows++;
                    continue;
                }

                preview.Add(projected);
            }
        }

        _logger.LogInformation(
            "Data source mapping preview source={Source} table={Table} entity={Entity} fingerprint={Fingerprint} previewRows={PreviewRows} rejectedRows={RejectedRows} identity={Identity}",
            sourceName,
            request.Table,
            entity.Key,
            fingerprint,
            preview.Count,
            rejectedRows,
            session.SourceIdentity);

        return new SourceMappingPreviewDto(
            sourceName,
            request.Table,
            entity.Key,
            fingerprint,
            session.SourceIdentity,
            externalKey,
            cursor,
            fieldResults,
            preview,
            preview.Count,
            rejectedRows,
            warnings);
    }

    internal static IReadOnlyList<SourceMappingFieldResultDto> ValidateFields(
        CanonicalSourceEntities.Entity entity,
        IReadOnlyList<SourceMappingFieldRequest> requested,
        IReadOnlyList<string> columns)
    {
        var results = new List<SourceMappingFieldResultDto>();
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requestedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in requested)
        {
            var target = field.Target?.Trim() ?? string.Empty;
            var source = field.Source?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target))
            {
                results.Add(new SourceMappingFieldResultDto(target, source, null, "rejected", "target_required"));
                continue;
            }

            requestedTargets.Add(target);
            if (!seenTargets.Add(target))
            {
                results.Add(new SourceMappingFieldResultDto(target, source, null, "rejected", "duplicate_target"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                results.Add(new SourceMappingFieldResultDto(
                    target,
                    source,
                    null,
                    "rejected",
                    "source_column_required",
                    SuggestedAliases(entity, target)));
                continue;
            }

            var resolved = ResolveColumn(columns, source);
            if (resolved is null)
            {
                results.Add(new SourceMappingFieldResultDto(
                    target,
                    source,
                    null,
                    "rejected",
                    "source_column_missing",
                    SuggestedAliases(entity, target)));
                continue;
            }

            if (!seenSources.Add(resolved))
            {
                results.Add(new SourceMappingFieldResultDto(target, source, resolved, "rejected", "duplicate_source"));
                continue;
            }

            results.Add(new SourceMappingFieldResultDto(target, source, resolved, "ok", null));
        }

        foreach (var required in entity.RequiredTargets)
        {
            if (requestedTargets.Contains(required))
                continue;

            results.Add(new SourceMappingFieldResultDto(
                required,
                null,
                null,
                "rejected",
                "target_required_unmapped",
                SuggestedAliases(entity, required)));
        }

        return results;
    }

    internal static SourceMappingSelectionDto ValidateSelection(
        string? requested,
        IReadOnlyList<string> columns,
        string missingReason,
        string emptyReason)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return new SourceMappingSelectionDto("rejected", null, null, emptyReason);

        var resolved = ResolveColumn(columns, requested);
        return resolved is null
            ? new SourceMappingSelectionDto("rejected", requested.Trim(), null, missingReason)
            : new SourceMappingSelectionDto("ok", resolved, null, null);
    }

    internal static SourceMappingSelectionDto ValidateCursor(
        SourceMappingPreviewRequest request,
        IReadOnlyList<string> columns)
    {
        var mode = string.IsNullOrWhiteSpace(request.CursorMode)
            ? "id"
            : request.CursorMode.Trim().ToLowerInvariant();

        if (mode is not ("none" or "id" or "timestamp" or "timestamp_then_id"))
            return new SourceMappingSelectionDto("rejected", null, mode, "unsupported_cursor_mode");

        if (mode == "none")
            return new SourceMappingSelectionDto("ok", null, mode, null);

        if (mode is "id" or "timestamp_then_id")
        {
            var id = ValidateSelection(
                request.CursorIdColumn ?? request.ExternalKeyColumn,
                columns,
                missingReason: "cursor_column_missing",
                emptyReason: "cursor_column_required");
            if (id.Status != "ok")
                return id with { Mode = mode };
            if (mode == "id")
                return id with { Mode = mode };
        }

        if (mode is "timestamp" or "timestamp_then_id")
        {
            var timestamp = ValidateSelection(
                request.CursorTimestampColumn,
                columns,
                missingReason: "cursor_column_missing",
                emptyReason: "cursor_column_required");
            if (timestamp.Status != "ok")
                return timestamp with { Mode = mode };
            if (mode == "timestamp")
                return timestamp with { Mode = mode };

            var idColumn = ResolveColumn(columns, request.CursorIdColumn ?? request.ExternalKeyColumn);
            return new SourceMappingSelectionDto("ok", $"{timestamp.Column}+{idColumn}", mode, null);
        }

        return new SourceMappingSelectionDto("ok", null, mode, null);
    }

    internal static string? ResolveColumn(IReadOnlyList<string> columns, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested) || columns.Count == 0)
            return null;

        var wanted = AccessImportService.Normalize(requested);
        foreach (var column in columns)
        {
            if (string.Equals(column, requested, StringComparison.OrdinalIgnoreCase)
                || AccessImportService.Normalize(column) == wanted)
            {
                return column;
            }
        }

        return null;
    }

    private static IReadOnlyList<string>? SuggestedAliases(CanonicalSourceEntities.Entity entity, string target)
        => entity.Aliases.TryGetValue(target, out var aliases) ? aliases : null;

    private static bool TryProjectRow(
        SourceDataRow row,
        IReadOnlyList<SourceMappingFieldResultDto> accepted,
        SourceMappingSelectionDto externalKey,
        out IReadOnlyDictionary<string, object?> projected)
    {
        if (externalKey.Status == "ok"
            && !string.IsNullOrWhiteSpace(externalKey.Column)
            && (!row.TryGetValue(externalKey.Column, out var key) || key is null or ""))
        {
            projected = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            return false;
        }

        var snapshot = new Dictionary<string, object?>(accepted.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var field in accepted)
        {
            row.TryGetValue(field.ResolvedSource!, out var value);
            snapshot[field.Target] = SanitizePreviewValue(value);
        }

        projected = snapshot;
        return true;
    }

    private static object? SanitizePreviewValue(object? value)
    {
        if (value is string text && text.Length > 200)
            return text[..200];

        return value;
    }
}
