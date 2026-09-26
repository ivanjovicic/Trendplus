using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImagePathToArtikli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kategorija, Pol, Velicina and Boja are owned by the preceding
            // AddArtikliKategorije migration. This migration was generated
            // against a model that already contained those columns and must
            // not try to add them a second time on a clean database.
            migrationBuilder.Sql(
                "ALTER TABLE \"Artikli\" ADD COLUMN IF NOT EXISTS \"MinimalnaKolicina\" integer;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimalnaKolicina",
                table: "Artikli");
        }
    }
}
