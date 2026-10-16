using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(TrendplusDbContext))]
[Migration("20261016120000_AddGoodsReceiptReversals")]
public sealed class AddGoodsReceiptReversals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "GoodsReceipts"
                ADD COLUMN IF NOT EXISTS "ReversalRequestKey" uuid,
                ADD COLUMN IF NOT EXISTS "ReversedBy" varchar(80),
                ADD COLUMN IF NOT EXISTS "ReversedAtUtc" timestamp with time zone,
                ADD COLUMN IF NOT EXISTS "ReversalReason" varchar(500);
            ALTER TABLE "GoodsReceipts" DROP CONSTRAINT IF EXISTS "CK_GoodsReceipts_Status";
            ALTER TABLE "GoodsReceipts"
                ADD CONSTRAINT "CK_GoodsReceipts_Status"
                CHECK ("Status" IN ('confirmed', 'reversed'));
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_GoodsReceipts_ReversalRequestKey"
                ON "GoodsReceipts" ("ReversalRequestKey") WHERE "ReversalRequestKey" IS NOT NULL;

            ALTER TABLE "GoodsReceiptLines"
                ADD COLUMN IF NOT EXISTS "ReversalMovementId" integer REFERENCES "DnevnikPromena" ("Id") ON DELETE RESTRICT,
                ADD COLUMN IF NOT EXISTS "ReversalProjectedAtUtc" timestamp with time zone;
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_GoodsReceiptLines_ReversalMovementId"
                ON "GoodsReceiptLines" ("ReversalMovementId")
                WHERE "ReversalMovementId" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reversal data is a durable inventory audit; never destroy it on automatic rollback.
    }
}
