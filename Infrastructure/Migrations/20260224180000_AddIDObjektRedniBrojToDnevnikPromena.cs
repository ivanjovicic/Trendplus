using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIDObjektRedniBrojToDnevnikPromena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""DnevnikPromena""
                    ADD COLUMN IF NOT EXISTS ""IDObjekat"" integer,
                    ADD COLUMN IF NOT EXISTS ""RedniBroj"" integer;

                CREATE INDEX IF NOT EXISTS ""IX_DnevnikPromena_IDObjekat_Datum""
                    ON ""DnevnikPromena"" (""IDObjekat"", ""Datum"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_DnevnikPromena_IDObjekat_Datum"";
                ALTER TABLE ""DnevnikPromena""
                    DROP COLUMN IF EXISTS ""IDObjekat"",
                    DROP COLUMN IF EXISTS ""RedniBroj"";
            ");
        }
    }
}
