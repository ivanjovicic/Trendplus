using Application.Analytics;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Services;

/// <summary>
/// Implementation-independent sale-line oracle for Supplier/Shoe Type drift probes (RQ412/RQ413).
/// </summary>
public static class OperationsAnalyticsRawFactOracle
{
    public sealed record Filters(
        DateTime FromUtc,
        DateTime ToUtc,
        int? StoreId,
        string DataScope);

    public sealed record Totals(int SaleLineCount, int TotalUnits, decimal TotalRevenue);

    public static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    public static async Task<Totals> QueryTotalsAsync(
        NpgsqlConnection connection,
        Filters filters,
        CancellationToken ct = default)
    {
        var sql = $"""
            SELECT
                COUNT(*)::int AS sale_line_count,
                COALESCE(SUM(ps.kolicina), 0)::int AS total_units,
                COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS total_revenue
            FROM prodaja_stavke ps
            INNER JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
            INNER JOIN "Artikli" a ON ps.id_artikal = a."Id"
            WHERE pz.datum_prodaje >= @fromUtc
              AND pz.datum_prodaje <= @toUtc
              AND (@storeId IS NULL OR pz.id_objekat = @storeId)
              AND (
                    @dataScope = 'all'
                    OR (@dataScope = 'imported' AND a."DataOrigin" = 'access')
                    OR (@dataScope = 'existing' AND (a."DataOrigin" = 'existing' OR a."DataOrigin" IS NULL OR a."DataOrigin" = ''))
                  )
              AND {RetailSalesReceiptPopulation.SqlExclusionPredicate}
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        AddFilterParameters(command, filters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Operations raw-fact oracle returned no totals row.");

        return new Totals(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2));
    }

    private static void AddFilterParameters(NpgsqlCommand command, Filters filters)
    {
        var dataScope = NormalizeDataScope(filters.DataScope);
        command.Parameters.Add(new NpgsqlParameter("fromUtc", NpgsqlDbType.TimestampTz) { Value = filters.FromUtc });
        command.Parameters.Add(new NpgsqlParameter("toUtc", NpgsqlDbType.TimestampTz) { Value = filters.ToUtc });
        command.Parameters.Add(new NpgsqlParameter("storeId", NpgsqlDbType.Integer)
        {
            Value = filters.StoreId.HasValue ? filters.StoreId.Value : DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("dataScope", NpgsqlDbType.Text) { Value = dataScope });
    }
}
