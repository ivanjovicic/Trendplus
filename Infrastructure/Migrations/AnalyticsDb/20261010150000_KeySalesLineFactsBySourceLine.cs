using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Infrastructure.DbContexts;

namespace Infrastructure.Migrations.AnalyticsDb;

/// <summary>
/// SalesLineFacts is keyed by the source receipt line, not by (SaleId, ProductId):
/// a receipt can carry the same product on several lines and each one must stay a
/// separate fact row. Source identity is (SourceTableKey, SourceLineId), see
/// Domain.Model.SalesLineSourceIdentity. Existing rows keep NULL identity (the analytics
/// database is separate from prodaja_stavke, so there is no reliable join to backfill
/// from); the partial unique index ignores them and the next import fills them in.
/// Every statement is idempotent so databases whose schema was already adjusted by hand
/// or by startup self-heal can still migrate.
/// </summary>
[DbContext(typeof(AnalyticsDbContext))]
[Migration("20261010150000_KeySalesLineFactsBySourceLine")]
public partial class KeySalesLineFactsBySourceLine : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "SalesLineFacts" ADD COLUMN IF NOT EXISTS "SourceTableKey" character varying(128);
            ALTER TABLE "SalesLineFacts" ADD COLUMN IF NOT EXISTS "SourceLineId" bigint;
            DROP INDEX IF EXISTS "IX_SalesLineFacts_SaleId_ProductId";
            CREATE INDEX IF NOT EXISTS "IX_SalesLineFacts_SaleId_ProductId" ON "SalesLineFacts" ("SaleId", "ProductId");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_SalesLineFacts_SaleId_SourceLine" ON "SalesLineFacts" ("SaleId", "SourceTableKey", "SourceLineId") WHERE "SourceLineId" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restores the columns/index shape only. The old unique (SaleId, ProductId)
        // key is deliberately not recreated: it rejects legitimate repeated lines.
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "UX_SalesLineFacts_SaleId_SourceLine";
            ALTER TABLE "SalesLineFacts" DROP COLUMN IF EXISTS "SourceLineId";
            ALTER TABLE "SalesLineFacts" DROP COLUMN IF EXISTS "SourceTableKey";
            """);
    }
}
