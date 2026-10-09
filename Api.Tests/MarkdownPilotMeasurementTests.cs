using System.Globalization;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class MarkdownPilotMeasurementContractTests
{
    [Fact(DisplayName = "RQ602 measurement is read-only and preserves unknown and unmatched evidence")]
    public void Query_IsReadOnlyAndKeepsUnknownCostsAndUnmatchedControlsExplicit()
    {
        var sql = MarkdownPilotMeasurementTests.LoadMeasurementQuery();

        Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'DUG', 'KOREKCIJA'", sql, StringComparison.Ordinal);
        Assert.Contains("unmatched_markdown_in_window", sql, StringComparison.Ordinal);
        Assert.Contains("pre_unknown_cost_line_count", sql, StringComparison.Ordinal);
        Assert.Contains("post_unknown_cost_line_count", sql, StringComparison.Ordinal);
        Assert.Contains("pre_known_cost_margin_rsd", sql, StringComparison.Ordinal);
        Assert.Contains("pre_realized_gross_margin_rsd", sql, StringComparison.Ordinal);
        Assert.Contains("post_realized_gross_margin_rsd", sql, StringComparison.Ordinal);
        Assert.Contains("markdown_event_verified", sql, StringComparison.Ordinal);
        Assert.Contains("GROUPING SETS", sql, StringComparison.Ordinal);
        Assert.Contains("current_stock_snapshot_pairs", sql, StringComparison.Ordinal);
        Assert.Contains("post_window_complete", sql, StringComparison.Ordinal);
        Assert.Contains("observed_through_local_date", sql, StringComparison.Ordinal);
    }
}

[Trait("Category", "Integration")]
public sealed class MarkdownPilotMeasurementTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public MarkdownPilotMeasurementTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "RQ602 query matches independent pre/post, return, exclusion and cost-coverage oracle")]
    public async Task Query_ProducesDescriptiveMetricsWithoutFakeCostOrMatchedControl()
    {
        var configuredPostgres = Environment.GetEnvironmentVariable("RQ602_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configuredPostgres) && !_fixture.IsAvailable)
        {
            return;
        }

        var databaseName = $"rq602_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(
            string.IsNullOrWhiteSpace(configuredPostgres) ? _fixture.AdminConnectionString : configuredPostgres)
        {
            Database = "postgres",
            Pooling = false
        };
        var testBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString) { Database = databaseName };

        await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", admin);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            await using var connection = new NpgsqlConnection(testBuilder.ConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, """
                CREATE TABLE "Artikli" (
                    "Id" integer PRIMARY KEY,
                    "IDObjekat" integer,
                    "Kolicina" integer
                );
                CREATE TABLE "DnevnikPromena" (
                    "Id" integer PRIMARY KEY,
                    "TipPromene" text NOT NULL,
                    "Datum" timestamp NOT NULL,
                    "StaraProdajnaCena" numeric,
                    "NovaProdajnaCena" numeric,
                    "ArtikalId" integer,
                    "IDObjekat" integer
                );
                CREATE TABLE prodaja_zaglavlje (
                    id integer PRIMARY KEY,
                    broj_racuna text,
                    datum_prodaje timestamp NOT NULL,
                    id_objekat integer
                );
                CREATE TABLE prodaja_stavke (
                    id integer PRIMARY KEY,
                    id_prodaja integer NOT NULL,
                    id_artikal integer NOT NULL,
                    kolicina integer NOT NULL,
                    cena numeric NOT NULL,
                    nabavna_cena numeric
                );
                INSERT INTO "Artikli" ("Id", "IDObjekat", "Kolicina") VALUES
                    (1, 1, 7), (2, 1, 9), (3, 1, 4), (4, 1, 5);
                INSERT INTO prodaja_zaglavlje (id, broj_racuna, datum_prodaje, id_objekat) VALUES
                    (101, 'pre-start-boundary', '2026-07-04T12:00:00', 1),
                    (102, 'before-pre-window', '2026-07-03T12:00:00', 1),
                    (103, 'pre-end', '2026-07-31T12:00:00', 1),
                    (104, 'post-start-boundary', '2026-08-01T12:00:00', 1),
                    (105, 'return-post', '2026-08-10T12:00:00', 1),
                    (106, 'post-end-boundary', '2026-08-29T12:00:00', 1),
                    (107, 'DUG', '2026-07-20T12:00:00', 1),
                    (108, 'KOREKCIJA', '2026-08-20T12:00:00', 1),
                    (109, 'control-pre', '2026-07-04T12:00:00', 1),
                    (110, 'control-post-known', '2026-08-02T12:00:00', 1),
                    (111, 'control-post-unknown', '2026-08-03T12:00:00', 1),
                    (112, 'treated-unknown', '2026-08-15T12:00:00', 1),
                    (113, 'unmatched-control-sale', '2026-08-05T12:00:00', 1);
                INSERT INTO prodaja_stavke (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena) VALUES
                    (201, 101, 1, 2, 100, 60),
                    (202, 102, 1, 100, 100, 60),
                    (203, 103, 1, 3, 100, 70),
                    (204, 104, 1, 5, 80, 60),
                    (205, 105, 1, -1, 80, 60),
                    (206, 106, 1, 50, 80, 60),
                    (207, 107, 1, 1000, 80, 60),
                    (208, 108, 1, 1000, 80, 60),
                    (209, 109, 2, 4, 100, 50),
                    (210, 110, 2, 2, 90, 50),
                    (211, 111, 2, 1, 90, NULL),
                    (212, 112, 3, 2, 75, NULL),
                    (213, 113, 4, 6, 70, 40);
                INSERT INTO "DnevnikPromena"
                    ("Id", "TipPromene", "Datum", "StaraProdajnaCena", "NovaProdajnaCena", "ArtikalId", "IDObjekat")
                VALUES
                    (300, 'Nivelacija', '2026-08-01T08:00:00', 100, 80, 1, 1),
                    (301, 'Nivelacija', '2026-08-05T09:00:00', 100, 70, 4, 1);
                """);

            await using var command = new NpgsqlCommand(LoadMeasurementQuery(), connection);
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[reader.GetName(index)] = await reader.IsDBNullAsync(index) ? null : reader.GetValue(index);
                }

                rows.Add(row);
            }

            var action1Treated = Find(rows, "fixture-action-1", "treated", "treated", isTotal: false);
            Assert.Equal(5m, Number(action1Treated, "pre_pairs_sold"));
            Assert.Equal(4m, Number(action1Treated, "post_pairs_sold"));
            Assert.Equal(170m, Number(action1Treated, "pre_realized_gross_margin_rsd"));
            Assert.Equal(80m, Number(action1Treated, "post_realized_gross_margin_rsd"));
            Assert.Equal(7m, Number(action1Treated, "current_stock_snapshot_pairs"));
            Assert.Equal(true, action1Treated["markdown_event_verified"]);
            Assert.Equal(true, action1Treated["pre_window_complete"]);
            Assert.Equal(true, action1Treated["post_window_complete"]);

            var action1Comparison = Find(rows, "fixture-action-1", "comparison", "matched", isTotal: false);
            Assert.Equal(4m, Number(action1Comparison, "pre_pairs_sold"));
            Assert.Equal(3m, Number(action1Comparison, "post_pairs_sold"));
            Assert.Equal(1m, Number(action1Comparison, "post_unknown_cost_line_count"));
            Assert.Null(action1Comparison["post_realized_gross_margin_rsd"]);
            Assert.Equal(9m, Number(action1Comparison, "current_stock_snapshot_pairs"));

            var action2Treated = Find(rows, "fixture-action-2", "treated", "treated", isTotal: false);
            Assert.Equal(1m, Number(action2Treated, "post_unknown_cost_line_count"));
            Assert.Null(action2Treated["post_realized_gross_margin_rsd"]);
            Assert.Equal(false, action2Treated["markdown_event_verified"]);

            var unmatched = Find(rows, "fixture-action-2", "comparison", "unmatched_markdown_in_window", isTotal: false);
            Assert.Equal(1m, Number(unmatched, "unmatched_comparison_count"));
            Assert.Equal(6m, Number(unmatched, "post_pairs_sold"));
            Assert.Contains(rows, row => Equals(row["match_status"], "unmatched_markdown_in_window"));
            Assert.Contains(rows, row => Equals(row["is_total"], true));
        }
        finally
        {
            await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    internal static string LoadMeasurementQuery()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "tools", "markdown-pilot-measurement.sql");
            if (File.Exists(path))
            {
                var script = File.ReadAllText(path);
                const string start = "-- RQ602_QUERY_START";
                const string end = "-- RQ602_QUERY_END";
                var startIndex = script.IndexOf(start, StringComparison.Ordinal);
                var endIndex = script.IndexOf(end, StringComparison.Ordinal);
                Assert.True(startIndex >= 0 && endIndex > startIndex, "RQ602 query markers are missing.");
                return script[(startIndex + start.Length)..endIndex];
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the RQ602 query script.");
    }

    private static Dictionary<string, object?> Find(
        IEnumerable<Dictionary<string, object?>> rows,
        string? actionId,
        string role,
        string matchStatus,
        bool isTotal)
        => Assert.Single(rows, row => Equals(row["action_id"], actionId)
            && Equals(row["cohort_role"], role)
            && Equals(row["match_status"], matchStatus)
            && Equals(row["is_total"], isTotal));

    private static decimal Number(Dictionary<string, object?> row, string key)
        => Convert.ToDecimal(row[key], CultureInfo.InvariantCulture);

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
