using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddAnalyticsActionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductsDim_ProductId",
                table: "ProductsDim");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovementFacts_SourceId",
                table: "InventoryMovementFacts");

            migrationBuilder.CreateTable(
                name: "analytics_action_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecommendationStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Priority = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ImpactEstimateRsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ConfidencePct = table.Column<int>(type: "integer", nullable: true),
                    ReliabilityPct = table.Column<int>(type: "integer", nullable: true),
                    DataQualityStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActionUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_action_items", x => x.Id);
                });

            // NOTE: ReturnFacts table is managed by DatabaseInitializer (raw SQL CREATE TABLE IF NOT EXISTS).
            // It already exists in the database; EF does not own its lifecycle.

            migrationBuilder.CreateIndex(
                name: "IX_ProductsDim_ProductId",
                table: "ProductsDim",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementFacts_SourceId_DataOrigin",
                table: "InventoryMovementFacts",
                columns: new[] { "SourceId", "DataOrigin" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_priority",
                table: "analytics_action_items",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_source_created",
                table: "analytics_action_items",
                columns: new[] { "SourceType", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_sourcekey_open",
                table: "analytics_action_items",
                columns: new[] { "SourceType", "SourceKey" },
                unique: true,
                filter: "\"Status\" IN ('new', 'accepted', 'deferred')");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_status",
                table: "analytics_action_items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_status_updated",
                table: "analytics_action_items",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_updated",
                table: "analytics_action_items",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_action_items");

            // NOTE: ReturnFacts is managed by DatabaseInitializer, not EF migrations.

            migrationBuilder.DropIndex(
                name: "IX_ProductsDim_ProductId",
                table: "ProductsDim");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovementFacts_SourceId_DataOrigin",
                table: "InventoryMovementFacts");

            migrationBuilder.CreateIndex(
                name: "IX_ProductsDim_ProductId",
                table: "ProductsDim",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementFacts_SourceId",
                table: "InventoryMovementFacts",
                column: "SourceId",
                unique: true);
        }
    }
}
