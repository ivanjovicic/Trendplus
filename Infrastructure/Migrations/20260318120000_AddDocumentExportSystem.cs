using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddDocumentExportSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Roles = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    HeaderContent = table.Column<string>(type: "text", nullable: true),
                    FooterContent = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TableKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TableTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Orientation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedByRoles = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    IncludeFiltersAndMetadata = table.Column<bool>(type: "boolean", nullable: false),
                    IsPreview = table.Column<bool>(type: "boolean", nullable: false),
                    IsAsync = table.Column<bool>(type: "boolean", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    FiltersJson = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAudits_DocumentId_CreatedAtUtc",
                table: "DocumentAudits",
                columns: new[] { "DocumentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BatchId",
                table: "Documents",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedAtUtc",
                table: "Documents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RequestedByUserId",
                table: "Documents",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status",
                table: "Documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status_NextAttemptAtUtc",
                table: "Documents",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_Name_Version",
                table: "DocumentTemplates",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplates_Type_IsActive",
                table: "DocumentTemplates",
                columns: new[] { "Type", "IsActive" });

            migrationBuilder.InsertData(
                table: "DocumentTemplates",
                columns: new[] { "Id", "Name", "Version", "Type", "Locale", "Content", "HeaderContent", "FooterContent", "IsActive", "CreatedByUserId", "CreatedAtUtc" },
                values: new object[,]
                {
                    {
                        new Guid("65f367aa-4206-4b7e-b7d2-7d8ef7351111"),
                        "analytics-table-default",
                        1,
                        "analytics-table-report",
                        "sr-RS",
                        "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><title>{{title}}</title><style>{{styles}}</style></head><body class=\"doc {{orientation}}\"><div class=\"sheet\"><header>{{header}}</header><section class=\"meta\"><div><strong>Izvestaj:</strong> {{title}}</div><div><strong>Generisano:</strong> {{generated_at}}</div><div><strong>Korisnik:</strong> {{requested_by}}</div></section><section class=\"filters\"><h3>Filteri</h3>{{filters}}</section><section class=\"metadata\"><h3>Metapodaci</h3>{{metadata}}</section><section class=\"table-section\">{{table}}</section><footer>{{footer}}</footer></div></body></html>",
                        "<div class=\"doc-header\"><h1>{{title}}</h1><p>Trendplus Analytics Export</p></div>",
                        "<div class=\"doc-footer\"><span>Template v{{template_version}}</span><span>{{table_key}}</span></div>",
                        true,
                        "system",
                        new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        new Guid("65f367aa-4206-4b7e-b7d2-7d8ef7352222"),
                        "executive-summary-default",
                        1,
                        "executive-summary",
                        "sr-RS",
                        "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><title>{{title}}</title><style>{{styles}}</style></head><body class=\"doc portrait\"><div class=\"sheet\"><header>{{header}}</header><section class=\"table-section\">{{table}}</section><footer>{{footer}}</footer></div></body></html>",
                        "<div class=\"doc-header\"><h1>{{title}}</h1></div>",
                        "<div class=\"doc-footer\">Trendplus Executive Summary</div>",
                        true,
                        "system",
                        new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        new Guid("65f367aa-4206-4b7e-b7d2-7d8ef7353333"),
                        "receipt-default",
                        1,
                        "receipt",
                        "sr-RS",
                        "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><title>{{title}}</title><style>{{styles}}</style></head><body class=\"doc portrait thermal\"><div class=\"sheet\">{{table}}</div></body></html>",
                        null,
                        null,
                        true,
                        "system",
                        new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        new Guid("65f367aa-4206-4b7e-b7d2-7d8ef7354444"),
                        "label-default",
                        1,
                        "label",
                        "sr-RS",
                        "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><title>{{title}}</title><style>{{styles}}</style></head><body class=\"doc portrait label\"><div class=\"sheet\">{{table}}</div></body></html>",
                        null,
                        null,
                        true,
                        "system",
                        new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
                    }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DocumentAudits");
            migrationBuilder.DropTable(name: "Documents");
            migrationBuilder.DropTable(name: "DocumentTemplates");
        }
    }
}
