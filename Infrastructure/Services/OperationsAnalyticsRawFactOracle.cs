using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Services;

/// <summary>
/// Implementation-independent sale-line oracle for bounded Operations drift probes.
/// </summary>
public static class OperationsAnalyticsRawFactOracle
{
    public sealed record Filters(
        DateTime FromUtc,
        DateTime ToUtc,
        int? StoreId,
        string DataScope);

    public sealed record Totals(int SaleLineCount, int TotalUnits, decimal TotalRevenue);
    public sealed record Bucket(int? DimensionId, int Units, decimal Revenue, int SaleLineCount);
    public sealed record TextBucket(string DimensionKey, int Units, decimal Revenue, int SaleLineCount);
    public sealed record DailyBucket(
        DateTime SaleDate,
        int? StoreId,
        int HourOfDay,
        string? SourceTimestampBasis,
        string? DataOrigin,
        int Units,
        decimal Revenue,
        int SaleLineCount);

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
        const string sql = """
            SELECT
                COUNT(*)::int AS sale_line_count,
                COALESCE(SUM(ps.kolicina), 0)::int AS total_units,
                COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS total_revenue
            FROM prodaja_stavke ps
            INNER JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
            INNER JOIN "Artikli" a ON ps.id_artikal = a."Id"
            WHERE pz.datum_prodaje >= @fromUtc
              AND pz.datum_prodaje < @toUtc
              AND (@storeId IS NULL OR pz.id_objekat = @storeId)
              AND UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
              AND (
                    @dataScope = 'all'
                    OR (@dataScope = 'imported' AND pz.data_origin = 'access')
                    OR (@dataScope = 'existing' AND (pz.data_origin = 'existing' OR pz.data_origin IS NULL OR pz.data_origin = ''))
                  )
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        AddFilterParameters(command, filters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Operations raw-fact oracle returned no totals row.");

        return new Totals(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2));
    }

    public static Task<IReadOnlyList<Bucket>> QuerySupplierBucketsAsync(
        NpgsqlConnection connection,
        Filters filters,
        CancellationToken ct = default)
        => QueryBucketsAsync(
            connection,
            filters,
            "CASE WHEN ps.supplier_id_at_sale IS NULL OR NULLIF(BTRIM(d.\"Naziv\"), '') IS NULL OR LOWER(BTRIM(d.\"Naziv\")) = 'nepoznato' THEN NULL::integer ELSE ps.supplier_id_at_sale END",
            """LEFT JOIN "Dobavljaci" d ON d."Id" = ps.supplier_id_at_sale""",
            ct);

    public static Task<IReadOnlyList<Bucket>> QueryShoeTypeBucketsAsync(
        NpgsqlConnection connection,
        Filters filters,
        CancellationToken ct = default)
        => QueryBucketsAsync(connection, filters, "ps.shoe_type_id_at_sale", "", ct);

    public static async Task<IReadOnlyList<TextBucket>> QueryColorBucketsAsync(
        NpgsqlConnection connection,
        Filters filters,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                a."Boja" AS dimension_value,
                COALESCE(SUM(ps.kolicina), 0)::int AS total_units,
                COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS total_revenue,
                COUNT(*)::int AS sale_line_count
            FROM prodaja_stavke ps
            INNER JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
            INNER JOIN "Artikli" a ON ps.id_artikal = a."Id"
            WHERE pz.datum_prodaje >= @fromUtc
              AND pz.datum_prodaje < @toUtc
              AND (@storeId IS NULL OR pz.id_objekat = @storeId)
              AND UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
              AND (
                    @dataScope = 'all'
                    OR (@dataScope = 'imported' AND pz.data_origin = 'access')
                    OR (@dataScope = 'existing' AND (pz.data_origin = 'existing' OR pz.data_origin IS NULL OR pz.data_origin = ''))
                  )
            GROUP BY a."Boja"
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        AddFilterParameters(command, filters);
        var buckets = new Dictionary<string, (int Units, decimal Revenue, int SaleLineCount)>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var key = Application.Analytics.ColorIdentityPolicy.Key(reader.IsDBNull(0) ? null : reader.GetString(0));
            var prior = buckets.GetValueOrDefault(key);
            buckets[key] = (
                prior.Units + reader.GetInt32(1),
                prior.Revenue + reader.GetDecimal(2),
                prior.SaleLineCount + reader.GetInt32(3));
        }

        return buckets
            .Select(pair => new TextBucket(pair.Key, pair.Value.Units, pair.Value.Revenue, pair.Value.SaleLineCount))
            .ToArray();
    }

    public static async Task<IReadOnlyList<DailyBucket>> QueryDailyBucketsAsync(
        NpgsqlConnection connection,
        Filters filters,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                (pz.datum_prodaje AT TIME ZONE 'UTC')::date AS sale_date,
                pz.id_objekat AS store_id,
                EXTRACT(HOUR FROM (pz.datum_prodaje AT TIME ZONE 'UTC'))::int AS hour_of_day,
                pz.source_timestamp_basis,
                pz.data_origin,
                COALESCE(SUM(ps.kolicina), 0)::int AS total_units,
                COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS total_revenue,
                COUNT(*)::int AS sale_line_count
            FROM prodaja_stavke ps
            INNER JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
            INNER JOIN "Artikli" a ON ps.id_artikal = a."Id"
            WHERE pz.datum_prodaje >= @fromUtc
              AND pz.datum_prodaje < @toUtc
              AND (@storeId IS NULL OR pz.id_objekat = @storeId)
              AND UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
              AND (
                    @dataScope = 'all'
                    OR (@dataScope = 'imported' AND pz.data_origin = 'access')
                    OR (@dataScope = 'existing' AND (pz.data_origin = 'existing' OR pz.data_origin IS NULL OR pz.data_origin = ''))
                  )
            GROUP BY
                (pz.datum_prodaje AT TIME ZONE 'UTC')::date,
                pz.id_objekat,
                EXTRACT(HOUR FROM (pz.datum_prodaje AT TIME ZONE 'UTC')),
                pz.source_timestamp_basis,
                pz.data_origin
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        AddFilterParameters(command, filters);
        var buckets = new List<DailyBucket>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            buckets.Add(new DailyBucket(
                DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Unspecified),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5),
                reader.GetDecimal(6),
                reader.GetInt32(7)));
        }

        return buckets;
    }

    private static async Task<IReadOnlyList<Bucket>> QueryBucketsAsync(
        NpgsqlConnection connection,
        Filters filters,
        string dimensionColumn,
        string dimensionJoin,
        CancellationToken ct)
    {
        // The column is selected only from the two fixed call sites above.
        var sql = $"""
            SELECT
                {dimensionColumn} AS dimension_id,
                COALESCE(SUM(ps.kolicina), 0)::int AS total_units,
                COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS total_revenue,
                COUNT(*)::int AS sale_line_count
            FROM prodaja_stavke ps
            INNER JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
            INNER JOIN "Artikli" a ON ps.id_artikal = a."Id"
            {dimensionJoin}
            WHERE pz.datum_prodaje >= @fromUtc
              AND pz.datum_prodaje < @toUtc
              AND (@storeId IS NULL OR pz.id_objekat = @storeId)
              AND UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
              AND (
                    @dataScope = 'all'
                    OR (@dataScope = 'imported' AND pz.data_origin = 'access')
                    OR (@dataScope = 'existing' AND (pz.data_origin = 'existing' OR pz.data_origin IS NULL OR pz.data_origin = ''))
                  )
            GROUP BY {dimensionColumn}
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        AddFilterParameters(command, filters);
        var buckets = new List<Bucket>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            buckets.Add(new Bucket(
                reader.IsDBNull(0) ? null : reader.GetInt32(0),
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
