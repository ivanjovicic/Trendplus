using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Infrastructure.DbContexts;

namespace Infrastructure.Migrations.AnalyticsDb;

[DbContext(typeof(AnalyticsDbContext))]
[Migration("20260909190000_AddSalesLineFactIdempotency")]
public partial class AddSalesLineFactIdempotency : Migration
{
    // History: this migration originally aborted (RAISE EXCEPTION) when duplicate
    // (SaleId, ProductId) rows existed and then made (SaleId, ProductId) unique. That key is
    // wrong: one receipt can carry the same product on several lines, so databases holding
    // legitimate repeated lines could not migrate at all. It already ran in production, where
    // EF never re-runs it; production is moved to the correct line key by
    // 20261010150000_KeySalesLineFactsBySourceLine. For fresh and not-yet-migrated databases
    // this step now only swaps the lookup index (non-unique) and never fails on repeated lines.
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_SalesLineFacts_ProductId_SaleId";
            CREATE INDEX IF NOT EXISTS "IX_SalesLineFacts_SaleId_ProductId" ON "SalesLineFacts" ("SaleId", "ProductId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_SalesLineFacts_SaleId_ProductId";
            CREATE INDEX IF NOT EXISTS "IX_SalesLineFacts_ProductId_SaleId" ON "SalesLineFacts" ("ProductId", "SaleId");
            """);
    }
}
