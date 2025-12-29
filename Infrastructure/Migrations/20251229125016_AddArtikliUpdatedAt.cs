using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArtikliUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Artikli",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "DnevnikPromena",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipPromene = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Datum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Iznos = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BrojRacuna = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DobavljacId = table.Column<int>(type: "integer", nullable: true),
                    Komentar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    KorisnikIme = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DnevnikPromena", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DnevnikPromena");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Artikli");
        }
    }
}
