using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistOperationsAnalyticsIntegrityEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operations_analytics_integrity_evidence",
                columns: table => new
                {
                    evidence_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    checked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_verified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    trigger = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    failure_classification = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    tenant_scope = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    store_id = table.Column<int>(type: "integer", nullable: true),
                    data_scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    requested_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requested_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    database_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    app_commit = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    schema_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    contract_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    fixture_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cache_version = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    endpoint_or_live_revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    oracle_revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    revenue_delta = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    endpoint_or_live_units = table.Column<int>(type: "integer", nullable: true),
                    oracle_units = table.Column<int>(type: "integer", nullable: true),
                    units_delta = table.Column<int>(type: "integer", nullable: true),
                    deltas_json = table.Column<string>(type: "jsonb", nullable: true),
                    coverage_json = table.Column<string>(type: "jsonb", nullable: true),
                    blocks_decision_signals = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations_analytics_integrity_evidence", x => x.evidence_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operations_integrity_evidence_checked_status",
                table: "operations_analytics_integrity_evidence",
                columns: new[] { "checked_at_utc", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operations_analytics_integrity_evidence");
        }
    }
}
