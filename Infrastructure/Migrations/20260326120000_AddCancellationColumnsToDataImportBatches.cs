using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Infrastructure.Migrations
{
    [DbContext(typeof(TrendplusDbContext))]
    [Migration("20260326120000_AddCancellationColumnsToDataImportBatches")]
    public partial class AddCancellationColumnsToDataImportBatches : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS "DataImportBatches"
                ADD COLUMN IF NOT EXISTS "CancellationRequested" boolean NOT NULL DEFAULT FALSE;

                ALTER TABLE IF EXISTS "DataImportBatches"
                ADD COLUMN IF NOT EXISTS "CancellationRequestedAtUtc" timestamp with time zone;

                CREATE INDEX IF NOT EXISTS "IX_DataImportBatches_CancellationRequested"
                ON "DataImportBatches" ("CancellationRequested");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DataImportBatches_CancellationRequested";""");
            migrationBuilder.Sql("""ALTER TABLE IF EXISTS "DataImportBatches" DROP COLUMN IF EXISTS "CancellationRequestedAtUtc";""");
            migrationBuilder.Sql("""ALTER TABLE IF EXISTS "DataImportBatches" DROP COLUMN IF EXISTS "CancellationRequested";""");
        }
    }
}
