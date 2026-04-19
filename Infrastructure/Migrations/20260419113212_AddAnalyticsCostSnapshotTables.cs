using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsCostSnapshotTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_cost_snapshot_batches",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "access_origin"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deactivated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "system"),
                    description = table.Column<string>(type: "text", nullable: true),
                    row_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_revenue_covered = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    coverage_pct = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    no_cost_pct = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    generation_duration_ms = table.Column<int>(type: "integer", nullable: true),
                    dry_run = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_cost_snapshot_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_sale_line_cost_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    batch_id = table.Column<long>(type: "bigint", nullable: false),
                    prodaja_stavka_id = table.Column<int>(type: "integer", nullable: false),
                    resolved_unit_cost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    cost_source = table.Column<short>(type: "smallint", nullable: false),
                    product_cost_rsd_at_snapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    product_cost_legacy_at_snapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    artikal_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_sale_line_cost_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_analytics_sale_line_cost_snapshots_analytics_cost_snapshot_~",
                        column: x => x.batch_id,
                        principalTable: "analytics_cost_snapshot_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_batches_status",
                table: "analytics_cost_snapshot_batches",
                columns: new[] { "status", "scope" });

            migrationBuilder.CreateIndex(
                name: "ux_snapshot_batches_active_scope",
                table: "analytics_cost_snapshot_batches",
                column: "scope",
                unique: true,
                filter: "\"status\" = 'active'");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_lines_batch_source",
                table: "analytics_sale_line_cost_snapshots",
                columns: new[] { "batch_id", "cost_source" });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_lines_stavka",
                table: "analytics_sale_line_cost_snapshots",
                column: "prodaja_stavka_id");

            migrationBuilder.CreateIndex(
                name: "ux_snapshot_lines_batch_stavka",
                table: "analytics_sale_line_cost_snapshots",
                columns: new[] { "batch_id", "prodaja_stavka_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_sale_line_cost_snapshots");

            migrationBuilder.DropTable(
                name: "analytics_cost_snapshot_batches");
        }
    }
}
