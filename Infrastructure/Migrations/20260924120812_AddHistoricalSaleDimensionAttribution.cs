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
            migrationBuilder.AddColumn<string>(
                name: "attribution_basis",
                table: "prodaja_stavke",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<int>(
                name: "shoe_type_id_at_sale",
                table: "prodaja_stavke",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "supplier_id_at_sale",
                table: "prodaja_stavke",
                type: "integer",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_prodaja_stavke_shoe_type_id_at_sale",
                table: "prodaja_stavke",
                column: "shoe_type_id_at_sale");

            migrationBuilder.CreateIndex(
                name: "IX_prodaja_stavke_supplier_id_at_sale",
                table: "prodaja_stavke",
                column: "supplier_id_at_sale");
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
