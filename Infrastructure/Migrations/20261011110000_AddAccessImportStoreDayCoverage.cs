using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.DbContexts;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(TrendplusDbContext))]
[Migration("20261011110000_AddAccessImportStoreDayCoverage")]
public sealed class AddAccessImportStoreDayCoverage : Migration
{
    public const string CreateSql = """
        CREATE TABLE access_import_store_day_coverage (
            source_batch_id bigint NOT NULL,
            store_id integer NULL,
            business_date date NOT NULL,
            business_timezone character varying(64) NOT NULL,
            coverage_status character varying(24) NOT NULL,
            source_scope_fingerprint character varying(64) NOT NULL,
            source_generation character varying(128) NOT NULL,
            source_checkpoint_hash character varying(64) NULL,
            source_header_table character varying(128) NOT NULL,
            source_line_table character varying(128) NULL,
            receipt_count integer NOT NULL DEFAULT 0,
            sale_line_count integer NOT NULL DEFAULT 0,
            source_header_rows integer NOT NULL DEFAULT 0,
            accepted_header_rows integer NOT NULL DEFAULT 0,
            source_line_rows integer NOT NULL DEFAULT 0,
            accepted_line_rows integer NOT NULL DEFAULT 0,
            reason_code character varying(64) NULL,
            captured_at_utc timestamp with time zone NOT NULL,
            CONSTRAINT "FK_AccessImportStoreDayCoverage_DataImportBatches_SourceBatchId"
                FOREIGN KEY (source_batch_id) REFERENCES "DataImportBatches" ("Id") ON DELETE CASCADE,
            CONSTRAINT "CK_AccessImportStoreDayCoverage_Status"
                CHECK (coverage_status IN ('observed_sales', 'verified_empty', 'partial', 'failed', 'unverified')),
            CONSTRAINT "CK_AccessImportStoreDayCoverage_Counts"
                CHECK (receipt_count >= 0 AND sale_line_count >= 0
                    AND source_header_rows >= 0 AND accepted_header_rows >= 0
                    AND source_line_rows >= 0 AND accepted_line_rows >= 0)
        );
        CREATE UNIQUE INDEX "IX_AccessImportStoreDayCoverage_batch_store_day"
            ON access_import_store_day_coverage (source_batch_id, store_id, business_date)
            WHERE store_id IS NOT NULL;
        CREATE UNIQUE INDEX "IX_AccessImportStoreDayCoverage_batch_unknown_store_day"
            ON access_import_store_day_coverage (source_batch_id, business_date)
            WHERE store_id IS NULL;
        CREATE INDEX "IX_AccessImportStoreDayCoverage_scope_status"
            ON access_import_store_day_coverage (store_id, business_date, coverage_status);
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(CreateSql);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("DROP TABLE IF EXISTS access_import_store_day_coverage;");
}
