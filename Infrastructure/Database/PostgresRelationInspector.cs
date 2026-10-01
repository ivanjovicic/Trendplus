using Npgsql;

namespace Infrastructure.Database;

/// <summary>Read-only view of one PostgreSQL relation as resolved by the current connection search path.</summary>
public sealed record PostgresRelationInspection(
    string CurrentUser,
    string[] CurrentSchemas,
    string? ToRegclass,
    string? ResolvedSchema,
    string[] SchemasWithRelation,
    bool HasSelectPrivilege,
    string[] Columns)
{
    public bool IsResolved => ToRegclass is not null;
}

/// <summary>Shared pg_catalog inspection used by startup readiness and runtime analytics contracts.</summary>
public static class PostgresRelationInspector
{
    private const string InspectSql = """
        WITH resolved AS (
            SELECT to_regclass(@relationName)::oid AS relation_oid
        ), candidates AS (
            SELECT n.nspname::text AS schema_name,
                   array_position(current_schemas(true)::text[], n.nspname::text) AS search_position
            FROM pg_catalog.pg_class c
            JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = @relationName
              AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
        )
        SELECT current_user::text,
               current_schemas(true)::text[],
               r.relation_oid::regclass::text,
               resolved_namespace.nspname::text,
               COALESCE(
                   (SELECT array_agg(c.schema_name ORDER BY c.search_position NULLS LAST, c.schema_name)
                    FROM candidates c),
                   ARRAY[]::text[]),
               CASE WHEN r.relation_oid IS NULL THEN false
                    ELSE has_table_privilege(current_user, r.relation_oid, 'SELECT') END,
               COALESCE(
                   (SELECT array_agg(a.attname::text ORDER BY a.attnum)
                    FROM pg_catalog.pg_attribute a
                    WHERE a.attrelid = r.relation_oid
                      AND a.attnum > 0
                      AND NOT a.attisdropped),
                   ARRAY[]::text[])
        FROM resolved r
        LEFT JOIN pg_catalog.pg_class resolved_class ON resolved_class.oid = r.relation_oid
        LEFT JOIN pg_catalog.pg_namespace resolved_namespace ON resolved_namespace.oid = resolved_class.relnamespace;
        """;

    public static async Task<PostgresRelationInspection> InspectAsync(
        NpgsqlConnection connection,
        string relationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(relationName);

        await using var command = new NpgsqlCommand(InspectSql, connection);
        command.Parameters.AddWithValue("relationName", relationName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("PostgreSQL relation inspection returned no row.");
        }

        return new PostgresRelationInspection(
            reader.GetString(0),
            reader.GetFieldValue<string[]>(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetFieldValue<string[]>(4),
            reader.GetBoolean(5),
            reader.GetFieldValue<string[]>(6));
    }
}
