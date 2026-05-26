using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddAnalyticsActionOutcomeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueAtUtc",
                table: "analytics_action_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedImpactRsd",
                table: "analytics_action_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MeasuredImpactRsd",
                table: "analytics_action_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutcomeMeasuredAtUtc",
                table: "analytics_action_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeNotes",
                table: "analytics_action_items",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeStatus",
                table: "analytics_action_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueAtUtc",
                table: "analytics_action_items");

            migrationBuilder.DropColumn(
                name: "ExpectedImpactRsd",
                table: "analytics_action_items");

            migrationBuilder.DropColumn(
                name: "MeasuredImpactRsd",
                table: "analytics_action_items");

            migrationBuilder.DropColumn(
                name: "OutcomeMeasuredAtUtc",
                table: "analytics_action_items");

            migrationBuilder.DropColumn(
                name: "OutcomeNotes",
                table: "analytics_action_items");

            migrationBuilder.DropColumn(
                name: "OutcomeStatus",
                table: "analytics_action_items");
        }
    }
}
