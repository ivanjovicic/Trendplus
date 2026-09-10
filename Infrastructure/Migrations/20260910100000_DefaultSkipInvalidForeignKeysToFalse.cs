using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(TrendplusDbContext))]
    [Migration("20260910100000_DefaultSkipInvalidForeignKeysToFalse")]
    public partial class DefaultSkipInvalidForeignKeysToFalse_20260910100000 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS "DataImportBatches"
                    ALTER COLUMN "SkipInvalidForeignKeys" SET DEFAULT FALSE;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS "DataImportBatches"
                    ALTER COLUMN "SkipInvalidForeignKeys" SET DEFAULT TRUE;
                """);
        }
    }
}
