using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(TrendplusDbContext))]
[Migration("20261016103000_EnsureArticleImagePathColumn")]
public sealed class EnsureArticleImagePathColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "ImagePath" character varying(500);
            CREATE INDEX IF NOT EXISTS "IX_Artikli_ImagePath" ON "Artikli" ("ImagePath");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the mapped article field and index available; dropping them could erase app data.
    }
}
