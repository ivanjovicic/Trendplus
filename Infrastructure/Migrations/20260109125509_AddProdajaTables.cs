using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProdajaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prodaja_zaglavlje",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BrojRacuna = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DatumProdaje = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NacinPlacanja = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IDObjekat = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prodaja_zaglavlje", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prodaja_stavke",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdProdaja = table.Column<int>(type: "integer", nullable: false),
                    IdArtikal = table.Column<int>(type: "integer", nullable: false),
                    Kolicina = table.Column<int>(type: "integer", nullable: false),
                    Cena = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prodaja_stavke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prodaja_stavke_prodaja_zaglavlje_IdProdaja",
                        column: x => x.IdProdaja,
                        principalTable: "prodaja_zaglavlje",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prodaja_stavke_IdProdaja",
                table: "prodaja_stavke",
                column: "IdProdaja");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prodaja_stavke");

            migrationBuilder.DropTable(
                name: "prodaja_zaglavlje");
        }
    }
}
