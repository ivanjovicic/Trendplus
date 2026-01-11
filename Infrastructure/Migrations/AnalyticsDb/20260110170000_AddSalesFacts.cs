using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddSalesFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesFacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    BrojRacuna = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SaleTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StoreId = table.Column<int>(type: "integer", nullable: false),
                    PaymentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalUnits = table.Column<int>(type: "integer", nullable: false),
                    TotalLines = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesFacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesLineFacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLineFacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesFacts_SaleId",
                table: "SalesFacts",
                column: "SaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesFacts_SaleTimestampUtc",
                table: "SalesFacts",
                column: "SaleTimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SalesFacts_StoreId",
                table: "SalesFacts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineFacts_SaleId",
                table: "SalesLineFacts",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLineFacts_ProductId_SaleId",
                table: "SalesLineFacts",
                columns: new[] { "ProductId", "SaleId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesLineFacts");

            migrationBuilder.DropTable(
                name: "SalesFacts");
        }
    }
}
