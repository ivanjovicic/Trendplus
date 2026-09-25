using Npgsql;
using NpgsqlTypes;

namespace Trendplus2.Tests.Analytics;

/// <summary>
/// Implementation-independent reference totals for Supplier and Shoe Type analytics.
/// Uses raw SQL against sale headers/lines and RQ411 attribution columns only.
/// </summary>
public static class SupplierShoeTypeRawFactOracle
{
    public sealed record Filters(
        DateTime FromUtc,
        DateTime ToUtc,
        int? StoreId,
        string DataScope);

    public sealed record Totals(
        int SaleLineCount,
        int TotalUnits,
        decimal TotalRevenue,
        int UnknownSupplierLines,
        int UnknownShoeTypeLines,
        int DistinctAttributionBases);

    public sealed record Bucket(int? DimensionId, int Units, decimal Revenue, int SaleLineCount);

    public static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    public static async Task<Totals> QueryTotalsAsync(NpgsqlConnection connection, Filters filters, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                COUNT(*)::int AS sale_line_count,
                COALESCE(SUM(ps.kolicina), 0)::int AS total_units,
                COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS total_revenue,
                COUNT(*) FILTER (WHERE ps.supplier_id_at_sale IS NULL)::int AS unknown_supplier_lines,
                COUNT(*) FILTER (WHERE ps.shoe_type_id_at_sale IS NULL)::int AS unknown_shoe_type_lines,
                COUNT(DISTINCT ps.attribution_basis)::int AS distinct_attribution_bases
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
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        AddFilterParameters(command, filters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Raw-fact oracle totals query returned no row.");

        return new Totals(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDecimal(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

    public static async Task<IReadOnlyList<Bucket>> QuerySupplierBucketsAsync(
        NpgsqlConnection connection,
        Filters filters,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                ps.supplier_id_at_sale AS dimension_id,
                COALESCE(SUM(ps.kolicina), 0)::int AS units,
                COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS revenue,
                COUNT(*)::int AS sale_line_count
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
            GROUP BY ps.supplier_id_at_sale
            """;

        return await QueryBucketsAsync(connection, sql, filters, ct);
    }

    public static async Task<IReadOnlyList<Bucket>> QueryShoeTypeBucketsAsync(
        NpgsqlConnection connection,
        Filters filters,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                ps.shoe_type_id_at_sale AS dimension_id,
                COALESCE(SUM(ps.kolicina), 0)::int AS units,
                COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS revenue,
                COUNT(*)::int AS sale_line_count
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
            GROUP BY ps.shoe_type_id_at_sale
            """;

        return await QueryBucketsAsync(connection, sql, filters, ct);
    }

    private static async Task<IReadOnlyList<Bucket>> QueryBucketsAsync(
        NpgsqlConnection connection,
        string sql,
        Filters filters,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        AddFilterParameters(command, filters);
        var buckets = new List<Bucket>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            int? dimensionId = reader.IsDBNull(0) ? null : reader.GetInt32(0);
            buckets.Add(new Bucket(
                dimensionId,
                reader.GetInt32(1),
                reader.GetDecimal(2),
                reader.GetInt32(3)));
        }

        return buckets;
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
