using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.Prodaja.Queries;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Repository
{
    public class ProdajaRepository : IProdajaRepository
    {
        private readonly string _connStr;

        public ProdajaRepository(IConfiguration config)
        {
            _connStr = config.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        public async Task<int> ProdajAsync(
            ProdajArtikleCommand command,
            CancellationToken ct)
        {
            await using var conn = new NpgsqlConnection(_connStr);
            await conn.OpenAsync(ct);

            // Serijalizuj stavke u JSON (camelCase da se poklopi sa funkcijom)
            var stavkeJson = JsonSerializer.Serialize(command.Stavke, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sp_prodaj_artikle_json($1::varchar, $2::integer, $3::varchar, $4::jsonb)";

            cmd.Parameters.Add(new NpgsqlParameter { Value = command.BrojRacuna ?? string.Empty, NpgsqlDbType = NpgsqlDbType.Varchar });
            cmd.Parameters.Add(new NpgsqlParameter { Value = command.IdObjekat, NpgsqlDbType = NpgsqlDbType.Integer });
            cmd.Parameters.Add(new NpgsqlParameter { Value = command.NacinPlacanja ?? string.Empty, NpgsqlDbType = NpgsqlDbType.Varchar });
            cmd.Parameters.Add(new NpgsqlParameter { Value = stavkeJson, NpgsqlDbType = NpgsqlDbType.Jsonb });

            var result = await cmd.ExecuteScalarAsync(ct);
            var prodajaId = Convert.ToInt32(result);

            // Backfill purchase price on operational sale lines so both analytics
            // backfill and outbox projection can read sale-time gross margin inputs.
            await using var costCmd = conn.CreateCommand();
            costCmd.CommandText = @"
                WITH payload AS (
                    SELECT
                        ordinality,
                        NULLIF(item->>'nabavnaCena', '')::numeric(18,2) AS nabavna_cena
                    FROM jsonb_array_elements($2::jsonb) WITH ORDINALITY AS elements(item, ordinality)
                ),
                sale_lines AS (
                    SELECT
                        ps.id,
                        ps.id_artikal,
                        ROW_NUMBER() OVER (ORDER BY ps.id) AS ordinality
                    FROM prodaja_stavke ps
                    WHERE ps.id_prodaja = $1
                )
                UPDATE prodaja_stavke ps
                SET nabavna_cena = COALESCE(payload.nabavna_cena, a.""NabavnaCena"")
                FROM sale_lines sl
                LEFT JOIN payload ON payload.ordinality = sl.ordinality
                LEFT JOIN ""Artikli"" a ON a.""Id"" = sl.id_artikal
                WHERE ps.id = sl.id
                  AND (
                      ps.nabavna_cena IS NULL
                      OR payload.nabavna_cena IS NOT NULL
                  );";
            costCmd.Parameters.Add(new NpgsqlParameter { Value = prodajaId, NpgsqlDbType = NpgsqlDbType.Integer });
            costCmd.Parameters.Add(new NpgsqlParameter { Value = stavkeJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
            await costCmd.ExecuteNonQueryAsync(ct);

            // Insert into DnevnikPromena for audit trail
            await using var logCmd = conn.CreateCommand();
            logCmd.CommandText = @"
                INSERT INTO ""DnevnikPromena"" 
                    (""TipPromene"", ""Datum"", ""Iznos"", ""BrojRacuna"", ""Komentar"")
                SELECT 
                    'Prodaja',
                    CURRENT_TIMESTAMP AT TIME ZONE 'UTC',
                    COALESCE(SUM(ps.kolicina * ps.cena), 0),
                    pz.broj_racuna,
                    'Prodaja - ' || COALESCE(pz.broj_racuna, 'N/A') || ' (' || pz.nacin_placanja || ')'
                FROM prodaja_zaglavlje pz
                LEFT JOIN prodaja_stavke ps ON pz.id = ps.id_prodaja
                WHERE pz.id = $1
                GROUP BY pz.id, pz.broj_racuna, pz.nacin_placanja";
            
            logCmd.Parameters.Add(new NpgsqlParameter { Value = prodajaId, NpgsqlDbType = NpgsqlDbType.Integer });
            await logCmd.ExecuteNonQueryAsync(ct);

            return prodajaId;
        }

        public async Task<ProdajeListResponse> GetProdajeAsync(
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken ct)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connStr);
                await conn.OpenAsync(ct);

                // Build WHERE clause
                var whereConditions = new List<string>();
                var parameters = new List<NpgsqlParameter>();
                int paramIndex = 1;

                if (fromDate.HasValue)
                {
                    whereConditions.Add($"pz.datum_prodaje >= ${paramIndex}");
                    parameters.Add(new NpgsqlParameter { Value = fromDate.Value, NpgsqlDbType = NpgsqlDbType.Timestamp });
                    paramIndex++;
                }

                if (toDate.HasValue)
                {
                    whereConditions.Add($"pz.datum_prodaje <= ${paramIndex}");
                    parameters.Add(new NpgsqlParameter { Value = toDate.Value.AddDays(1).AddTicks(-1), NpgsqlDbType = NpgsqlDbType.Timestamp });
                    paramIndex++;
                }

                var whereClause = whereConditions.Count > 0 
                    ? "WHERE " + string.Join(" AND ", whereConditions) 
                    : "";

                // Get total count
                var countSql = $@"
                    SELECT COUNT(*)
                    FROM prodaja_zaglavlje pz
                    {whereClause}";

                await using var countCmd = conn.CreateCommand();
                countCmd.CommandText = countSql;
                foreach (var p in parameters)
                {
                    countCmd.Parameters.Add(new NpgsqlParameter { Value = p.Value, NpgsqlDbType = p.NpgsqlDbType });
                }
                var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

                // Get paginated data
                var offset = (pageNumber - 1) * pageSize;
                var dataSql = $@"
                    SELECT 
                        pz.id,
                        pz.broj_racuna,
                        pz.datum_prodaje,
                        COALESCE(SUM(ps.kolicina * ps.cena), 0) as ukupan_iznos,
                        COUNT(ps.id) as broj_stavki,
                        pz.nacin_placanja
                    FROM prodaja_zaglavlje pz
                    LEFT JOIN prodaja_stavke ps ON pz.id = ps.id_prodaja
                    {whereClause}
                    GROUP BY pz.id, pz.broj_racuna, pz.datum_prodaje, pz.nacin_placanja
                    ORDER BY pz.datum_prodaje DESC
                    LIMIT ${paramIndex} OFFSET ${paramIndex + 1}";

                await using var dataCmd = conn.CreateCommand();
                dataCmd.CommandText = dataSql;
                foreach (var p in parameters)
                {
                    dataCmd.Parameters.Add(new NpgsqlParameter { Value = p.Value, NpgsqlDbType = p.NpgsqlDbType });
                }
                dataCmd.Parameters.Add(new NpgsqlParameter { Value = pageSize, NpgsqlDbType = NpgsqlDbType.Integer });
                dataCmd.Parameters.Add(new NpgsqlParameter { Value = offset, NpgsqlDbType = NpgsqlDbType.Integer });

                var items = new List<ProdajaListItemDto>();
                await using var reader = await dataCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    items.Add(new ProdajaListItemDto(
                        Id: reader.GetInt32(0),
                        BrojRacuna: reader.IsDBNull(1) ? "" : reader.GetString(1),
                        DatumProdaje: reader.GetDateTime(2),
                        UkupanIznos: reader.GetDecimal(3),
                        BrojStavki: Convert.ToInt32(reader.GetInt64(4)),
                        NacinPlacanja: reader.IsDBNull(5) ? "" : reader.GetString(5)
                    ));
                }

                return new ProdajeListResponse(items, totalCount, pageNumber, pageSize);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01") // relation does not exist
            {
                // Tables don't exist yet - return empty result
                return new ProdajeListResponse(new List<ProdajaListItemDto>(), 0, pageNumber, pageSize);
            }
        }
    }
}
