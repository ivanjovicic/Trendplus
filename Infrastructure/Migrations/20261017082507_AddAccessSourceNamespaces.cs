using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Adds explicit, operator-registered Access logical-source provenance.
    /// This migration deliberately does not infer a namespace for historical
    /// rows or cursors: an inferred namespace could join separate databases.
    /// </summary>
    public partial class AddAccessSourceNamespaces : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "SourceNamespace" character varying(128);
                ALTER TABLE IF EXISTS "Artikli" ADD COLUMN IF NOT EXISTS "SourceNamespace" character varying(128);
                ALTER TABLE IF EXISTS "DnevnikPromena" ADD COLUMN IF NOT EXISTS "SourceNamespace" character varying(128);
                ALTER TABLE IF EXISTS prodaja_zaglavlje ADD COLUMN IF NOT EXISTS source_namespace character varying(128);
                ALTER TABLE IF EXISTS prodaja_stavke ADD COLUMN IF NOT EXISTS source_namespace character varying(128);
                ALTER TABLE IF EXISTS povracaj_zaglavlje ADD COLUMN IF NOT EXISTS source_namespace character varying(128);
                ALTER TABLE IF EXISTS povracaj_stavke ADD COLUMN IF NOT EXISTS source_namespace character varying(128);

                DROP INDEX IF EXISTS "UX_Artikli_Access_SourceRow";
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_Artikli_Access_SourceNamespace_Row"
                    ON "Artikli" ("DataOrigin", "SourceNamespace", "SourceTableKey", "SourceRowId")
                    WHERE "DataOrigin" = 'access' AND "SourceNamespace" IS NOT NULL AND "SourceTableKey" IS NOT NULL AND "SourceRowId" IS NOT NULL;

                DROP INDEX IF EXISTS "IX_DnevnikPromena_Access_SourceRow";
                DROP INDEX IF EXISTS "UX_DnevnikPromena_Access_SourceNamespace_Row";
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_DnevnikPromena_Access_SourceNamespace_Row"
                    ON "DnevnikPromena" ("DataOrigin", "SourceNamespace", "SourceTableKey", "SourceRowId", "TipPromene")
                    WHERE "DataOrigin" = 'access' AND "SourceNamespace" IS NOT NULL AND "SourceTableKey" IS NOT NULL AND "SourceRowId" IS NOT NULL;

                DROP INDEX IF EXISTS ux_prodaja_zaglavlje_access_source_row;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_prodaja_zaglavlje_access_source_namespace_row
                    ON prodaja_zaglavlje (data_origin, source_namespace, source_table_key, source_row_id)
                    WHERE data_origin = 'access' AND source_namespace IS NOT NULL AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL;

                DROP INDEX IF EXISTS ux_prodaja_stavke_source_row;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_prodaja_stavke_source_namespace_row
                    ON prodaja_stavke (source_namespace, source_table_key, source_row_id)
                    WHERE source_namespace IS NOT NULL AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL;

                DROP INDEX IF EXISTS ux_povracaj_zaglavlje_access_source_row;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_povracaj_zaglavlje_access_source_namespace_row
                    ON povracaj_zaglavlje (data_origin, source_namespace, source_table_key, source_row_id)
                    WHERE data_origin = 'access' AND source_namespace IS NOT NULL AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL;

                DROP INDEX IF EXISTS ux_povracaj_stavke_source_row;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_povracaj_stavke_source_namespace_row
                    ON povracaj_stavke (source_namespace, source_table_key, source_row_id)
                    WHERE source_namespace IS NOT NULL AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                DO $access_cursor_namespace$
                BEGIN
                    IF to_regclass('"AccessImportCursors"') IS NOT NULL
                       AND EXISTS (SELECT 1 FROM "AccessImportCursors") THEN
                        RAISE EXCEPTION 'access cursor namespace migration requires an empty cursor table; register and replay namespaces explicitly instead of guessing legacy ownership';
                    END IF;
                END $access_cursor_namespace$;

                ALTER TABLE "AccessImportCursors" ADD COLUMN IF NOT EXISTS "SourceNamespace" character varying(128);
                ALTER TABLE "AccessImportCursors" ALTER COLUMN "SourceNamespace" SET NOT NULL;
                ALTER TABLE "AccessImportCursors" DROP CONSTRAINT IF EXISTS "PK_AccessImportCursors";
                ALTER TABLE "AccessImportCursors" ADD CONSTRAINT "PK_AccessImportCursors" PRIMARY KEY ("SourceNamespace", "TableKey");
                DROP INDEX IF EXISTS "IX_AccessImportCursors_TableKey_LeaseExpiresAtUtc";
                CREATE INDEX IF NOT EXISTS "IX_AccessImportCursors_SourceNamespace_TableKey_LeaseExpiresAtUtc"
                    ON "AccessImportCursors" ("SourceNamespace", "TableKey", "LeaseExpiresAtUtc");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Access source namespace provenance is forward-only; use the documented backup restore backout path.");
        }
    }
}
