using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailySalesStatsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE:
            // - "Dobavljaci"."Id" is already covered by primary-key index, so no extra index is added there.
            // - Existing migration 20260327201000 already added (datum_prodaje, id_objekat) and core sales indexes.
            // - The indexes below extend coverage for store-filtered scans and line-level aggregation projection.

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_prodaja_zaglavlje_id_objekat_datum_prodaje"
                ON "prodaja_zaglavlje" ("id_objekat", "datum_prodaje");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_prodaja_stavke_id_prodaja_id_artikal_cover_qty_price"
                ON "prodaja_stavke" ("id_prodaja", "id_artikal")
                INCLUDE ("kolicina", "cena");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_prodaja_zaglavlje_id_objekat_datum_prodaje";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_prodaja_stavke_id_prodaja_id_artikal_cover_qty_price";
                """);
        }
    }
}
