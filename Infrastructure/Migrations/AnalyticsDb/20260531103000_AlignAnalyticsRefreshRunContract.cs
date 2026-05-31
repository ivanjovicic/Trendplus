using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AlignAnalyticsRefreshRunContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_job_started",
                table: "analytics_refresh_runs");

            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_status_started",
                table: "analytics_refresh_runs");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "analytics_refresh_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "analytics_refresh_runs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "JobKey",
                table: "analytics_refresh_runs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ProcessMode",
                table: "analytics_refresh_runs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "TriggeredBy",
                table: "analytics_refresh_runs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "WorkerName",
                table: "analytics_refresh_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_created_at",
                table: "analytics_refresh_runs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_job_started",
                table: "analytics_refresh_runs",
                columns: new[] { "JobKey", "StartedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_status",
                table: "analytics_refresh_runs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_created_at",
                table: "analytics_refresh_runs");

            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_job_started",
                table: "analytics_refresh_runs");

            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_status",
                table: "analytics_refresh_runs");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "analytics_refresh_runs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "analytics_refresh_runs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "JobKey",
                table: "analytics_refresh_runs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "ProcessMode",
                table: "analytics_refresh_runs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "TriggeredBy",
                table: "analytics_refresh_runs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "WorkerName",
                table: "analytics_refresh_runs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_job_started",
                table: "analytics_refresh_runs",
                columns: new[] { "JobKey", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_status_started",
                table: "analytics_refresh_runs",
                columns: new[] { "Status", "StartedAtUtc" });
        }
    }
}
