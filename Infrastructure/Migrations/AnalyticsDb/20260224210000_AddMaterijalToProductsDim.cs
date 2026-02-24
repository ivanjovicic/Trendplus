using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    [DbContext(typeof(AnalyticsDbContext))]
    [Migration("20260224210000_AddMaterijalToProductsDim")]
    public partial class AddMaterijalToProductsDim : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""ProductsDim"" ADD COLUMN IF NOT EXISTS ""Materijal"" character varying(100);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Materijal",
                table: "ProductsDim");
        }
    }
}
