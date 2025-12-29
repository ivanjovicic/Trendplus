using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceChangeFieldsToDnevnikPromena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArtikalId",
                table: "DnevnikPromena",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NovaProdajnaCena",
                table: "DnevnikPromena",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StaraProdajnaCena",
                table: "DnevnikPromena",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtikalId",
                table: "DnevnikPromena");

            migrationBuilder.DropColumn(
                name: "NovaProdajnaCena",
                table: "DnevnikPromena");

            migrationBuilder.DropColumn(
                name: "StaraProdajnaCena",
                table: "DnevnikPromena");
        }
    }
}
