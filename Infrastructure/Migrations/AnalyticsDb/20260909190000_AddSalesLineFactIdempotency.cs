using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Infrastructure.DbContexts;

namespace Infrastructure.Migrations.AnalyticsDb;

[DbContext(typeof(AnalyticsDbContext))]
[Migration("20260909190000_AddSalesLineFactIdempotency")]
public partial class AddSalesLineFactIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM "SalesLineFacts"
                    GROUP BY "SaleId", "ProductId"
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Cannot enforce SalesLineFacts uniqueness because duplicate (SaleId, ProductId) rows exist.';
                END IF;
            END
            $$;
            """);

        migrationBuilder.DropIndex(
            name: "IX_SalesLineFacts_ProductId_SaleId",
            table: "SalesLineFacts");

        migrationBuilder.CreateIndex(
            name: "IX_SalesLineFacts_SaleId_ProductId",
            table: "SalesLineFacts",
            columns: new[] { "SaleId", "ProductId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SalesLineFacts_SaleId_ProductId",
            table: "SalesLineFacts");

        migrationBuilder.CreateIndex(
            name: "IX_SalesLineFacts_ProductId_SaleId",
            table: "SalesLineFacts",
            columns: new[] { "ProductId", "SaleId" });
    }
}
