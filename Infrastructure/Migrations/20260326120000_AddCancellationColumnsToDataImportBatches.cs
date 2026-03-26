using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddCancellationColumnsToDataImportBatches : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancellationRequested",
                table: "DataImportBatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationRequestedAtUtc",
                table: "DataImportBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataImportBatches_CancellationRequested",
                table: "DataImportBatches",
                column: "CancellationRequested");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataImportBatches_CancellationRequested",
                table: "DataImportBatches");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAtUtc",
                table: "DataImportBatches");

            migrationBuilder.DropColumn(
                name: "CancellationRequested",
                table: "DataImportBatches");
        }
    }
}
