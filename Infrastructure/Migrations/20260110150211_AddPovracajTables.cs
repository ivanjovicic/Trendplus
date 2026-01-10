using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPovracajTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "povracaj_zaglavlje",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    broj_zapisnika = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    datum_povracaja = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_dobavljac = table.Column<int>(type: "integer", nullable: false),
                    razlog_povracaja = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ukupan_iznos = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    komentar = table.Column<string>(type: "text", nullable: true),
                    kreirao_korisnik = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    odobrio_korisnik = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    datum_kreiranja = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    datum_odobrenja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_povracaj_zaglavlje", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "povracaj_stavke",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_povracaj = table.Column<int>(type: "integer", nullable: false),
                    id_artikal = table.Column<int>(type: "integer", nullable: false),
                    kolicina = table.Column<int>(type: "integer", nullable: false),
                    cena = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    razlog = table.Column<string>(type: "text", nullable: true),
                    stanje_artikla = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_povracaj_stavke", x => x.id);
                    table.ForeignKey(
                        name: "FK_povracaj_stavke_povracaj_zaglavlje_id_povracaj",
                        column: x => x.id_povracaj,
                        principalTable: "povracaj_zaglavlje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_povracaj_stavke_id_artikal",
                table: "povracaj_stavke",
                column: "id_artikal");

            migrationBuilder.CreateIndex(
                name: "IX_povracaj_stavke_id_povracaj",
                table: "povracaj_stavke",
                column: "id_povracaj");

            migrationBuilder.CreateIndex(
                name: "IX_povracaj_zaglavlje_broj_zapisnika",
                table: "povracaj_zaglavlje",
                column: "broj_zapisnika",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_povracaj_zaglavlje_datum_povracaja",
                table: "povracaj_zaglavlje",
                column: "datum_povracaja");

            migrationBuilder.CreateIndex(
                name: "IX_povracaj_zaglavlje_id_dobavljac",
                table: "povracaj_zaglavlje",
                column: "id_dobavljac");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "povracaj_stavke");

            migrationBuilder.DropTable(
                name: "povracaj_zaglavlje");
        }
    }
}
