using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    [DbContext(typeof(AnalyticsDbContext))]
    [Migration("20260203123000_AddBojaVelicinaToProductsDim")]
    public partial class AddBojaVelicinaToProductsDim : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""ProductsDim"" ADD COLUMN IF NOT EXISTS ""Boja"" text;");
            migrationBuilder.Sql(@"ALTER TABLE ""ProductsDim"" ADD COLUMN IF NOT EXISTS ""Velicina"" text;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Boja",
                table: "ProductsDim");

            migrationBuilder.DropColumn(
                name: "Velicina",
                table: "ProductsDim");
        }
    }
}
