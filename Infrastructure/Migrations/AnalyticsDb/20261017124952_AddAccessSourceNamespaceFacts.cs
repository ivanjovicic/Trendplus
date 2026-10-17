using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddAccessSourceNamespaceFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No existing fact is assigned a namespace here.  Assigning it from an
            // Access filename, target sale ID, or content hash could collapse two
            // independent logical databases.  The importer writes provenance only
            // for future registered namespaces.
            migrationBuilder.Sql(
                """
                ALTER TABLE "SalesFacts" ADD COLUMN IF NOT EXISTS "SourceNamespace" character varying(128);
                ALTER TABLE "SalesLineFacts" ADD COLUMN IF NOT EXISTS "SourceNamespace" character varying(128);
                DROP INDEX IF EXISTS "UX_SalesLineFacts_SaleId_SourceLine";
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_SalesLineFacts_SaleId_SourceLine"
                    ON "SalesLineFacts" ("SourceNamespace", "SaleId", "SourceTableKey", "SourceLineId")
                    WHERE "SourceNamespace" IS NOT NULL AND "SourceLineId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Access fact namespace provenance is forward-only; restore the verified pre-migration snapshot for backout.");
        }
    }
}
