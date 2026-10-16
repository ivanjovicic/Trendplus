using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Workers;

/// <summary>
/// Projects goods receipt movements into analytics with a durable operational retry marker.
/// The operational and analytics databases are separate; the upsert is therefore repeated safely
/// until the operational marker can be committed after it.
/// </summary>
public static class GoodsReceiptMovementProjector
{
    private const int BatchSize = 500;

    public static async Task<int> ProjectPendingAsync(
        TrendplusDbContext operationalDb,
        AnalyticsDbContext analyticsDb,
        CancellationToken ct)
    {
        var connection = (NpgsqlConnection)operationalDb.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(ct);

        try
        {
            var pending = new List<PendingMovement>();
            await using (var read = new NpgsqlCommand("""
                SELECT l."Id", d."Id", d."TipPromene", d."Datum", d."ArtikalId", d."Kolicina",
                       d."StaraProdajnaCena", d."NovaProdajnaCena", d."Iznos", d."IDObjekat",
                       d."DobavljacId", d."BrojRacuna", d."KorisnikIme", d."DataOrigin", false AS "IsReversal"
                FROM "GoodsReceiptLines" l
                JOIN "DnevnikPromena" d ON d."GoodsReceiptLineId" = l."Id"
                WHERE l."AnalyticsProjectedAtUtc" IS NULL
                UNION ALL
                SELECT l."Id", d."Id", d."TipPromene", d."Datum", d."ArtikalId", d."Kolicina",
                       d."StaraProdajnaCena", d."NovaProdajnaCena", d."Iznos", d."IDObjekat",
                       d."DobavljacId", d."BrojRacuna", d."KorisnikIme", d."DataOrigin", true AS "IsReversal"
                FROM "GoodsReceiptLines" l
                JOIN "DnevnikPromena" d ON d."Id" = l."ReversalMovementId"
                WHERE l."ReversalMovementId" IS NOT NULL AND l."ReversalProjectedAtUtc" IS NULL
                ORDER BY "Id", "IsReversal"
                LIMIT @batchSize
                """, connection))
            {
                read.Parameters.AddWithValue("batchSize", BatchSize);
                await using var reader = await read.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    pending.Add(new PendingMovement(
                        reader.GetInt64(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc),
                        reader.IsDBNull(4) ? null : reader.GetInt32(4),
                        reader.IsDBNull(5) ? null : reader.GetInt32(5),
                        reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                        reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                        reader.GetDecimal(8),
                        reader.IsDBNull(9) ? null : reader.GetInt32(9),
                        reader.IsDBNull(10) ? null : reader.GetInt32(10),
                        reader.IsDBNull(11) ? null : reader.GetString(11),
                        reader.IsDBNull(12) ? null : reader.GetString(12),
                        reader.GetString(13),
                        reader.GetBoolean(14)));
                }
            }

            foreach (var movement in pending)
            {
                await analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "InventoryMovementFacts" ("SourceId", "TipPromene", "Datum", "ArtikalId", "Kolicina", "StaraProdajnaCena", "NovaProdajnaCena", "Iznos", "StoreId", "DobavljacId", "BrojDokumenta", "KorisnikIme", "DataOrigin")
                    VALUES ({movement.SourceId}, {movement.ChangeType}, {movement.DateUtc}, {movement.ArticleId}, {movement.Quantity}, {movement.OldSalesPrice}, {movement.NewSalesPrice}, {movement.Amount}, {movement.StoreId}, {movement.SupplierId}, {movement.DocumentNumber}, {movement.UserName}, {movement.DataOrigin})
                    ON CONFLICT ("SourceId", "DataOrigin") DO UPDATE
                    SET "TipPromene" = EXCLUDED."TipPromene",
                        "Datum" = EXCLUDED."Datum",
                        "ArtikalId" = EXCLUDED."ArtikalId",
                        "Kolicina" = EXCLUDED."Kolicina",
                        "StaraProdajnaCena" = EXCLUDED."StaraProdajnaCena",
                        "NovaProdajnaCena" = EXCLUDED."NovaProdajnaCena",
                        "Iznos" = EXCLUDED."Iznos",
                        "StoreId" = EXCLUDED."StoreId",
                        "DobavljacId" = EXCLUDED."DobavljacId",
                        "BrojDokumenta" = EXCLUDED."BrojDokumenta",
                        "KorisnikIme" = EXCLUDED."KorisnikIme",
                        "DataOrigin" = EXCLUDED."DataOrigin";
                    """, ct);

                var markerSql = movement.IsReversal
                    ? """
                      UPDATE "GoodsReceiptLines"
                      SET "ReversalProjectedAtUtc" = NOW()
                      WHERE "Id" = @lineId AND "ReversalProjectedAtUtc" IS NULL
                      """
                    : """
                      UPDATE "GoodsReceiptLines"
                      SET "AnalyticsProjectedAtUtc" = NOW()
                      WHERE "Id" = @lineId AND "AnalyticsProjectedAtUtc" IS NULL
                      """;
                await using var mark = new NpgsqlCommand(markerSql, connection);
                mark.Parameters.AddWithValue("lineId", movement.ReceiptLineId);
                await mark.ExecuteNonQueryAsync(ct);
            }

            return pending.Count;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    private sealed record PendingMovement(
        long ReceiptLineId,
        int SourceId,
        string ChangeType,
        DateTime DateUtc,
        int? ArticleId,
        int? Quantity,
        decimal? OldSalesPrice,
        decimal? NewSalesPrice,
        decimal Amount,
        int? StoreId,
        int? SupplierId,
        string? DocumentNumber,
        string? UserName,
        string DataOrigin,
        bool IsReversal);
}
