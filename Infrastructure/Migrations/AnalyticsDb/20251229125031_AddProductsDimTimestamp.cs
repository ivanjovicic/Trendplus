using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddProductsDimTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PerformanceLogs table is created/managed outside of this migration in existing environments.
            // Do not attempt to create it here to avoid "relation already exists" errors.

            migrationBuilder.CreateTable(
                name: "ProductsDim",
                columns: table => new
                {
                    ProductKey = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    SubCategory = table.Column<string>(type: "text", nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: false),
                    FootwearTypeId = table.Column<int>(type: "integer", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SeasonId = table.Column<int>(type: "integer", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    PurchasePriceRsd = table.Column<decimal>(type: "numeric", nullable: true),
                    FirstSalePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    SalePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductsDim", x => x.ProductKey);
                });

            migrationBuilder.CreateTable(
                name: "StoresDim",
                columns: table => new
                {
                    StoreKey = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StoreId = table.Column<int>(type: "integer", nullable: false),
                    StoreName = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: true),
                    Region = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoresDim", x => x.StoreKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductsDim_ProductId",
                table: "ProductsDim",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductsDim_Timestamp",
                table: "ProductsDim",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductsDim");

            migrationBuilder.DropTable(
                name: "StoresDim");

            // Intentionally not dropping PerformanceLogs here.
        }
    }
}
