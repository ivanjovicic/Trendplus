using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(TrendplusDbContext))]
[Migration("20261010160000_AddPurchaseCostProvenance")]
public partial class AddPurchaseCostProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Additive and idempotent: legacy numeric values are deliberately not rewritten.
        migrationBuilder.Sql(
            """
            ALTER TABLE IF EXISTS prodaja_stavke
                ADD COLUMN IF NOT EXISTS purchase_cost_provenance character varying(64);
            """,
            suppressTransaction: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep evidence metadata on rollback; dropping it would destroy provenance.
    }
}
