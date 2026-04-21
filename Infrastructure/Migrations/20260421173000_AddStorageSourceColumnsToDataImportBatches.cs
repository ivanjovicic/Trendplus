using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Infrastructure.Migrations
{
    [DbContext(typeof(TrendplusDbContext))]
    [Migration("20260421173000_AddStorageSourceColumnsToDataImportBatches")]
    public partial class AddStorageSourceColumnsToDataImportBatches : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS "DataImportBatches"
                ADD COLUMN IF NOT EXISTS "SourceStorageKey" character varying(1024);

                ALTER TABLE IF EXISTS "DataImportBatches"
                ADD COLUMN IF NOT EXISTS "SourceStorageProvider" character varying(32);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE IF EXISTS "DataImportBatches" DROP COLUMN IF EXISTS "SourceStorageProvider";""");
            migrationBuilder.Sql("""ALTER TABLE IF EXISTS "DataImportBatches" DROP COLUMN IF EXISTS "SourceStorageKey";""");
        }
    }
}
