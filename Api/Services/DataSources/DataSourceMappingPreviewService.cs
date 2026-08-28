using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Api.Config;
using Api.Models;
using Microsoft.Extensions.Options;

namespace Api.Services.DataSources;

public interface IDataSourceMappingPreviewService
{
    Task<DataSourceMappingPreviewResponse> PreviewAsync(
        string profileName,
        DataSourceMappingPreviewRequest request,
        CancellationToken ct = default);
}

public sealed class DataSourceMappingPreviewService : IDataSourceMappingPreviewService
{
    private static readonly HashSet<string> AllowedTransforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "trim",
        "upper",
        "lower",
        "empty_to_null"
    };

    private static readonly IReadOnlyDictionary<string, CanonicalEntitySpec> EntitySpecs =
        BuildEntitySpecs();

    private readonly IDataSourceProfileCatalog _catalog;
    private readonly ISourceDataSessionFactory _sessionFactory;
    private readonly DataSourceOptions _options;

    public DataSourceMappingPreviewService(
        IDataSourceProfileCatalog catalog,
        ISourceDataSessionFactory sessionFactory,
        IOptions<DataSourceOptions> options)
    {
        _catalog = catalog;
        _sessionFactory = sessionFactory;
        _options = options.Value;
    }

    public async Task<DataSourceMappingPreviewResponse> PreviewAsync(
        string profileName,
        DataSourceMappingPreviewRequest request,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentNullException.ThrowIfNull(request);

        var profile = GetProfile(profileName);
        var entity = GetEntitySpec(request.CanonicalEntity);
        var table = request.Table?.Trim();
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("Table identifier is required.", nameof(request));

        var sampleSize = ResolveSampleSize(request.SampleSize, _options.PreviewSampleLimit);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(GetPreviewTimeoutSeconds()));

        try
        {
            await using var session = _sessionFactory.Create(profile);

            var columnDefinitions = await session.GetColumnDefinitionsAsync(table, timeoutCts.Token);
            var response = new DataSourceMappingPreviewResponse
            {
                ProfileName = profile.Name,
                Provider = profile.Provider,
                Mode = profile.Mode,
                CanonicalEntity = entity.Name,
                Table = table,
                CanPreview = true,
                CanSync = true,
                SampleSize = sampleSize,
                Columns = columnDefinitions
                    .OrderBy(column => column.Ordinal)
                    .Select(column => new DataSourcePreviewColumn
                    {
                        Name = column.Name,
                        NormalizedName = Normalize(column.Name),
                        SourceType = NormalizeSourceType(column.SourceType),
                        IsNullable = column.IsNullable,
                        Ordinal = column.Ordinal
                    })
                    .ToList()
            };

            var rowCount = await session.TryGetRowCountAsync(table, timeoutCts.Token);
            response.RowCount = rowCount.Count;
            response.RowCountMode = rowCount.Mode;
            response.SchemaFingerprint = ComputeSchemaFingerprint(columnDefinitions);

            var columnsByName = columnDefinitions
                .GroupBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            AddDuplicateAliasWarnings(columnDefinitions, response.Warnings);

            var externalKeyColumns = ResolveExternalKeyColumns(request.ExternalKeyColumns, columnsByName, response.Warnings);
            response.ExternalKeyColumns = externalKeyColumns.ToList();
            if (externalKeyColumns.Count == 0)
            {
                response.CanSync = false;
                response.Warnings.Add("At least one explicit external key column is required. Row number fallback is not allowed.");
            }

            response.Cursor = BuildCursorPreview(request.Cursor, columnsByName);
            if (!string.Equals(response.Cursor.Status, "valid", StringComparison.OrdinalIgnoreCase))
                response.CanSync = false;

            var fieldMappings = BuildFieldMappings(entity, request.ColumnMappings, columnsByName);
            response.FieldMappings = fieldMappings;
            if (fieldMappings.Any(mapping => IsMappingBlocking(mapping.Status)))
                response.CanSync = false;

            var mappedFields = fieldMappings
                .Where(mapping => !string.IsNullOrWhiteSpace(mapping.SourceColumn)
                    && !string.Equals(mapping.Status, "invalid_target", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(mapping => mapping.TargetField, StringComparer.OrdinalIgnoreCase);

            var rows = new List<DataSourcePreviewRow>();
            var duplicateRowsByKey = new Dictionary<string, List<DataSourcePreviewRow>>(StringComparer.Ordinal);
            var rowIndex = 0;

            await foreach (var row in session.ReadRowsAsync(table, timeoutCts.Token))
            {
                rowIndex++;
                if (rows.Count >= sampleSize)
                    break;

                var previewRow = BuildPreviewRow(
                    row,
                    rowIndex,
                    entity,
                    mappedFields,
                    externalKeyColumns,
                    response.Cursor);
                rows.Add(previewRow);

                if (!string.IsNullOrWhiteSpace(previewRow.ExternalKey))
                {
                    if (!duplicateRowsByKey.TryGetValue(previewRow.ExternalKey, out var bucket))
                    {
                        bucket = [];
                        duplicateRowsByKey[previewRow.ExternalKey] = bucket;
                    }

                    bucket.Add(previewRow);
                }
            }

            response.PreviewedRows = rows.Count;
            response.PreviewRows = rows;
            ApplyDuplicateExternalKeyValidation(duplicateRowsByKey, response);
            response.CanSync &= rows.All(row => string.Equals(row.Status, "accepted", StringComparison.OrdinalIgnoreCase));
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Mapping preview timed out for data source profile '{profile.Name}'.");
        }
    }

    private static DataSourcePreviewRow BuildPreviewRow(
        SourceDataRow row,
        int rowIndex,
        CanonicalEntitySpec entity,
        IReadOnlyDictionary<string, DataSourcePreviewFieldMapping> mappedFields,
        IReadOnlyList<string> externalKeyColumns,
        DataSourcePreviewCursor cursor)
    {
        var snapshot = new Dictionary<string, object?>(row.ToDictionary(), StringComparer.OrdinalIgnoreCase);
        var previewRow = new DataSourcePreviewRow
        {
            RowIndex = rowIndex,
            SourceSnapshot = snapshot
        };

        previewRow.ExternalKey = BuildExternalKey(snapshot, externalKeyColumns, previewRow.RejectionReasons);

        foreach (var field in entity.Fields)
        {
            mappedFields.TryGetValue(field.TargetField, out var mapping);
            previewRow.Fields.Add(BuildFieldValue(snapshot, field, mapping));
        }

        ApplyCursorValidation(snapshot, cursor, previewRow.RejectionReasons);

        if (previewRow.Fields.Any(field => !string.Equals(field.Status, "accepted", StringComparison.OrdinalIgnoreCase)))
        {
            previewRow.RejectionReasons.AddRange(previewRow.Fields
                .Where(field => field.RejectionReasons.Count > 0)
                .SelectMany(field => field.RejectionReasons)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        previewRow.Status = previewRow.RejectionReasons.Count == 0 ? "accepted" : "rejected";
        return previewRow;
    }

    private static DataSourcePreviewFieldValue BuildFieldValue(
        IReadOnlyDictionary<string, object?> snapshot,
        CanonicalFieldSpec field,
        DataSourcePreviewFieldMapping? mapping)
    {
        var result = new DataSourcePreviewFieldValue
        {
            TargetField = field.TargetField,
            ValueType = field.ValueType
        };

        if (mapping is null || string.IsNullOrWhiteSpace(mapping.SourceColumn))
        {
            result.SourceColumn = mapping?.SourceColumn;
            result.Status = field.Required ? "missing" : "skipped";
            if (field.Required)
                result.RejectionReasons.Add($"Required field '{field.TargetField}' is not mapped.");
            return result;
        }

        result.SourceColumn = mapping.SourceColumn;
        if (mapping.ValidationErrors.Count > 0 || IsMappingBlocking(mapping.Status))
        {
            result.Status = "unavailable";
            result.RejectionReasons.AddRange(mapping.ValidationErrors);
            return result;
        }

        if (!snapshot.TryGetValue(mapping.SourceColumn, out var rawValue))
        {
            result.Status = "missing";
            result.RejectionReasons.Add($"Source column '{mapping.SourceColumn}' is missing from the row.");
            return result;
        }

        result.RawValue = rawValue;
        if (!TryParseFieldValue(rawValue, field.ValueType, mapping.Transforms, out var parsedValue, out var failureReason))
        {
            result.Status = "rejected";
            result.RejectionReasons.Add(failureReason ?? $"Field '{field.TargetField}' could not be parsed.");
            return result;
        }

        result.ParsedValue = parsedValue;
        if (parsedValue is null && field.Required)
        {
            result.Status = "missing";
            result.RejectionReasons.Add($"Required field '{field.TargetField}' is empty.");
            return result;
        }

        result.Status = "accepted";
        return result;
    }

    private static void ApplyCursorValidation(
        IReadOnlyDictionary<string, object?> snapshot,
        DataSourcePreviewCursor cursor,
        List<string> rejectionReasons)
    {
        if (!string.Equals(cursor.Status, "valid", StringComparison.OrdinalIgnoreCase))
            return;

        switch (Normalize(cursor.Mode))
        {
            case "id":
                ValidateCursorValue(snapshot, cursor.IdColumn, "integer", "Cursor ID", rejectionReasons);
                break;
            case "timestamp":
                ValidateCursorValue(snapshot, cursor.TimestampColumn, "datetime", "Cursor timestamp", rejectionReasons);
                break;
            case "timestampthenid":
                ValidateCursorValue(snapshot, cursor.TimestampColumn, "datetime", "Cursor timestamp", rejectionReasons);
                ValidateCursorValue(snapshot, cursor.TieBreakerColumn ?? cursor.IdColumn, "integer", "Cursor tie-breaker", rejectionReasons);
                break;
        }
    }

    private static void ValidateCursorValue(
        IReadOnlyDictionary<string, object?> snapshot,
        string? column,
        string valueType,
        string label,
        List<string> rejectionReasons)
    {
        if (string.IsNullOrWhiteSpace(column))
            return;

        if (!snapshot.TryGetValue(column, out var rawValue))
        {
            rejectionReasons.Add($"{label} column '{column}' is missing from the row.");
            return;
        }

        if (rawValue is null or DBNull)
        {
            rejectionReasons.Add($"{label} column '{column}' is null.");
            return;
        }

        if (!TryParseFieldValue(rawValue, valueType, [], out _, out var failureReason))
            rejectionReasons.Add(failureReason ?? $"{label} column '{column}' is invalid.");
    }

    private static void ApplyDuplicateExternalKeyValidation(
        IReadOnlyDictionary<string, List<DataSourcePreviewRow>> rowsByKey,
        DataSourceMappingPreviewResponse response)
    {
        foreach (var pair in rowsByKey.Where(pair => pair.Value.Count > 1))
        {
            response.CanSync = false;
            response.Warnings.Add($"Preview sample contains duplicate external key '{pair.Key}'.");

            foreach (var row in pair.Value)
            {
                row.RejectionReasons.Add($"Duplicate external key '{pair.Key}' within preview sample.");
                row.Status = "rejected";
            }
        }
    }

    private static IReadOnlyList<string> ResolveExternalKeyColumns(
        IReadOnlyList<string>? requestedColumns,
        IReadOnlyDictionary<string, SourceColumnDefinition> columnsByName,
        List<string> warnings)
    {
        if (requestedColumns is null || requestedColumns.Count == 0)
            return [];

        var resolved = new List<string>();
        foreach (var requestedColumn in requestedColumns)
        {
            if (string.IsNullOrWhiteSpace(requestedColumn))
                continue;

            if (columnsByName.TryGetValue(requestedColumn.Trim(), out var column))
            {
                    if (!resolved.Any(existing => string.Equals(existing, column.Name, StringComparison.OrdinalIgnoreCase)))
                        resolved.Add(column.Name);
                continue;
            }

            warnings.Add($"External key column '{requestedColumn}' does not exist in source table '{string.Join(", ", columnsByName.Keys.Take(3))}{(columnsByName.Count > 3 ? ", ..." : string.Empty)}'.");
        }

        return resolved;
    }

    private static DataSourcePreviewCursor BuildCursorPreview(
        DataSourceCursorSelection? selection,
        IReadOnlyDictionary<string, SourceColumnDefinition> columnsByName)
    {
        selection ??= new DataSourceCursorSelection();
        var cursor = new DataSourcePreviewCursor
        {
            Mode = NormalizeCursorMode(selection.Mode)
        };

        cursor.IdColumn = ResolveColumn(selection.IdColumn, columnsByName);
        cursor.TimestampColumn = ResolveColumn(selection.TimestampColumn, columnsByName);
        cursor.TieBreakerColumn = ResolveColumn(selection.TieBreakerColumn, columnsByName);

        switch (Normalize(cursor.Mode))
        {
            case "none":
                return cursor;
            case "id":
                RequireColumn(cursor.IdColumn, "Cursor mode 'id' requires an explicit id column.", cursor.ValidationErrors);
                ValidateColumnType(cursor.IdColumn, columnsByName, IsIntegerType, "Cursor id column must be integer-like.", cursor.ValidationErrors);
                break;
            case "timestamp":
                RequireColumn(cursor.TimestampColumn, "Cursor mode 'timestamp' requires an explicit timestamp column.", cursor.ValidationErrors);
                ValidateColumnType(cursor.TimestampColumn, columnsByName, IsTimestampType, "Cursor timestamp column must be datetime-like.", cursor.ValidationErrors);
                break;
            case "timestampthenid":
                RequireColumn(cursor.TimestampColumn, "Cursor mode 'timestamp_then_id' requires an explicit timestamp column.", cursor.ValidationErrors);
                RequireColumn(cursor.TieBreakerColumn ?? cursor.IdColumn, "Cursor mode 'timestamp_then_id' requires an explicit integer tie-breaker column.", cursor.ValidationErrors);
                ValidateColumnType(cursor.TimestampColumn, columnsByName, IsTimestampType, "Cursor timestamp column must be datetime-like.", cursor.ValidationErrors);
                ValidateColumnType(cursor.TieBreakerColumn ?? cursor.IdColumn, columnsByName, IsIntegerType, "Cursor tie-breaker column must be integer-like.", cursor.ValidationErrors);
                break;
            default:
                cursor.ValidationErrors.Add("Cursor mode must be one of: none, id, timestamp, timestamp_then_id.");
                break;
        }

        if (cursor.ValidationErrors.Count > 0)
            cursor.Status = "invalid";

        return cursor;
    }

    private static List<DataSourcePreviewFieldMapping> BuildFieldMappings(
        CanonicalEntitySpec entity,
        IReadOnlyList<DataSourceFieldMappingSelection>? requestedMappings,
        IReadOnlyDictionary<string, SourceColumnDefinition> columnsByName)
    {
        var requestedByTarget = new Dictionary<string, DataSourceFieldMappingSelection>(StringComparer.OrdinalIgnoreCase);
        var duplicateTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invalidTargets = new List<DataSourcePreviewFieldMapping>();

        if (requestedMappings is not null)
        {
            foreach (var mapping in requestedMappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.TargetField))
                    continue;

                var normalizedTarget = Normalize(mapping.TargetField);
                if (!entity.FieldsByNormalizedName.TryGetValue(normalizedTarget, out var field))
                {
                    invalidTargets.Add(new DataSourcePreviewFieldMapping
                    {
                        TargetField = mapping.TargetField.Trim(),
                        ValueType = "unknown",
                        Status = "invalid_target",
                        Transforms = NormalizeTransforms(mapping.Transforms),
                        ValidationErrors = [$"Target field '{mapping.TargetField}' is not supported for entity '{entity.Name}'."]
                    });
                    continue;
                }

                if (!requestedByTarget.TryAdd(field.TargetField, mapping))
                    duplicateTargets.Add(field.TargetField);
            }
        }

        var results = new List<DataSourcePreviewFieldMapping>(entity.Fields.Count + invalidTargets.Count);
        foreach (var field in entity.Fields)
        {
            requestedByTarget.TryGetValue(field.TargetField, out var requested);
            var result = new DataSourcePreviewFieldMapping
            {
                TargetField = field.TargetField,
                ValueType = field.ValueType,
                Required = field.Required
            };

            if (duplicateTargets.Contains(field.TargetField))
            {
                result.Status = "duplicate_target";
                result.ValidationErrors.Add($"Target field '{field.TargetField}' is mapped more than once.");
                results.Add(result);
                continue;
            }

            if (requested is null)
            {
                result.Status = field.Required ? "required_missing" : "missing";
                if (field.Required)
                    result.ValidationErrors.Add($"Required field '{field.TargetField}' is not mapped.");
                results.Add(result);
                continue;
            }

            result.Transforms = NormalizeTransforms(requested.Transforms);

            if (result.Transforms.Count != requested.Transforms.Count)
                result.ValidationErrors.Add("One or more requested transforms are not allowed. Allowed: trim, upper, lower, empty_to_null.");

            if (string.IsNullOrWhiteSpace(requested.SourceColumn))
            {
                result.Status = "invalid_source";
                result.ValidationErrors.Add($"Target field '{field.TargetField}' requires an explicit source column.");
                results.Add(result);
                continue;
            }

            if (!columnsByName.TryGetValue(requested.SourceColumn.Trim(), out var column))
            {
                result.SourceColumn = requested.SourceColumn.Trim();
                result.Status = "invalid_source";
                result.ValidationErrors.Add($"Source column '{requested.SourceColumn}' does not exist in the selected table.");
                results.Add(result);
                continue;
            }

            result.SourceColumn = column.Name;
            result.Status = result.ValidationErrors.Count == 0 ? "mapped" : "invalid_transform";
            results.Add(result);
        }

        results.AddRange(invalidTargets);
        return results;
    }

    private static bool TryParseFieldValue(
        object? rawValue,
        string valueType,
        IReadOnlyList<string> transforms,
        out object? parsedValue,
        out string? failureReason)
    {
        parsedValue = null;
        failureReason = null;
        var normalizedType = Normalize(valueType);

        if (rawValue is null or DBNull)
            return true;

        switch (normalizedType)
        {
            case "string":
                parsedValue = ApplyStringTransforms(Convert.ToString(rawValue, CultureInfo.InvariantCulture), transforms);
                return true;

            case "integer":
                if (TryConvertToInt64(rawValue, out var longValue))
                {
                    parsedValue = longValue;
                    return true;
                }

                failureReason = $"Value '{rawValue}' is not a valid integer.";
                return false;

            case "decimal":
                if (TryConvertToDecimal(rawValue, out var decimalValue))
                {
                    parsedValue = decimalValue;
                    return true;
                }

                failureReason = $"Value '{rawValue}' is not a valid decimal.";
                return false;

            case "datetime":
                if (TryConvertToUtcDateTime(rawValue, out var dateTimeValue))
                {
                    parsedValue = dateTimeValue;
                    return true;
                }

                failureReason = $"Value '{rawValue}' is not a valid datetime.";
                return false;

            default:
                parsedValue = rawValue;
                return true;
        }
    }

    private static string? ApplyStringTransforms(string? value, IReadOnlyList<string> transforms)
    {
        if (value is null)
            return null;

        var current = value;
        foreach (var transform in transforms)
        {
            switch (Normalize(transform))
            {
                case "trim":
                    current = current.Trim();
                    break;
                case "upper":
                    current = current.ToUpperInvariant();
                    break;
                case "lower":
                    current = current.ToLowerInvariant();
                    break;
                case "emptytonull":
                    if (string.IsNullOrWhiteSpace(current))
                        current = string.Empty;
                    break;
            }
        }

        if (transforms.Any(transform => string.Equals(transform, "empty_to_null", StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrWhiteSpace(current))
            return null;

        return current;
    }

    private static bool TryConvertToInt64(object rawValue, out long result)
    {
        switch (rawValue)
        {
            case byte value:
                result = value;
                return true;
            case short value:
                result = value;
                return true;
            case int value:
                result = value;
                return true;
            case long value:
                result = value;
                return true;
            case decimal value when decimal.Truncate(value) == value && value >= long.MinValue && value <= long.MaxValue:
                result = decimal.ToInt64(value);
                return true;
            case float value when MathF.Truncate(value) == value:
                result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            case double value when Math.Truncate(value) == value:
                result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            case string text when long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static bool TryConvertToDecimal(object rawValue, out decimal result)
    {
        switch (rawValue)
        {
            case decimal value:
                result = value;
                return true;
            case byte value:
                result = value;
                return true;
            case short value:
                result = value;
                return true;
            case int value:
                result = value;
                return true;
            case long value:
                result = value;
                return true;
            case float value:
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case double value:
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case string text when decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0m;
                return false;
        }
    }

    private static bool TryConvertToUtcDateTime(object rawValue, out DateTime result)
    {
        switch (rawValue)
        {
            case DateTimeOffset dto:
                result = dto.UtcDateTime;
                return true;
            case DateTime dt:
                result = dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt.ToUniversalTime();
                return true;
            case string text when DateTimeOffset.TryParse(
                text.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var parsed):
                result = parsed.UtcDateTime;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static string? BuildExternalKey(
        IReadOnlyDictionary<string, object?> snapshot,
        IReadOnlyList<string> externalKeyColumns,
        List<string> rejectionReasons)
    {
        if (externalKeyColumns.Count == 0)
            return null;

        var parts = new List<string>(externalKeyColumns.Count);
        foreach (var column in externalKeyColumns)
        {
            if (!snapshot.TryGetValue(column, out var rawValue) || rawValue is null or DBNull)
            {
                rejectionReasons.Add($"External key column '{column}' is null.");
                return null;
            }

            var text = Convert.ToString(rawValue, CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                rejectionReasons.Add($"External key column '{column}' is empty.");
                return null;
            }

            parts.Add(text);
        }

        return string.Join("|", parts);
    }

    private static IReadOnlyDictionary<string, CanonicalEntitySpec> BuildEntitySpecs()
    {
        var entities = new[]
        {
            new CanonicalEntitySpec(
                "Product",
                [
                    new CanonicalFieldSpec("name", "string", true),
                    new CanonicalFieldSpec("sku", "string", false),
                    new CanonicalFieldSpec("supplier_external_id", "string", false),
                    new CanonicalFieldSpec("cost", "decimal", false),
                    new CanonicalFieldSpec("price", "decimal", false),
                    new CanonicalFieldSpec("quantity", "decimal", false)
                ]),
            new CanonicalEntitySpec(
                "Supplier",
                [
                    new CanonicalFieldSpec("name", "string", true),
                    new CanonicalFieldSpec("address", "string", false),
                    new CanonicalFieldSpec("phone", "string", false)
                ]),
            new CanonicalEntitySpec(
                "Sale",
                [
                    new CanonicalFieldSpec("occurred_at", "datetime", true),
                    new CanonicalFieldSpec("receipt_number", "string", false),
                    new CanonicalFieldSpec("store_external_id", "string", false),
                    new CanonicalFieldSpec("product_external_id", "string", false),
                    new CanonicalFieldSpec("quantity", "decimal", false),
                    new CanonicalFieldSpec("unit_price", "decimal", false)
                ]),
            new CanonicalEntitySpec(
                "InventoryMovement",
                [
                    new CanonicalFieldSpec("product_external_id", "string", true),
                    new CanonicalFieldSpec("occurred_at", "datetime", true),
                    new CanonicalFieldSpec("quantity", "decimal", true),
                    new CanonicalFieldSpec("supplier_external_id", "string", false),
                    new CanonicalFieldSpec("movement_type", "string", false),
                    new CanonicalFieldSpec("unit_cost", "decimal", false)
                ]),
            new CanonicalEntitySpec(
                "PriceChange",
                [
                    new CanonicalFieldSpec("product_external_id", "string", true),
                    new CanonicalFieldSpec("occurred_at", "datetime", true),
                    new CanonicalFieldSpec("old_price", "decimal", false),
                    new CanonicalFieldSpec("new_price", "decimal", true)
                ])
        };

        return entities.ToDictionary(entity => Normalize(entity.Name), StringComparer.OrdinalIgnoreCase);
    }

    private static CanonicalEntitySpec GetEntitySpec(string? canonicalEntity)
    {
        var normalized = Normalize(canonicalEntity);
        if (!EntitySpecs.TryGetValue(normalized, out var entity))
        {
            throw new ArgumentException(
                $"Canonical entity must be one of: {string.Join(", ", EntitySpecs.Values.Select(entity => entity.Name))}.",
                nameof(canonicalEntity));
        }

        return entity;
    }

    private static int ResolveSampleSize(int requestedSize, int maxSize)
        => Math.Clamp(requestedSize <= 0 ? 10 : requestedSize, 1, Math.Clamp(maxSize, 1, 200));

    private int GetPreviewTimeoutSeconds() => Math.Clamp(_options.PreviewTimeoutSeconds, 1, 300);

    private static string ComputeSchemaFingerprint(IReadOnlyList<SourceColumnDefinition> columns)
    {
        var fingerprintInput = string.Join(
            "\n",
            columns
                .OrderBy(column => Normalize(column.Name), StringComparer.OrdinalIgnoreCase)
                .ThenBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
                .Select(column =>
                    $"{Normalize(column.Name)}|{column.Name}|{NormalizeSourceType(column.SourceType)}|{NormalizeNullability(column.IsNullable)}"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
    }

    private static void AddDuplicateAliasWarnings(
        IReadOnlyList<SourceColumnDefinition> columns,
        List<string> warnings)
    {
        foreach (var group in columns
            .GroupBy(column => Normalize(column.Name), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1))
        {
            warnings.Add($"Source schema contains duplicate normalized alias '{group.Key}': {string.Join(", ", group.Select(column => column.Name))}.");
        }
    }

    private static string NormalizeNullability(bool? isNullable)
        => isNullable.HasValue ? (isNullable.Value ? "nullable" : "required") : "unknown";

    private static string NormalizeSourceType(string? sourceType)
        => string.IsNullOrWhiteSpace(sourceType) ? "unknown" : sourceType.Trim().ToLowerInvariant();

    private static string Normalize(string? value) => AccessImportService.Normalize(value);

    private static string NormalizeCursorMode(string? value)
    {
        var normalized = Normalize(value);
        return normalized switch
        {
            "" => "none",
            "timestampthenid" => "timestamp_then_id",
            _ => value?.Trim().ToLowerInvariant() ?? "none"
        };
    }

    private static List<string> NormalizeTransforms(IReadOnlyList<string>? transforms)
    {
        if (transforms is null || transforms.Count == 0)
            return [];

        return transforms
            .Where(transform => !string.IsNullOrWhiteSpace(transform) && AllowedTransforms.Contains(transform.Trim()))
            .Select(transform => transform.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ResolveColumn(
        string? requestedName,
        IReadOnlyDictionary<string, SourceColumnDefinition> columnsByName)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
            return null;

        return columnsByName.TryGetValue(requestedName.Trim(), out var column)
            ? column.Name
            : requestedName.Trim();
    }

    private static void RequireColumn(string? column, string error, List<string> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(column))
            validationErrors.Add(error);
    }

    private static void ValidateColumnType(
        string? column,
        IReadOnlyDictionary<string, SourceColumnDefinition> columnsByName,
        Func<string, bool> predicate,
        string error,
        List<string> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(column))
            return;

        if (!columnsByName.TryGetValue(column, out var definition))
        {
            validationErrors.Add($"Column '{column}' does not exist in the selected table.");
            return;
        }

        var sourceType = NormalizeSourceType(definition.SourceType);
        if (sourceType != "unknown" && !predicate(sourceType))
            validationErrors.Add(error);
    }

    private static bool IsIntegerType(string sourceType)
        => sourceType.Contains("int", StringComparison.OrdinalIgnoreCase)
            || sourceType.Contains("bigint", StringComparison.OrdinalIgnoreCase)
            || sourceType.Contains("smallint", StringComparison.OrdinalIgnoreCase)
            || sourceType.Contains("numeric", StringComparison.OrdinalIgnoreCase)
            || sourceType.Contains("decimal", StringComparison.OrdinalIgnoreCase);

    private static bool IsTimestampType(string sourceType)
        => sourceType.Contains("date", StringComparison.OrdinalIgnoreCase)
            || sourceType.Contains("time", StringComparison.OrdinalIgnoreCase);

    private static bool IsMappingBlocking(string status)
    {
        var normalized = Normalize(status);
        return normalized is "requiredmissing" or "invalidsource" or "invalidtransform" or "invalidtarget" or "duplicatetarget";
    }

    private NamedDataSourceProfile GetProfile(string profileName)
    {
        if (_catalog.TryGetProfile(profileName, out var profile, out var error))
            return profile;

        throw new KeyNotFoundException(error ?? $"Data source profile '{profileName}' was not found.");
    }

    private sealed record CanonicalFieldSpec(string TargetField, string ValueType, bool Required);

    private sealed class CanonicalEntitySpec
    {
        public CanonicalEntitySpec(string name, IReadOnlyList<CanonicalFieldSpec> fields)
        {
            Name = name;
            Fields = fields;
            FieldsByNormalizedName = fields.ToDictionary(field => Normalize(field.TargetField), StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; }

        public IReadOnlyList<CanonicalFieldSpec> Fields { get; }

        public IReadOnlyDictionary<string, CanonicalFieldSpec> FieldsByNormalizedName { get; }
    }
}
