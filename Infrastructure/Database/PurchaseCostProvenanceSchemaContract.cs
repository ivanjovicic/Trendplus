using Npgsql;

namespace Infrastructure.Database;

/// <summary>
/// Read-only verification for the PostgreSQL column consumed by sale-line cost projections.
/// Repairs remain owned by the explicit database initializer; readiness never performs DDL.
/// </summary>
public static class PurchaseCostProvenanceSchemaContract
{
    public const string ColumnName = "purchase_cost_provenance";

    public static async Task EnsureColumnAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            ALTER TABLE IF EXISTS public.prodaja_stavke
                ADD COLUMN IF NOT EXISTS purchase_cost_provenance character varying(64);
            """,
            connection)
        {
            CommandTimeout = 10
        };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<string?> ValidateAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT data_type, character_maximum_length, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'prodaja_stavke'
              AND column_name = @columnName;
            """,
            connection)
        {
            CommandTimeout = 10
        };
        command.Parameters.AddWithValue("columnName", ColumnName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return "public.prodaja_stavke.purchase_cost_provenance is missing; expected nullable character varying(64).";
        }

        var dataType = reader.GetString(0);
        var maxLength = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
        var nullable = string.Equals(reader.GetString(2), "YES", StringComparison.Ordinal);
        if (!string.Equals(dataType, "character varying", StringComparison.Ordinal)
            || maxLength != 64
            || !nullable)
        {
            return $"public.prodaja_stavke.purchase_cost_provenance has incompatible schema " +
                   $"(data_type={dataType}, max_length={maxLength?.ToString() ?? "unbounded"}, nullable={nullable}); " +
                   "expected nullable character varying(64).";
        }

        return null;
    }
}
