using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_prodaja_zaglavlje_data_origin_datum_prodaje"
                ON "prodaja_zaglavlje" ("data_origin", "datum_prodaje");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_prodaja_zaglavlje_datum_prodaje_id_objekat"
                ON "prodaja_zaglavlje" ("datum_prodaje", "id_objekat");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_prodaja_stavke_id_artikal"
                ON "prodaja_stavke" ("id_artikal");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_prodaja_stavke_id_prodaja_id_artikal"
                ON "prodaja_stavke" ("id_prodaja", "id_artikal");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Artikli_DataOrigin"
                ON "Artikli" ("DataOrigin");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Artikli_IDDobavljac"
                ON "Artikli" ("IDDobavljac");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Artikli_IDObjekat"
                ON "Artikli" ("IDObjekat");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Artikli_IDObjekat_IDDobavljac"
                ON "Artikli" ("IDObjekat", "IDDobavljac");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_prodaja_zaglavlje_data_origin_datum_prodaje";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_prodaja_zaglavlje_datum_prodaje_id_objekat";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_prodaja_stavke_id_artikal";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_prodaja_stavke_id_prodaja_id_artikal";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Artikli_DataOrigin";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Artikli_IDDobavljac";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Artikli_IDObjekat";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Artikli_IDObjekat_IDDobavljac";
                """);
        }
    }
}
