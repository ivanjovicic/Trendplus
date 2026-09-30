using Npgsql;

namespace Infrastructure.Analytics;

public sealed record MaterializedViewCapabilityResult(
    string SchemaName,
    string RelationName,
    bool Exists,
    bool IsPopulated,
    IReadOnlyList<string> MissingColumns)
{
    public bool HasRequiredColumns => MissingColumns.Count == 0;

    public bool IsReady => Exists && IsPopulated && HasRequiredColumns;

    public string? ErrorCode => !Exists
        ? "MISSING_OBJECT"
        : !HasRequiredColumns
            ? "MISSING_COLUMNS"
            : !IsPopulated
                ? "NOT_POPULATED"
                : null;
}

public static class SupplierDecisionMaterializedViewContract
{
    public static readonly string[] DecisionScoreRequiredColumns =
    [
        "supplier_id",
        "supplier_name",
        "period_from",
        "period_to",
        "revenue",
        "units",
        "fullprice_revenue_share",
        "fullprice_sellthrough",
        "pre_markdown_margin_pct",
        "repeat_winner_rate",
        "markdown_dependency_score",
        "stock_risk_score",
        "return_rate",
        "category_focus_score",
        "supplier_quality_index",
        "recommendation_code",
        "confidence_score",
        "post_signal_coverage"
    ];

    public static readonly string[] MlSupplierScoreColumn = ["ml_supplier_score"];
}

public static class PostgresMaterializedViewCapabilityReader
{
    public static async Task<MaterializedViewCapabilityResult> InspectAsync(
        NpgsqlConnection connection,
        string relationName,
        IReadOnlyCollection<string>? requiredColumns = null,
        string schemaName = "public",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        var normalizedRequiredColumns = (requiredColumns ?? Array.Empty<string>())
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Select(column => column.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(column => column, StringComparer.Ordinal)
            .ToArray();

        const string sql = """
            WITH target AS (
                SELECT
                    c.oid,
                    COALESCE(mv.ispopulated, FALSE) AS ispopulated
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                LEFT JOIN pg_matviews mv
                    ON mv.schemaname = n.nspname
                   AND mv.matviewname = c.relname
                WHERE n.nspname = @schemaName
                  AND c.relname = @relationName
                  AND c.relkind = 'm'
            )
            SELECT
                EXISTS (SELECT 1 FROM target) AS object_exists,
                COALESCE((SELECT ispopulated FROM target LIMIT 1), FALSE) AS is_populated,
                COALESCE(
                    ARRAY(
                        SELECT required_column
                        FROM unnest(@requiredColumns::text[]) AS required(required_column)
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM target t
                            JOIN pg_attribute a ON a.attrelid = t.oid
                            WHERE a.attname = required.required_column
                              AND a.attnum > 0
                              AND NOT a.attisdropped
                        )
                        ORDER BY required_column
                    ),
                    ARRAY[]::text[]
                ) AS missing_columns;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schemaName", schemaName);
        command.Parameters.AddWithValue("relationName", relationName);
        command.Parameters.AddWithValue("requiredColumns", normalizedRequiredColumns);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new MaterializedViewCapabilityResult(
                schemaName,
                relationName,
                Exists: false,
                IsPopulated: false,
                MissingColumns: normalizedRequiredColumns);
        }

        var missingColumns = reader.IsDBNull(reader.GetOrdinal("missing_columns"))
            ? Array.Empty<string>()
            : reader.GetFieldValue<string[]>(reader.GetOrdinal("missing_columns"));

        return new MaterializedViewCapabilityResult(
            schemaName,
            relationName,
            Exists: reader.GetBoolean(reader.GetOrdinal("object_exists")),
            IsPopulated: reader.GetBoolean(reader.GetOrdinal("is_populated")),
            MissingColumns: missingColumns);
    }
}
