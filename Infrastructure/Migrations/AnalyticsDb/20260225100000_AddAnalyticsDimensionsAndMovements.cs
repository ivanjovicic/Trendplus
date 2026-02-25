using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations.AnalyticsDb
{
    /// <inheritdoc />
    public partial class AddAnalyticsDimensionsAndMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── Existing table column additions ───────────────────────────

            migrationBuilder.AddColumn<string>(
                name: "PLU",
                table: "ProductsDim",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimalnaKolicina",
                table: "ProductsDim",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefon",
                table: "StoresDim",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Menedzer",
                table: "StoresDim",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NabavnaCena",
                table: "SalesLineFacts",
                type: "numeric(18,2)",
                nullable: true);

            // ─── New dimension tables ───────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "SuppliersDim",
                columns: table => new
                {
                    SupplierKey = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId    = table.Column<int>(type: "integer", nullable: false),
                    Naziv         = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: ""),
                    Adresa        = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Telefon       = table.Column<string>(type: "character varying(50)",  maxLength: 50,  nullable: true),
                    Napomena      = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DataOrigin    = table.Column<string>(type: "character varying(32)",  maxLength: 32,  nullable: false, defaultValue: "existing"),
                    UpdatedAt     = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_SuppliersDim", x => x.SupplierKey); });

            migrationBuilder.CreateIndex(name: "IX_SuppliersDim_SupplierId", table: "SuppliersDim", column: "SupplierId", unique: true);

            migrationBuilder.CreateTable(
                name: "SeasonsDim",
                columns: table => new
                {
                    SeasonKey  = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonId   = table.Column<int>(type: "integer", nullable: false),
                    Naziv      = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    DatumOd    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatumDo    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataOrigin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "existing"),
                    UpdatedAt  = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_SeasonsDim", x => x.SeasonKey); });

            migrationBuilder.CreateIndex(name: "IX_SeasonsDim_SeasonId", table: "SeasonsDim", column: "SeasonId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_SeasonsDim_DatumOd", table: "SeasonsDim", column: "DatumOd");
            migrationBuilder.CreateIndex(name: "IX_SeasonsDim_DatumDo", table: "SeasonsDim", column: "DatumDo");

            migrationBuilder.CreateTable(
                name: "FootwearTypesDim",
                columns: table => new
                {
                    TypeKey    = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TypeId     = table.Column<int>(type: "integer", nullable: false),
                    Naziv      = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    DataOrigin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "existing"),
                    UpdatedAt  = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_FootwearTypesDim", x => x.TypeKey); });

            migrationBuilder.CreateIndex(name: "IX_FootwearTypesDim_TypeId", table: "FootwearTypesDim", column: "TypeId", unique: true);

            // ─── New fact table ─────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "InventoryMovementFacts",
                columns: table => new
                {
                    Id                 = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId           = table.Column<int>(type: "integer", nullable: false),
                    TipPromene         = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Datum              = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArtikalId          = table.Column<int>(type: "integer", nullable: true),
                    Kolicina           = table.Column<int>(type: "integer", nullable: true),
                    StaraProdajnaCena  = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NovaProdajnaCena   = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Iznos              = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StoreId            = table.Column<int>(type: "integer", nullable: true),
                    DobavljacId        = table.Column<int>(type: "integer", nullable: true),
                    BrojDokumenta      = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    KorisnikIme        = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataOrigin         = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "existing")
                },
                constraints: table => { table.PrimaryKey("PK_InventoryMovementFacts", x => x.Id); });

            migrationBuilder.CreateIndex(name: "IX_InventoryMovementFacts_SourceId",           table: "InventoryMovementFacts", column: "SourceId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_InventoryMovementFacts_Datum",              table: "InventoryMovementFacts", column: "Datum");
            migrationBuilder.CreateIndex(name: "IX_InventoryMovementFacts_TipPromene",         table: "InventoryMovementFacts", column: "TipPromene");
            migrationBuilder.CreateIndex(name: "IX_InventoryMovementFacts_ArtikalId_Datum",    table: "InventoryMovementFacts", columns: new[] { "ArtikalId", "Datum" });
            migrationBuilder.CreateIndex(name: "IX_InventoryMovementFacts_StoreId_Datum",      table: "InventoryMovementFacts", columns: new[] { "StoreId", "Datum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InventoryMovementFacts");
            migrationBuilder.DropTable(name: "FootwearTypesDim");
            migrationBuilder.DropTable(name: "SeasonsDim");
            migrationBuilder.DropTable(name: "SuppliersDim");

            migrationBuilder.DropColumn(name: "NabavnaCena",        table: "SalesLineFacts");
            migrationBuilder.DropColumn(name: "Telefon",            table: "StoresDim");
            migrationBuilder.DropColumn(name: "Menedzer",           table: "StoresDim");
            migrationBuilder.DropColumn(name: "PLU",                table: "ProductsDim");
            migrationBuilder.DropColumn(name: "MinimalnaKolicina",  table: "ProductsDim");
        }
    }
}
