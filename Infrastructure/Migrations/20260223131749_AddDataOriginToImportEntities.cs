using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddDataOriginToImportEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataOrigin",
                table: "DnevnikPromena",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "existing");

            migrationBuilder.AddColumn<string>(
                name: "data_origin",
                table: "povracaj_zaglavlje",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "existing");

            migrationBuilder.CreateIndex(
                name: "IX_DnevnikPromena_DataOrigin",
                table: "DnevnikPromena",
                column: "DataOrigin");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DnevnikPromena_DataOrigin",
                table: "DnevnikPromena");

            migrationBuilder.DropColumn(
                name: "DataOrigin",
                table: "DnevnikPromena");

            migrationBuilder.DropColumn(
                name: "data_origin",
                table: "povracaj_zaglavlje");
        }
    }
}
