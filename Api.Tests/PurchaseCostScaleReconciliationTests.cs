using System.Globalization;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class PurchaseCostScaleReconciliationContractTests
{
    [Fact(DisplayName = "RQ601 report is read-only and keeps unknown cost nullable")]
    public void Report_IsReadOnlyAndKeepsUnknownCostNullable()
    {
        var sql = PurchaseCostScaleReconciliationTests.LoadReportQuery();

        Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("master_nabavnacena_din_backfill", sql, StringComparison.Ordinal);
        Assert.Contains("master_nabavnacena_backfill", sql, StringComparison.Ordinal);
        Assert.Contains("source_sale_line", sql, StringComparison.Ordinal);
        Assert.Contains("ELSE NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("d.\"Iznos\" > 0", sql, StringComparison.Ordinal);
    }
}

[Trait("Category", "Integration")]
public sealed class PurchaseCostScaleReconciliationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public PurchaseCostScaleReconciliationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "RQ601 report attributes source, inbound and master-backfill costs")]
    public async Task Report_AttributesCostOriginsWithoutTurningUnknownIntoZero()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var databaseName = $"rq601_{Guid.NewGuid():N}";
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(databaseName);
        Assert.NotNull(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, """
            CREATE TABLE "Artikli" (
                "Id" integer PRIMARY KEY,
                "PLU" text,
                "Naziv" text NOT NULL,
                "IDDobavljac" integer,
                "IDObjekat" integer,
                "NabavnaCena" numeric,
                "NabavnaCenaDin" numeric,
                "ProdajnaCena" numeric,
                "Kolicina" integer
            );
            CREATE TABLE "DnevnikPromena" (
                "Id" integer PRIMARY KEY,
                "TipPromene" text NOT NULL,
                "Datum" timestamp NOT NULL,
                "Iznos" numeric NOT NULL,
                "ArtikalId" integer,
                "Kolicina" integer,
                "IDObjekat" integer,
                "DataOrigin" text
            );
            CREATE TABLE "prodaja_zaglavlje" (
                "id" integer PRIMARY KEY,
                "datum_prodaje" timestamp NOT NULL,
                "id_objekat" integer
            );
            CREATE TABLE "prodaja_stavke" (
                "id" integer PRIMARY KEY,
                "id_prodaja" integer NOT NULL,
                "id_artikal" integer NOT NULL,
                "nabavna_cena" numeric
            );
            INSERT INTO "Artikli" ("Id", "PLU", "Naziv", "IDDobavljac", "NabavnaCena", "NabavnaCenaDin", "ProdajnaCena", "Kolicina") VALUES
                (1, 'SOURCE-1', 'Source cost', 10, 245, NULL, 5700, 5),
                (2, 'BACKFILL-2', 'Master fallback', 10, 245, NULL, 5700, 4),
                (3, 'UNKNOWN-3', 'Unknown cost', 11, NULL, NULL, 5700, 3);
            INSERT INTO "DnevnikPromena" ("Id", "TipPromene", "Datum", "Iznos", "ArtikalId", "Kolicina", "IDObjekat", "DataOrigin") VALUES
                (11, 'Ulaz robe', '2026-08-01T10:00:00', 29000, 1, 10, 1, 'existing'),
                (12, 'Ulaz robe', '2026-08-02T10:00:00', 11600, 2, 4, 1, 'existing'),
                (13, 'Ulaz robe', '2026-08-03T10:00:00', 0, 3, 3, 1, 'access');
            INSERT INTO "prodaja_zaglavlje" ("id", "datum_prodaje", "id_objekat") VALUES
                (21, '2026-08-04T10:00:00', 1),
                (22, '2026-08-05T10:00:00', 1),
                (23, '2026-08-06T10:00:00', 1);
            INSERT INTO "prodaja_stavke" ("id", "id_prodaja", "id_artikal", "nabavna_cena") VALUES
                (31, 21, 1, 3000),
                (32, 22, 2, NULL),
                (33, 23, 3, NULL);
            """);

        var reportSql = LoadReportQuery();
        await using var command = new NpgsqlCommand(reportSql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new Dictionary<int, Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = await reader.IsDBNullAsync(index) ? null : reader.GetValue(index);
            }

            rows[Convert.ToInt32(row["article_id"], CultureInfo.InvariantCulture)] = row;
        }

        Assert.Equal(3, rows.Count);
        Assert.Equal(245m, Convert.ToDecimal(rows[1]["master_cost_legacy"], CultureInfo.InvariantCulture));
        Assert.Equal(2900m, Convert.ToDecimal(rows[1]["latest_inbound_unit_cost"], CultureInfo.InvariantCulture));
        Assert.Equal(3000m, Convert.ToDecimal(rows[1]["latest_sale_line_cost"], CultureInfo.InvariantCulture));
        Assert.Equal("source_sale_line", rows[1]["sale_line_cost_origin"]);
        Assert.True(Convert.ToBoolean(rows[1]["master_scale_suspicious"], CultureInfo.InvariantCulture));

        Assert.Equal(2900m, Convert.ToDecimal(rows[2]["latest_inbound_unit_cost"], CultureInfo.InvariantCulture));
        Assert.Equal(245m, Convert.ToDecimal(rows[2]["latest_sale_line_cost"], CultureInfo.InvariantCulture));
        Assert.Equal("master_nabavnacena_backfill", rows[2]["sale_line_cost_origin"]);
        Assert.Null(rows[3]["latest_inbound_unit_cost"]);
        Assert.Null(rows[3]["latest_sale_line_cost"]);
        Assert.Equal("unknown", rows[3]["sale_line_cost_origin"]);
        Assert.Null(rows[3]["stock_value_sale_line"]);
    }

    internal static string LoadReportQuery()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "tools", "purchase-cost-scale-reconciliation.sql");
            if (File.Exists(path))
            {
                var script = File.ReadAllText(path);
                const string start = "-- RQ601_REPORT_QUERY_START";
                const string end = "-- RQ601_REPORT_QUERY_END";
                var startIndex = script.IndexOf(start, StringComparison.Ordinal);
                var endIndex = script.IndexOf(end, StringComparison.Ordinal);
                Assert.True(startIndex >= 0 && endIndex > startIndex, "RQ601 report query markers are missing.");
                return script[(startIndex + start.Length)..endIndex];
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the RQ601 report script.");
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
