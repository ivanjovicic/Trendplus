using System.Security.Cryptography;
using System.Text;
using Api.Models;
using Api.Services;

namespace Api.Services.DataSources;

public static class SourceMappingPreviewService
{
    private const int MaxPreviewTake = 25;

    public static int BoundTake(int take)
        => take <= 0 ? 1 : Math.Min(take, MaxPreviewTake);

    public static SourceMappingPreviewResponse BuildPreview(
        string profileName,
        string provider,
        SourceMappingPreviewRequest request,
        IReadOnlyList<string> sourceColumns,
        IReadOnlyList<SourceDataRow> sampleRows,
        bool truncated)
    {
        var normalizedSourceColumns = BuildSourceColumnLookup(sourceColumns);
        var issues = new List<SourceMappingPreviewIssue>();
        var fieldMappings = ResolveFieldMappings(request.FieldMappings, normalizedSourceColumns, issues);

        AddKeyValidationIssues(request.ExternalKeyColumns, normalizedSourceColumns, issues);
        AddCursorValidationIssues(request.Cursor, normalizedSourceColumns, issues);

        var response = new SourceMappingPreviewResponse
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
        SourceMappingPreviewRequest request,
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

    private static List<SourceMappingPreviewIssue> AddCursorValidationIssues(
        SourceReadQuery? cursor,
        IReadOnlyDictionary<string, string> normalizedSourceColumns,
        List<SourceMappingPreviewIssue> issues)
    {
        if (cursor is null)
        {
            issues.Add(new SourceMappingPreviewIssue
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
            issues.Add(new SourceMappingPreviewIssue
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
                issues.Add(new SourceMappingPreviewIssue
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
                issues.Add(new SourceMappingPreviewIssue
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
        List<SourceMappingPreviewIssue> issues)
    {
        if (externalKeyColumns.Count == 0)
        {
            issues.Add(new SourceMappingPreviewIssue
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
                issues.Add(new SourceMappingPreviewIssue
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
                issues.Add(new SourceMappingPreviewIssue
                {
                    Scope = "external_key",
                    Field = key,
                    ReasonCode = "external_key_missing_column",
                    Message = $"External key column '{key}' was not found in the source schema."
                });
            }
        }
    }

    private static List<SourceMappingPreviewFieldResult> ResolveFieldMappings(
        IReadOnlyList<SourceMappingFieldRequest> fieldMappings,
        IReadOnlyDictionary<string, string> normalizedSourceColumns,
        List<SourceMappingPreviewIssue> issues)
    {
        var results = new List<SourceMappingPreviewFieldResult>(fieldMappings.Count);
        var usedSourceColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in fieldMappings)
        {
            var targetField = mapping.TargetField?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetField))
            {
                issues.Add(new SourceMappingPreviewIssue
                {
                    Scope = "field",
                    ReasonCode = "target_field_required",
                    Message = "A target field name is required for each mapping."
                });

                results.Add(new SourceMappingPreviewFieldResult
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
                issues.Add(new SourceMappingPreviewIssue
                {
                    Scope = "field",
                    Field = targetField,
                    ReasonCode = "aliases_required",
                    Message = $"Target field '{targetField}' does not define any aliases."
                });

                results.Add(new SourceMappingPreviewFieldResult
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
                issues.Add(new SourceMappingPreviewIssue
                {
                    Scope = "field",
                    Field = targetField,
                    ReasonCode = "source_column_not_found",
                    Message = $"No alias for target field '{targetField}' matched the source schema."
                });

                results.Add(new SourceMappingPreviewFieldResult
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
            results.Add(new SourceMappingPreviewFieldResult
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

    private static List<SourceMappingPreviewRow> BuildRows(
        IReadOnlyList<SourceDataRow> sampleRows,
        IReadOnlyList<SourceMappingPreviewFieldResult> fieldMappings)
    {
        var rows = new List<SourceMappingPreviewRow>(sampleRows.Count);
        for (var i = 0; i < sampleRows.Count; i++)
        {
            var row = sampleRows[i];
            var values = new List<SourceMappedValue>(fieldMappings.Count);
            foreach (var mapping in fieldMappings)
            {
                object? value = null;
                if (!string.IsNullOrWhiteSpace(mapping.SourceColumn) &&
                    row.TryGetValue(mapping.SourceColumn!, out var mappedValue))
                {
                    value = mappedValue;
                }

                values.Add(new SourceMappedValue
                {
                    TargetField = mapping.TargetField,
                    Value = value
                });
            }

            rows.Add(new SourceMappingPreviewRow
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
