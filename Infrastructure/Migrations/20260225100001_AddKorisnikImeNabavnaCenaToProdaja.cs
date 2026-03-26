using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(TrendplusDbContext))]
    [Migration("20260225100001_AddKorisnikImeNabavnaCenaToProdaja")]
    public partial class AddKorisnikImeNabavnaCenaToProdaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE prodaja_zaglavlje
                    ADD COLUMN IF NOT EXISTS ""korisnik_ime"" character varying(200);

                ALTER TABLE prodaja_stavke
                    ADD COLUMN IF NOT EXISTS ""nabavna_cena"" decimal(18,2);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE prodaja_zaglavlje
                    DROP COLUMN IF EXISTS ""korisnik_ime"";

                ALTER TABLE prodaja_stavke
                    DROP COLUMN IF EXISTS ""nabavna_cena"";
            ");
        }
    }
}
