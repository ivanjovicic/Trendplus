using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSalesToDnevnikPromena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate existing sales to DnevnikPromena
            migrationBuilder.Sql(@"
                INSERT INTO ""DnevnikPromena"" 
                    (""TipPromene"", ""Datum"", ""Iznos"", ""BrojRacuna"", ""Komentar"", ""KorisnikIme"")
                SELECT 
                    'Prodaja' as ""TipPromene"",
                    pz.datum_prodaje as ""Datum"",
                    COALESCE(SUM(ps.kolicina * ps.cena), 0) as ""Iznos"",
                    pz.broj_racuna as ""BrojRacuna"",
                    'Prodaja - ' || COALESCE(pz.broj_racuna, 'N/A') || ' (' || COALESCE(pz.nacin_placanja, 'Nepoznato') || ')' as ""Komentar"",
                    NULL as ""KorisnikIme""
                FROM prodaja_zaglavlje pz
                LEFT JOIN prodaja_stavke ps ON pz.id = ps.id_prodaja
                WHERE NOT EXISTS (
                    SELECT 1 
                    FROM ""DnevnikPromena"" dp 
                    WHERE dp.""TipPromene"" = 'Prodaja' 
                      AND dp.""BrojRacuna"" = pz.broj_racuna
                      AND dp.""Datum"" = pz.datum_prodaje
                )
                GROUP BY pz.id, pz.broj_racuna, pz.datum_prodaje, pz.nacin_placanja
                ORDER BY pz.datum_prodaje;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove migrated sales from DnevnikPromena
            migrationBuilder.Sql(@"
                DELETE FROM ""DnevnikPromena"" 
                WHERE ""TipPromene"" = 'Prodaja' 
                  AND ""Komentar"" LIKE 'Prodaja - %';
            ");
        }
    }
}
