using Api.Services;
using System.Security.Cryptography;
using System.Text;
using PreviewModelRequest = Api.Models.SourceMappingPreviewRequest;
using PreviewModelFieldRequest = Api.Models.SourceMappingFieldRequest;
using PreviewResponseModel = Api.Models.SourceMappingPreviewResponse;
using PreviewIssueModel = Api.Models.SourceMappingPreviewIssue;
using PreviewFieldResultModel = Api.Models.SourceMappingPreviewFieldResult;
using PreviewMappedValueModel = Api.Models.SourceMappedValue;
using PreviewRowModel = Api.Models.SourceMappingPreviewRow;
using Api.Models;
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
        Api.Services.DataSources.SourceMappingPreviewRequest request,
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
        IReadOnlyList<Api.Services.DataSources.SourceMappingFieldRequest> requested,
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
        Api.Services.DataSources.SourceMappingPreviewRequest request,
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
    private const int MaxPreviewTake = 25;

    public static int BoundTake(int take)
        => take <= 0 ? 1 : Math.Min(take, MaxPreviewTake);

    public static PreviewResponseModel BuildPreview(
        string profileName,
        string provider,
        PreviewModelRequest request,
        IReadOnlyList<string> sourceColumns,
        IReadOnlyList<SourceDataRow> sampleRows,
        bool truncated)
    {
        var normalizedSourceColumns = BuildSourceColumnLookup(sourceColumns);
        var issues = new List<PreviewIssueModel>();
        var fieldMappings = ResolveFieldMappings(request.FieldMappings, normalizedSourceColumns, issues);

        AddKeyValidationIssues(request.ExternalKeyColumns, normalizedSourceColumns, issues);
        AddCursorValidationIssues(request.Cursor, normalizedSourceColumns, issues);

        var response = new PreviewResponseModel
        {
            ProfileName = profileName,
            Provider = provider,
            CanonicalEntity = request.CanonicalEntity.Trim(),
            SourceTable = request.SourceTable.Trim(),
            ExternalKeyColumns = request.ExternalKeyColumns
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .Select(column => column.Trim())
                .ToList(),
            Cursor = CloneCursor(request.Cursor),
            SchemaFingerprint = ComputeSchemaFingerprint(profileName, provider, request, sourceColumns),
            RequestedTake = request.Take,
            ReturnedRows = sampleRows.Count,
            Truncated = truncated,
            FieldMappings = fieldMappings,
            Issues = issues,
            Rows = BuildRows(sampleRows, fieldMappings)
        };

        return response;
    }

    public static string ComputeSchemaFingerprint(
        string profileName,
        string provider,
        PreviewModelRequest request,
        IReadOnlyList<string> sourceColumns)
    {
        var builder = new StringBuilder();
        builder
            .Append("profile=").Append(Normalize(profileName)).Append('\n')
            .Append("provider=").Append(Normalize(provider)).Append('\n')
            .Append("entity=").Append(Normalize(request.CanonicalEntity)).Append('\n')
            .Append("table=").Append(Normalize(request.SourceTable)).Append('\n')
            .Append("external-keys=");

        foreach (var key in request.ExternalKeyColumns.Where(column => !string.IsNullOrWhiteSpace(column)))
            builder.Append(Normalize(key)).Append('|');

        builder
            .Append('\n')
            .Append("cursor=").Append(SerializeCursor(request.Cursor)).Append('\n')
            .Append("columns=");

        foreach (var column in sourceColumns)
            builder.Append(Normalize(column)).Append('|');

        builder.Append('\n').Append("fields=");
        foreach (var field in request.FieldMappings.OrderBy(x => Normalize(x.TargetField), StringComparer.OrdinalIgnoreCase))
        {
            builder
                .Append(Normalize(field.TargetField)).Append('=');

            foreach (var alias in field.Aliases)
                builder.Append(Normalize(alias)).Append(',');

            builder.Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static List<PreviewIssueModel> AddCursorValidationIssues(
        SourceReadQuery? cursor,
        IReadOnlyDictionary<string, string> normalizedSourceColumns,
        List<PreviewIssueModel> issues)
    {
        if (cursor is null)
        {
            issues.Add(new PreviewIssueModel
            {
                Scope = "cursor",
                ReasonCode = "cursor_required",
                Message = "A cursor selection is required for mapping preview."
            });

            return issues;
        }

        var mode = Normalize(cursor.CursorMode);
        if (string.IsNullOrWhiteSpace(mode))
        {
            issues.Add(new PreviewIssueModel
            {
                Scope = "cursor",
                ReasonCode = "cursor_mode_required",
                Message = "Cursor mode is required."
            });
            return issues;
        }

        if (ModeEquals(mode, "timestamp") ||
            ModeEquals(mode, "timestamp_then_id"))
        {
            if (!HasAnyAlias(cursor.TimestampAliases, normalizedSourceColumns))
            {
                issues.Add(new PreviewIssueModel
                {
                    Scope = "cursor",
                    ReasonCode = "cursor_missing_timestamp_alias",
                    Message = "No timestamp cursor alias matched the source schema; preview will fall back to a full scan."
                });
            }
        }

        if (ModeEquals(mode, "id") ||
            ModeEquals(mode, "id_or_composite") ||
            ModeEquals(mode, "timestamp_then_id"))
        {
            if (!HasAnyAlias(cursor.IdAliases, normalizedSourceColumns))
            {
                issues.Add(new PreviewIssueModel
                {
                    Scope = "cursor",
                    ReasonCode = "cursor_missing_id_alias",
                    Message = "No ID cursor alias matched the source schema; preview will fall back to a full scan."
                });
            }
        }

        return issues;
    }

    private static void AddKeyValidationIssues(
        IReadOnlyList<string> externalKeyColumns,
        IReadOnlyDictionary<string, string> normalizedSourceColumns,
        List<PreviewIssueModel> issues)
    {
        if (externalKeyColumns.Count == 0)
        {
            issues.Add(new PreviewIssueModel
            {
                Scope = "external_key",
                ReasonCode = "external_key_required",
                Message = "At least one external key column is required."
            });
            return;
        }

        foreach (var key in externalKeyColumns)
        {
            var normalized = Normalize(key);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                issues.Add(new PreviewIssueModel
                {
                    Scope = "external_key",
                    Field = key,
                    ReasonCode = "external_key_invalid",
                    Message = "External key columns must not be blank."
                });
                continue;
            }

            if (!normalizedSourceColumns.ContainsKey(normalized))
            {
                issues.Add(new PreviewIssueModel
                {
                    Scope = "external_key",
                    Field = key,
                    ReasonCode = "external_key_missing_column",
                    Message = $"External key column '{key}' was not found in the source schema."
                });
            }
        }
    }

    private static List<PreviewFieldResultModel> ResolveFieldMappings(
        IReadOnlyList<PreviewModelFieldRequest> fieldMappings,
        IReadOnlyDictionary<string, string> normalizedSourceColumns,
        List<PreviewIssueModel> issues)
    {
        var results = new List<PreviewFieldResultModel>(fieldMappings.Count);
        var usedSourceColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in fieldMappings)
        {
            var targetField = mapping.TargetField?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetField))
            {
                issues.Add(new PreviewIssueModel
                {
                    Scope = "field",
                    ReasonCode = "target_field_required",
                    Message = "A target field name is required for each mapping."
                });

                results.Add(new PreviewFieldResultModel
                {
                    TargetField = targetField,
                    Aliases = mapping.Aliases.ToList(),
                    Status = "rejected",
                    ReasonCode = "target_field_required",
                    Message = "Target field is required."
                });
                continue;
            }

            var aliases = mapping.Aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(alias => alias.Trim())
                .ToList();

            if (aliases.Count == 0)
            {
                issues.Add(new PreviewIssueModel
                {
                    Scope = "field",
                    Field = targetField,
                    ReasonCode = "aliases_required",
                    Message = $"Target field '{targetField}' does not define any aliases."
                });

                results.Add(new PreviewFieldResultModel
                {
                    TargetField = targetField,
                    Aliases = aliases,
                    Status = "missing",
                    ReasonCode = "aliases_required",
                    Message = "No aliases were supplied."
                });
                continue;
            }

            var resolvedSourceColumn = ResolveAlias(aliases, normalizedSourceColumns, usedSourceColumns);
            if (resolvedSourceColumn is null)
            {
                issues.Add(new PreviewIssueModel
                {
                    Scope = "field",
                    Field = targetField,
                    ReasonCode = "source_column_not_found",
                    Message = $"No alias for target field '{targetField}' matched the source schema."
                });

                results.Add(new PreviewFieldResultModel
                {
                    TargetField = targetField,
                    Aliases = aliases,
                    Status = "missing",
                    ReasonCode = "source_column_not_found",
                    Message = "No source column matched the supplied aliases."
                });
                continue;
            }

            usedSourceColumns.Add(resolvedSourceColumn);
            results.Add(new PreviewFieldResultModel
            {
                TargetField = targetField,
                Aliases = aliases,
                SourceColumn = resolvedSourceColumn,
                Status = "matched"
            });
        }

        return results;
    }

    private static string? ResolveAlias(
        IReadOnlyList<string> aliases,
        IReadOnlyDictionary<string, string> normalizedSourceColumns,
        HashSet<string> usedSourceColumns)
    {
        foreach (var alias in aliases)
        {
            var normalized = Normalize(alias);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!normalizedSourceColumns.TryGetValue(normalized, out var sourceColumn))
                continue;

            if (usedSourceColumns.Contains(sourceColumn))
                continue;

            return sourceColumn;
        }

        return null;
    }

    private static bool HasAnyAlias(
        IReadOnlyList<string> aliases,
        IReadOnlyDictionary<string, string> normalizedSourceColumns)
    {
        foreach (var alias in aliases)
        {
            var normalized = Normalize(alias);
            if (!string.IsNullOrWhiteSpace(normalized) && normalizedSourceColumns.ContainsKey(normalized))
                return true;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, string> BuildSourceColumnLookup(IReadOnlyList<string> sourceColumns)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in sourceColumns)
        {
            var normalized = Normalize(column);
            if (!string.IsNullOrWhiteSpace(normalized))
                lookup.TryAdd(normalized, column);
        }

        return lookup;
    }

    private static bool ModeEquals(string normalizedMode, string expectedMode)
        => string.Equals(normalizedMode, Normalize(expectedMode), StringComparison.OrdinalIgnoreCase);

    private static List<PreviewRowModel> BuildRows(
        IReadOnlyList<SourceDataRow> sampleRows,
        IReadOnlyList<PreviewFieldResultModel> fieldMappings)
    {
        var rows = new List<PreviewRowModel>(sampleRows.Count);
        for (var i = 0; i < sampleRows.Count; i++)
        {
            var row = sampleRows[i];
            var values = new List<PreviewMappedValueModel>(fieldMappings.Count);
            foreach (var mapping in fieldMappings)
            {
                object? value = null;
                if (!string.IsNullOrWhiteSpace(mapping.SourceColumn) &&
                    row.TryGetValue(mapping.SourceColumn!, out var mappedValue))
                {
                    value = mappedValue;
                }

                values.Add(new PreviewMappedValueModel
                {
                    TargetField = mapping.TargetField,
                    Value = value
                });
            }

            rows.Add(new PreviewRowModel
            {
                RowIndex = i + 1,
                Values = values
            });
        }

        return rows;
    }

    private static string SerializeCursor(SourceReadQuery? cursor)
    {
        if (cursor is null)
            return string.Empty;

        var builder = new StringBuilder();
        builder
            .Append("mode=").Append(Normalize(cursor.CursorMode)).Append('|')
            .Append("cursorId=").Append(cursor.CursorId?.ToString() ?? string.Empty).Append('|')
            .Append("tieBreakerId=").Append(cursor.CursorTieBreakerId?.ToString() ?? string.Empty).Append('|')
            .Append("timestamp=").Append(cursor.CursorTimestampUtc?.ToString("O") ?? string.Empty).Append('|')
            .Append("overlap=").Append(cursor.OverlapSeconds).Append('|')
            .Append("timestampAliases=");

        foreach (var alias in cursor.TimestampAliases)
            builder.Append(Normalize(alias)).Append(',');

        builder.Append("|idAliases=");
        foreach (var alias in cursor.IdAliases)
            builder.Append(Normalize(alias)).Append(',');

        return builder.ToString();
    }

    private static SourceReadQuery? CloneCursor(SourceReadQuery? cursor)
    {
        if (cursor is null)
            return null;

        return new SourceReadQuery
        {
            CursorMode = cursor.CursorMode,
            CursorTimestampUtc = cursor.CursorTimestampUtc,
            CursorId = cursor.CursorId,
            CursorTieBreakerId = cursor.CursorTieBreakerId,
            OverlapSeconds = cursor.OverlapSeconds,
            TimestampAliases = cursor.TimestampAliases.ToArray(),
            IdAliases = cursor.IdAliases.ToArray()
        };
    }

    private static string Normalize(string? value)
        => AccessImportService.Normalize(value);
}
