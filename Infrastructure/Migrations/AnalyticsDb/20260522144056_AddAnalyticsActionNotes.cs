using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddAnalyticsActionNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_action_notes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActionItemId = table.Column<long>(type: "bigint", nullable: false),
                    StatusFrom = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StatusTo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedByUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_action_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_analytics_action_notes_analytics_action_items_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "analytics_action_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_notes_action_created",
                table: "analytics_action_notes",
                columns: new[] { "ActionItemId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_notes_action_item",
                table: "analytics_action_notes",
                column: "ActionItemId");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_notes_created",
                table: "analytics_action_notes",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_action_notes");
        }
    }
}
