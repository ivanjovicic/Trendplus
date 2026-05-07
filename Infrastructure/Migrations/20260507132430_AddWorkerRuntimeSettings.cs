using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerRuntimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessImportCursors_TableKey_LeaseExpiresAtUtc",
                table: "AccessImportCursors");

            migrationBuilder.CreateTable(
                name: "WorkerRuntimeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsScheduleEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsManuallyStopped = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerRuntimeSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerRuntimeSettings_UpdatedAtUtc",
                table: "WorkerRuntimeSettings",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerRuntimeSettings_WorkerName",
                table: "WorkerRuntimeSettings",
                column: "WorkerName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkerRuntimeSettings");

            migrationBuilder.CreateIndex(
                name: "IX_AccessImportCursors_TableKey_LeaseExpiresAtUtc",
                table: "AccessImportCursors",
                columns: new[] { "TableKey", "LeaseExpiresAtUtc" });
        }
    }
}
