using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20260521123000_AddAnalyticsActionIndexes")]
    public partial class AddAnalyticsActionIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_sourcekey_status",
                table: "analytics_action_items",
                columns: new[] { "SourceType", "SourceKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "idx_analytics_action_priority_status",
                table: "analytics_action_items",
                columns: new[] { "Priority", "Status" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_analytics_action_sourcekey_status",
                table: "analytics_action_items");

            migrationBuilder.DropIndex(
                name: "idx_analytics_action_priority_status",
                table: "analytics_action_items");
        }
    }
}
