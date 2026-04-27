using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(TrendplusDbContext))]
    [Migration("20260427153000_AddAccessImportLineageAndMetrics")]
    public partial class AddAccessImportLineageAndMetrics_20260427153000 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE IF EXISTS "AccessImportCursors" ADD COLUMN IF NOT EXISTS "SourceKey" character varying(256);
                ALTER TABLE IF EXISTS "AccessImportCursors" ADD COLUMN IF NOT EXISTS "CursorTimestampColumn" character varying(128);
                ALTER TABLE IF EXISTS "AccessImportCursors" ADD COLUMN IF NOT EXISTS "CursorIdColumn" character varying(128);
                ALTER TABLE IF EXISTS "AccessImportCursors" ADD COLUMN IF NOT EXISTS "LastRowsRead" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "AccessImportCursors" ADD COLUMN IF NOT EXISTS "LastRowsMerged" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "AccessImportCursors" ADD COLUMN IF NOT EXISTS "LastLagSeconds" integer;

                CREATE INDEX IF NOT EXISTS "IX_AccessImportCursors_TableKey_LeaseExpiresAtUtc"
                ON "AccessImportCursors" ("TableKey", "LeaseExpiresAtUtc");

                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "ImportStrategy" character varying(32) NOT NULL DEFAULT 'full';
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "CursorBeforeJson" jsonb;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "CursorAfterJson" jsonb;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsStaged" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsSkippedStale" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsRejected" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "ShadowMismatchCount" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "SourceFileHash" character varying(128);

                ALTER TABLE IF EXISTS "Artikli" ADD COLUMN IF NOT EXISTS "SourceTableKey" character varying(128);
                ALTER TABLE IF EXISTS "Artikli" ADD COLUMN IF NOT EXISTS "SourceRowId" bigint;
                ALTER TABLE IF EXISTS "Artikli" ADD COLUMN IF NOT EXISTS "SourceUpdatedAtUtc" timestamp with time zone;
                ALTER TABLE IF EXISTS "Artikli" ADD COLUMN IF NOT EXISTS "SourceHash" character varying(128);
                ALTER TABLE IF EXISTS "Artikli" ADD COLUMN IF NOT EXISTS "SourceBatchId" bigint;
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_Artikli_Access_SourceRow"
                ON "Artikli" ("DataOrigin", "SourceTableKey", "SourceRowId")
                WHERE "DataOrigin" = 'access' AND "SourceTableKey" IS NOT NULL AND "SourceRowId" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS "IX_Artikli_Access_SourceCursor"
                ON "Artikli" ("DataOrigin", "SourceTableKey", "SourceUpdatedAtUtc", "SourceRowId");

                ALTER TABLE IF EXISTS "DnevnikPromena" ADD COLUMN IF NOT EXISTS "SourceTableKey" character varying(128);
                ALTER TABLE IF EXISTS "DnevnikPromena" ADD COLUMN IF NOT EXISTS "SourceRowId" bigint;
                ALTER TABLE IF EXISTS "DnevnikPromena" ADD COLUMN IF NOT EXISTS "SourceUpdatedAtUtc" timestamp with time zone;
                ALTER TABLE IF EXISTS "DnevnikPromena" ADD COLUMN IF NOT EXISTS "SourceHash" character varying(128);
                ALTER TABLE IF EXISTS "DnevnikPromena" ADD COLUMN IF NOT EXISTS "SourceBatchId" bigint;
                CREATE INDEX IF NOT EXISTS "IX_DnevnikPromena_Access_SourceRow"
                ON "DnevnikPromena" ("DataOrigin", "SourceTableKey", "SourceRowId")
                WHERE "DataOrigin" = 'access' AND "SourceTableKey" IS NOT NULL AND "SourceRowId" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS "IX_DnevnikPromena_Access_SourceCursor"
                ON "DnevnikPromena" ("DataOrigin", "SourceTableKey", "SourceUpdatedAtUtc", "SourceRowId");

                ALTER TABLE IF EXISTS prodaja_zaglavlje ADD COLUMN IF NOT EXISTS source_table_key character varying(128);
                ALTER TABLE IF EXISTS prodaja_zaglavlje ADD COLUMN IF NOT EXISTS source_row_id bigint;
                ALTER TABLE IF EXISTS prodaja_zaglavlje ADD COLUMN IF NOT EXISTS source_updated_at_utc timestamp with time zone;
                ALTER TABLE IF EXISTS prodaja_zaglavlje ADD COLUMN IF NOT EXISTS source_hash character varying(128);
                ALTER TABLE IF EXISTS prodaja_zaglavlje ADD COLUMN IF NOT EXISTS source_batch_id bigint;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_prodaja_zaglavlje_access_source_row
                ON prodaja_zaglavlje (data_origin, source_table_key, source_row_id)
                WHERE data_origin = 'access' AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_prodaja_zaglavlje_access_source_cursor
                ON prodaja_zaglavlje (data_origin, source_table_key, source_updated_at_utc, source_row_id);

                ALTER TABLE IF EXISTS prodaja_stavke ADD COLUMN IF NOT EXISTS source_table_key character varying(128);
                ALTER TABLE IF EXISTS prodaja_stavke ADD COLUMN IF NOT EXISTS source_row_id bigint;
                ALTER TABLE IF EXISTS prodaja_stavke ADD COLUMN IF NOT EXISTS source_updated_at_utc timestamp with time zone;
                ALTER TABLE IF EXISTS prodaja_stavke ADD COLUMN IF NOT EXISTS source_hash character varying(128);
                ALTER TABLE IF EXISTS prodaja_stavke ADD COLUMN IF NOT EXISTS source_batch_id bigint;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_prodaja_stavke_source_row
                ON prodaja_stavke (source_table_key, source_row_id)
                WHERE source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_prodaja_stavke_source_cursor
                ON prodaja_stavke (source_table_key, source_updated_at_utc, source_row_id);

                ALTER TABLE IF EXISTS povracaj_zaglavlje ADD COLUMN IF NOT EXISTS source_table_key character varying(128);
                ALTER TABLE IF EXISTS povracaj_zaglavlje ADD COLUMN IF NOT EXISTS source_row_id bigint;
                ALTER TABLE IF EXISTS povracaj_zaglavlje ADD COLUMN IF NOT EXISTS source_updated_at_utc timestamp with time zone;
                ALTER TABLE IF EXISTS povracaj_zaglavlje ADD COLUMN IF NOT EXISTS source_hash character varying(128);
                ALTER TABLE IF EXISTS povracaj_zaglavlje ADD COLUMN IF NOT EXISTS source_batch_id bigint;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_povracaj_zaglavlje_access_source_row
                ON povracaj_zaglavlje (data_origin, source_table_key, source_row_id)
                WHERE data_origin = 'access' AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_povracaj_zaglavlje_access_source_cursor
                ON povracaj_zaglavlje (data_origin, source_table_key, source_updated_at_utc, source_row_id);

                ALTER TABLE IF EXISTS povracaj_stavke ADD COLUMN IF NOT EXISTS source_table_key character varying(128);
                ALTER TABLE IF EXISTS povracaj_stavke ADD COLUMN IF NOT EXISTS source_row_id bigint;
                ALTER TABLE IF EXISTS povracaj_stavke ADD COLUMN IF NOT EXISTS source_updated_at_utc timestamp with time zone;
                ALTER TABLE IF EXISTS povracaj_stavke ADD COLUMN IF NOT EXISTS source_hash character varying(128);
                ALTER TABLE IF EXISTS povracaj_stavke ADD COLUMN IF NOT EXISTS source_batch_id bigint;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_povracaj_stavke_source_row
                ON povracaj_stavke (source_table_key, source_row_id)
                WHERE source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_povracaj_stavke_source_cursor
                ON povracaj_stavke (source_table_key, source_updated_at_utc, source_row_id);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Production-safe additive migration: columns are intentionally retained on rollback.
        }
    }
}
