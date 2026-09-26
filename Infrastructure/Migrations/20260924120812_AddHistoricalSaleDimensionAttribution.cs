using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalSaleDimensionAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DatabaseInitializer creates these compatibility columns before EF
            // migrations run. Keep the migration safe for both a raw EF database
            // and the production bootstrap path.
            migrationBuilder.Sql(
                """
                ALTER TABLE prodaja_stavke
                    ADD COLUMN IF NOT EXISTS attribution_basis character varying(64) NOT NULL DEFAULT 'unknown',
                    ADD COLUMN IF NOT EXISTS shoe_type_id_at_sale integer,
                    ADD COLUMN IF NOT EXISTS supplier_id_at_sale integer;
                """);

            // Legacy rows are frozen from the current article master only once. This is
            // explicitly estimated provenance, never a claim of sale-time truth.
            migrationBuilder.Sql(
                """
                UPDATE prodaja_stavke ps
                SET supplier_id_at_sale = a."IDDobavljac",
                    shoe_type_id_at_sale = a."IDTipObuce",
                    attribution_basis = 'frozen_current_master_backfill'
                FROM "Artikli" a
                WHERE ps.id_artikal = a."Id"
                  AND COALESCE(ps.attribution_basis, 'unknown') = 'unknown';
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_prodaja_stavke_shoe_type_id_at_sale"
                    ON prodaja_stavke (shoe_type_id_at_sale);
                CREATE INDEX IF NOT EXISTS "IX_prodaja_stavke_supplier_id_at_sale"
                    ON prodaja_stavke (supplier_id_at_sale);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prodaja_stavke_shoe_type_id_at_sale",
                table: "prodaja_stavke");

            migrationBuilder.DropIndex(
                name: "IX_prodaja_stavke_supplier_id_at_sale",
                table: "prodaja_stavke");

            migrationBuilder.DropColumn(
                name: "attribution_basis",
                table: "prodaja_stavke");

            migrationBuilder.DropColumn(
                name: "shoe_type_id_at_sale",
                table: "prodaja_stavke");

            migrationBuilder.DropColumn(
                name: "supplier_id_at_sale",
                table: "prodaja_stavke");
        }
    }
}
