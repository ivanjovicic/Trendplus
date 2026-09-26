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
                -- Reconcile columns expected by the current model before
                -- creating the dependent indexes. Older versions of
                -- AddDataOriginToImportEntities recorded only a subset of
                -- these columns, so this must be safe on both clean and
                -- partially upgraded databases.
                ALTER TABLE IF EXISTS "Artikli"
                    ADD COLUMN IF NOT EXISTS "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing';
                ALTER TABLE IF EXISTS "Dobavljaci"
                    ADD COLUMN IF NOT EXISTS "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing';
                ALTER TABLE IF EXISTS "Sezone"
                    ADD COLUMN IF NOT EXISTS "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing';
                ALTER TABLE IF EXISTS "TipoviObuce"
                    ADD COLUMN IF NOT EXISTS "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing';
                ALTER TABLE IF EXISTS "prodaja_zaglavlje"
                    ADD COLUMN IF NOT EXISTS "data_origin" character varying(32) NOT NULL DEFAULT 'existing';

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
