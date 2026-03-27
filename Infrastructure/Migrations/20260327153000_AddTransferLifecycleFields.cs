using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddTransferLifecycleFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Transfers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Transfers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessedQuantity",
                table: "TransferItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantity",
                table: "TransferItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE "Transfers"
                SET "UpdatedAt" = COALESCE("CreatedAt", NOW())
                WHERE "UpdatedAt" IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ProcessedQuantity",
                table: "TransferItems");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "TransferItems");
        }
    }
}
