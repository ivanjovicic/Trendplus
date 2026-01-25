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
            migrationBuilder.AddColumn<string>(
                name: "Boja",
                table: "Artikli",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kategorija",
                table: "Artikli",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimalnaKolicina",
                table: "Artikli",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pol",
                table: "Artikli",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Velicina",
                table: "Artikli",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Boja",
                table: "Artikli");

            migrationBuilder.DropColumn(
                name: "Kategorija",
                table: "Artikli");

            migrationBuilder.DropColumn(
                name: "MinimalnaKolicina",
                table: "Artikli");

            migrationBuilder.DropColumn(
                name: "Pol",
                table: "Artikli");

            migrationBuilder.DropColumn(
                name: "Velicina",
                table: "Artikli");
        }
    }
}
