using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeOperationsAnalyticsIntegrityEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "context_fingerprint",
                table: "operations_analytics_integrity_evidence",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "family",
                table: "operations_analytics_integrity_evidence",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "supplier_shoe_type");

            migrationBuilder.AddColumn<string>(
                name: "source_generation",
                table: "operations_analytics_integrity_evidence",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "context_fingerprint",
                table: "operations_analytics_integrity_evidence");

            migrationBuilder.DropColumn(
                name: "family",
                table: "operations_analytics_integrity_evidence");

            migrationBuilder.DropColumn(
                name: "source_generation",
                table: "operations_analytics_integrity_evidence");
        }
    }
}
