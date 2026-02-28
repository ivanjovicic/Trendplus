using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddTrendMomentumEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "inventory_recommendations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    product_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sales_velocity = table.Column<double>(type: "double precision", nullable: false),
                    stock_on_hand = table.Column<double>(type: "double precision", nullable: false),
                    trend_score = table.Column<double>(type: "double precision", nullable: false),
                    momentum_score = table.Column<double>(type: "double precision", nullable: false),
                    recommended_qty = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_recommendations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trend_product_momentum",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    canonical_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    momentum_score = table.Column<double>(type: "double precision", nullable: false),
                    score_delta = table.Column<double>(type: "double precision", nullable: false),
                    rank_delta = table.Column<int>(type: "integer", nullable: false),
                    is_new_entry = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trend_product_momentum", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trend_product_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    canonical_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    product_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    market = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    rank_global = table.Column<int>(type: "integer", nullable: false),
                    social_score = table.Column<double>(type: "double precision", nullable: true),
                    source_count = table.Column<int>(type: "integer", nullable: false),
                    unique_sources = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trend_product_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trendplus_index",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scope_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    index_value = table.Column<double>(type: "double precision", nullable: false),
                    base_component = table.Column<double>(type: "double precision", nullable: false),
                    momentum_component = table.Column<double>(type: "double precision", nullable: false),
                    social_component = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trendplus_index", x => x.id);
                });


            migrationBuilder.CreateIndex(
                name: "idx_inv_rec_date",
                table: "inventory_recommendations",
                column: "snapshot_date");

            migrationBuilder.CreateIndex(
                name: "idx_inv_rec_product",
                table: "inventory_recommendations",
                columns: new[] { "product_id", "snapshot_date" });

            migrationBuilder.CreateIndex(
                name: "idx_trend_momentum_date_key",
                table: "trend_product_momentum",
                columns: new[] { "snapshot_date", "canonical_key" });

            migrationBuilder.CreateIndex(
                name: "idx_trend_snapshots_key_date",
                table: "trend_product_snapshots",
                columns: new[] { "canonical_key", "snapshot_date" });

            migrationBuilder.CreateIndex(
                name: "idx_trendplus_index_date",
                table: "trendplus_index",
                column: "snapshot_date");

            migrationBuilder.CreateIndex(
                name: "idx_trendplus_index_scope_date",
                table: "trendplus_index",
                columns: new[] { "scope_type", "scope_value", "snapshot_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_recommendations");

            migrationBuilder.DropTable(
                name: "trend_product_momentum");

            migrationBuilder.DropTable(
                name: "trend_product_snapshots");

            migrationBuilder.DropTable(
                name: "trendplus_index");
        }
    }
}
