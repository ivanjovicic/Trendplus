using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddAnalyticsRefreshRunCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_started",
                table: "analytics_refresh_runs");

            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_status",
                table: "analytics_refresh_runs");

            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_worker",
                table: "analytics_refresh_runs");

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_status_started",
                table: "analytics_refresh_runs",
                columns: new[] { "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_refresh_runs_worker_started",
                table: "analytics_refresh_runs",
                columns: new[] { "WorkerName", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_status_started",
                table: "analytics_refresh_runs");

            migrationBuilder.DropIndex(
                name: "idx_analytics_refresh_runs_worker_started",
                table: "analytics_refresh_runs");

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
    }
}
