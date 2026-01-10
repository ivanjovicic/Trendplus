using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixProdajaColumnMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prodaja_stavke_prodaja_zaglavlje_IdProdaja",
                table: "prodaja_stavke");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "prodaja_zaglavlje",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "NacinPlacanja",
                table: "prodaja_zaglavlje",
                newName: "nacin_placanja");

            migrationBuilder.RenameColumn(
                name: "IDObjekat",
                table: "prodaja_zaglavlje",
                newName: "id_objekat");

            migrationBuilder.RenameColumn(
                name: "DatumProdaje",
                table: "prodaja_zaglavlje",
                newName: "datum_prodaje");

            migrationBuilder.RenameColumn(
                name: "BrojRacuna",
                table: "prodaja_zaglavlje",
                newName: "broj_racuna");

            migrationBuilder.RenameColumn(
                name: "Kolicina",
                table: "prodaja_stavke",
                newName: "kolicina");

            migrationBuilder.RenameColumn(
                name: "Cena",
                table: "prodaja_stavke",
                newName: "cena");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "prodaja_stavke",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "IdProdaja",
                table: "prodaja_stavke",
                newName: "id_prodaja");

            migrationBuilder.RenameColumn(
                name: "IdArtikal",
                table: "prodaja_stavke",
                newName: "id_artikal");

            migrationBuilder.RenameIndex(
                name: "IX_prodaja_stavke_IdProdaja",
                table: "prodaja_stavke",
                newName: "IX_prodaja_stavke_id_prodaja");

            migrationBuilder.AddForeignKey(
                name: "FK_prodaja_stavke_prodaja_zaglavlje_id_prodaja",
                table: "prodaja_stavke",
                column: "id_prodaja",
                principalTable: "prodaja_zaglavlje",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prodaja_stavke_prodaja_zaglavlje_id_prodaja",
                table: "prodaja_stavke");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "prodaja_zaglavlje",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "nacin_placanja",
                table: "prodaja_zaglavlje",
                newName: "NacinPlacanja");

            migrationBuilder.RenameColumn(
                name: "id_objekat",
                table: "prodaja_zaglavlje",
                newName: "IDObjekat");

            migrationBuilder.RenameColumn(
                name: "datum_prodaje",
                table: "prodaja_zaglavlje",
                newName: "DatumProdaje");

            migrationBuilder.RenameColumn(
                name: "broj_racuna",
                table: "prodaja_zaglavlje",
                newName: "BrojRacuna");

            migrationBuilder.RenameColumn(
                name: "kolicina",
                table: "prodaja_stavke",
                newName: "Kolicina");

            migrationBuilder.RenameColumn(
                name: "cena",
                table: "prodaja_stavke",
                newName: "Cena");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "prodaja_stavke",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "id_prodaja",
                table: "prodaja_stavke",
                newName: "IdProdaja");

            migrationBuilder.RenameColumn(
                name: "id_artikal",
                table: "prodaja_stavke",
                newName: "IdArtikal");

            migrationBuilder.RenameIndex(
                name: "IX_prodaja_stavke_id_prodaja",
                table: "prodaja_stavke",
                newName: "IX_prodaja_stavke_IdProdaja");

            migrationBuilder.AddForeignKey(
                name: "FK_prodaja_stavke_prodaja_zaglavlje_IdProdaja",
                table: "prodaja_stavke",
                column: "IdProdaja",
                principalTable: "prodaja_zaglavlje",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
