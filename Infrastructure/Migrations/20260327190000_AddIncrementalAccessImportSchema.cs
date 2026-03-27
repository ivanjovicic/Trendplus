using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(TrendplusDbContext))]
    [Migration("20260327190000_AddIncrementalAccessImportSchema")]
    public partial class AddIncrementalAccessImportSchema_20260327190000 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "AccessImportCursors" (
                    "TableKey" character varying(128) NOT NULL,
                    "CursorMode" character varying(32) NOT NULL DEFAULT 'id',
                    "CursorTimestampUtc" timestamp with time zone NULL,
                    "CursorId" bigint NULL,
                    "CursorTieBreakerId" bigint NULL,
                    "OverlapSeconds" integer NOT NULL DEFAULT 60,
                    "LastSuccessfulBatchId" bigint NULL,
                    "LastRunStartedAtUtc" timestamp with time zone NULL,
                    "LastRunCompletedAtUtc" timestamp with time zone NULL,
                    "LeaseOwner" character varying(200) NULL,
                    "LeaseAcquiredAtUtc" timestamp with time zone NULL,
                    "LeaseExpiresAtUtc" timestamp with time zone NULL,
                    "LastError" character varying(2000) NULL,
                    "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                    CONSTRAINT "PK_AccessImportCursors" PRIMARY KEY ("TableKey")
                );

                CREATE INDEX IF NOT EXISTS "IX_AccessImportCursors_LastSuccessfulBatchId"
                ON "AccessImportCursors" ("LastSuccessfulBatchId");

                CREATE INDEX IF NOT EXISTS "IX_AccessImportCursors_LeaseExpiresAtUtc"
                ON "AccessImportCursors" ("LeaseExpiresAtUtc");

                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "IsIncremental" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "CursorSnapshot" jsonb NULL;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "ProcessedRowCount" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "SkippedRowCount" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsInserted" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsUpdated" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsUnchanged" integer NOT NULL DEFAULT 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: additive migration for production-safe rollout.
        }
    }
}
