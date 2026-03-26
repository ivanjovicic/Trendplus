using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(TrendplusDbContext))]
    [Migration("20260112000000_AddArtikliKategorije")]
    public partial class AddArtikliKategorije : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Kategorija column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Artikli' AND column_name = 'Kategorija'
                    ) THEN
                        ALTER TABLE ""Artikli"" ADD COLUMN ""Kategorija"" text;
                    END IF;
                END $$;
            ");

            // Add Pol column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Artikli' AND column_name = 'Pol'
                    ) THEN
                        ALTER TABLE ""Artikli"" ADD COLUMN ""Pol"" text;
                    END IF;
                END $$;
            ");

            // Add Velicina column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Artikli' AND column_name = 'Velicina'
                    ) THEN
                        ALTER TABLE ""Artikli"" ADD COLUMN ""Velicina"" text;
                    END IF;
                END $$;
            ");

            // Add Boja column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Artikli' AND column_name = 'Boja'
                    ) THEN
                        ALTER TABLE ""Artikli"" ADD COLUMN ""Boja"" text;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Boja",
                table: "Artikli");

            migrationBuilder.DropColumn(
                name: "Velicina",
                table: "Artikli");

            migrationBuilder.DropColumn(
                name: "Kategorija",
                table: "Artikli");

            migrationBuilder.DropColumn(
                name: "Pol",
                table: "Artikli");
        }
    }
}
