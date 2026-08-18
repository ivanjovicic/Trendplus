using System;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(TrendplusDbContext))]
    [Migration("20260818120000_AddSourceSyncCheckpoints")]
    public partial class AddSourceSyncCheckpoints_20260818120000 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "SourceSyncCheckpoints" (
                    "ConnectionId" character varying(128) NOT NULL,
                    "MappingProfileId" character varying(64) NOT NULL,
                    "SourceStream" character varying(128) NOT NULL,
                    "CursorMode" character varying(32) NOT NULL DEFAULT 'id',
                    "CursorTimestampUtc" timestamp with time zone NULL,
                    "ExternalKeyTieBreaker" character varying(256) NULL,
                    "OverlapSeconds" integer NOT NULL DEFAULT 60,
                    "SchemaFingerprint" character varying(80) NULL,
                    "LastStartedBatchId" uuid NULL,
                    "LastCompletedBatchId" uuid NULL,
                    "LastSuccessfulSyncUtc" timestamp with time zone NULL,
                    "FailureCategory" character varying(64) NULL,
                    "LastError" character varying(2000) NULL,
                    "TenantScope" character varying(32) NOT NULL DEFAULT 'n/a_dedicated',
                    "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                    CONSTRAINT "PK_SourceSyncCheckpoints" PRIMARY KEY ("ConnectionId", "MappingProfileId", "SourceStream")
                );

                CREATE TABLE IF NOT EXISTS "SourceSyncAppliedRows" (
                    "ConnectionId" character varying(128) NOT NULL,
                    "MappingProfileId" character varying(64) NOT NULL,
                    "SourceStream" character varying(128) NOT NULL,
                    "ExternalKey" character varying(256) NOT NULL,
                    "PayloadHash" character varying(80) NOT NULL,
                    "CursorTimestampUtc" timestamp with time zone NULL,
                    "LastBatchId" uuid NOT NULL,
                    "ApplyStatus" character varying(16) NOT NULL,
                    "RejectionReason" character varying(64) NULL,
                    "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                    CONSTRAINT "PK_SourceSyncAppliedRows" PRIMARY KEY ("ConnectionId", "MappingProfileId", "SourceStream", "ExternalKey")
                );

                CREATE INDEX IF NOT EXISTS "IX_SourceSyncCheckpoints_FailureCategory"
                ON "SourceSyncCheckpoints" ("FailureCategory");

                CREATE INDEX IF NOT EXISTS "IX_SourceSyncAppliedRows_LastBatchId"
                ON "SourceSyncAppliedRows" ("LastBatchId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TABLE IF EXISTS "SourceSyncAppliedRows";
                DROP TABLE IF EXISTS "SourceSyncCheckpoints";
                """);
        }
    }
}
