using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddAnalyticsRefreshRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_refresh_runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    JobName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    RefreshedObjectsJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailedObjectsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TriggeredBy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProcessMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WorkerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_refresh_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_job_started",
                table: "analytics_refresh_runs",
                columns: new[] { "JobKey", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_started",
                table: "analytics_refresh_runs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_status",
                table: "analytics_refresh_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_worker",
                table: "analytics_refresh_runs",
                column: "WorkerName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_refresh_runs");
        }
    }
}
