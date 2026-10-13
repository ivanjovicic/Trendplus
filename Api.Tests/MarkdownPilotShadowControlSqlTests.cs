using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "RQ612")]
public sealed class MarkdownPilotShadowControlSqlTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public MarkdownPilotShadowControlSqlTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresRequiredFact(DisplayName = "RQ612 shadow screen requires nonzero matched demand and keeps unknown availability unqualified")]
    public async Task ShadowQuery_RejectsZeroUnmatchedConfoundedAndReusedControls()
    {
        Assert.True(_fixture.IsAvailable,
            "RQ612 PostgreSQL certification requires disposable PostgreSQL; unavailable infrastructure must fail, not skip.");
        var databaseName = $"tp_rq612_{Guid.NewGuid():N}";
        try
        {
            var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(databaseName);
            Assert.False(string.IsNullOrWhiteSpace(connectionString));

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await SeedAsync(connection);

            var rows = await ExecuteQueryAsync(connection, LoadQuery());
            var summary1 = Assert.Single(rows.Where(r => Text(r, "record_type") == "cohort_summary" && Int(r, "cohort_no") == 1));
            Assert.Equal(6, Int(summary1, "exact_store_supplier_type_candidates"));
            Assert.Equal(1, Int(summary1, "same_store_type_wrong_supplier"));
            Assert.Equal(1, Int(summary1, "unique_screened_candidates"));
            Assert.Equal(0, Int(summary1, "fully_qualified_pairs"));
            Assert.Equal("unverified_no_historical_daily_stockout_or_availability_evidence", Text(summary1, "availability_status"));
            Assert.Equal("unverified_batch23_per_store_day_source_coverage", Text(summary1, "source_coverage_status"));

            var signedControl = FindCandidate(rows, 1, 9001);
            Assert.Equal(4m, Decimal(signedControl, "pre_gross_positive_units"));
            Assert.Equal(3m, Decimal(signedControl, "pre_signed_units"));
            Assert.Equal(1, Int(signedControl, "pre_return_line_count"));
            Assert.Equal(2, Int(signedControl, "pre_unknown_cost_lines"));
            Assert.Equal("screen_pass_availability_unverified", Text(signedControl, "screen_status"));
            Assert.Equal("screened_candidate_not_qualified_availability_unknown", Text(signedControl, "qualification_status"));

            var zeroSignal = FindCandidate(rows, 1, 9002);
            Assert.Equal("zero_signal_pre_sales", Text(zeroSignal, "screen_status"));
            var excludedDocuments = FindCandidate(rows, 1, 9005);
            Assert.Equal("zero_signal_pre_sales", Text(excludedDocuments, "screen_status"));

            var boundaryCandidate = FindCandidate(rows, 1, 9004);
            Assert.Equal(1m, Decimal(boundaryCandidate, "pre_gross_positive_units"));
            Assert.Equal(1, Int(boundaryCandidate, "pre_receipt_count"));

            var mismatchedPrice = FindCandidate(rows, 1, 9007);
            Assert.Equal("pre_price_mismatch", Text(mismatchedPrice, "screen_status"));
            var repeatedMarkdown = FindCandidate(rows, 1, 9006);
            Assert.Equal("overlapping_or_repeated_markdown", Text(repeatedMarkdown, "screen_status"));
            Assert.Equal(2, Int(repeatedMarkdown, "markdown_event_count_56d"));

            var reused = FindCandidate(rows, 13, 9008);
            Assert.Equal(4, Int(reused, "candidate_reuse_count"));
            Assert.Equal("candidate_reused_across_treated_events", Text(reused, "screen_status"));
            Assert.Equal("excluded_or_nonselected_candidate", Text(reused, "qualification_status"));
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    [Fact(DisplayName = "RQ612 query remains read-only, exploratory and explicit about historical evidence gaps")]
    public void Query_IsReadOnlyAndDoesNotClaimHistoricalAvailability()
    {
        var sql = LoadQuery();

        Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("min_gross_pre_units", sql, StringComparison.Ordinal);
        Assert.Contains("max_pre_price_deviation", sql, StringComparison.Ordinal);
        Assert.Contains("same_store_type_wrong_supplier", sql, StringComparison.Ordinal);
        Assert.Contains("candidate_reused_across_treated_events", sql, StringComparison.Ordinal);
        Assert.Contains("unverified_no_historical_daily_stockout_or_availability_evidence", sql, StringComparison.Ordinal);
        Assert.Contains("unverified_batch23_per_store_day_source_coverage", sql, StringComparison.Ordinal);
        Assert.Contains("DUG", sql, StringComparison.Ordinal);
        Assert.Contains("KOREKCIJA", sql, StringComparison.Ordinal);
    }

    private static async Task SeedAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            CREATE TABLE "Artikli" (
                "Id" integer PRIMARY KEY,
                "PLU" text,
                "IDObjekat" integer,
                "IDDobavljac" integer,
                "IDTipObuce" integer,
                "DataOrigin" text NOT NULL,
                "SourceBatchId" bigint
            );
            CREATE TABLE prodaja_zaglavlje (
                id integer PRIMARY KEY,
                id_objekat integer NOT NULL,
                datum_prodaje timestamp without time zone NOT NULL,
                broj_racuna text,
                source_batch_id bigint NOT NULL
            );
            CREATE TABLE prodaja_stavke (
                id integer PRIMARY KEY,
                id_prodaja integer NOT NULL,
                id_artikal integer NOT NULL,
                kolicina integer NOT NULL,
                cena numeric NOT NULL,
                nabavna_cena numeric,
                source_batch_id bigint NOT NULL
            );
            CREATE TABLE "DnevnikPromena" (
                "Id" integer PRIMARY KEY,
                "TipPromene" text,
                "Datum" timestamp with time zone NOT NULL,
                "StaraProdajnaCena" numeric,
                "NovaProdajnaCena" numeric,
                "ArtikalId" integer,
                "IDObjekat" integer
            );
            INSERT INTO "Artikli" ("Id", "PLU", "IDObjekat", "IDDobavljac", "IDTipObuce", "DataOrigin", "SourceBatchId") VALUES
                (9001, 'C-1', 2082886995, 1447386007, -424518832, 'access', 23),
                (9002, 'C-2', 2082886995, 1447386007, -424518832, 'access', 23),
                (9003, 'C-3', 2082886995, 123, -424518832, 'access', 23),
                (9004, 'C-4', 2082886995, 1447386007, -424518832, 'access', 23),
                (9005, 'C-5', 2082886995, 1447386007, -424518832, 'access', 23),
                (9006, 'C-6', 2082886995, 1447386007, -424518832, 'access', 23),
                (9007, 'C-7', 2082886995, 1447386007, -424518832, 'access', 23),
                (9008, 'C-8', 2082886995, 1681773579, -2004188974, 'access', 23);
            INSERT INTO prodaja_zaglavlje (id, id_objekat, datum_prodaje, broj_racuna, source_batch_id) VALUES
                (1, 2082886995, '2026-05-21 10:00:00', 'R-1', 23),
                (2, 2082886995, '2026-06-17 10:00:00', 'R-2', 23),
                (3, 2082886995, '2026-06-10 10:00:00', 'R-3', 23),
                (4, 2082886995, '2026-06-18 10:00:00', 'R-4', 23),
                (5, 2082886995, '2026-06-01 10:00:00', 'DUG', 23),
                (6, 2082886995, '2026-06-02 10:00:00', ' KOREKCIJA ', 23),
                (7, 2082886995, '2026-06-08 10:00:00', 'R-7', 23),
                (8, 2082886995, '2026-06-09 10:00:00', 'R-8', 23),
                (9, 2082886995, '2026-05-17 10:00:00', 'R-9', 23),
                (10, 2082886995, '2026-05-17 11:00:00', 'R-10', 23),
                (11, 2082886995, '2026-05-17 12:00:00', 'R-11', 23),
                (12, 2082886995, '2026-05-17 13:00:00', 'R-12', 23),
                (13, 2082886995, '2026-05-17 14:00:00', 'R-13', 23),
                (14, 2082886995, '2026-05-17 15:00:00', 'R-14', 23),
                (15, 2082886995, '2026-05-17 16:00:00', 'R-15', 23);
            INSERT INTO prodaja_stavke (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, source_batch_id) VALUES
                (1, 1, 9001, 1, 4990, NULL, 23),
                (2, 2, 9001, 3, 4990, 1200, 23),
                (3, 2, 9001, -1, 4990, NULL, 23),
                (4, 3, 21609, 3, 4990, 1000, 23),
                (5, 1, 9004, 1, 4990, 1000, 23),
                (6, 4, 9004, 5, 4990, 1000, 23),
                (7, 5, 9005, 2, 4990, 1000, 23),
                (8, 6, 9005, 3, 4990, 1000, 23),
                (9, 7, 9006, 3, 4990, 1000, 23),
                (10, 8, 9007, 3, 9000, 1000, 23),
                (11, 9, 9008, 3, 7000, 1000, 23),
                (12, 10, 21473, 3, 5990, 1000, 23),
                (13, 11, 21474, 3, 7990, 1000, 23),
                (14, 12, 21475, 3, 7990, 1000, 23),
                (15, 13, 21476, 3, 7490, 1000, 23);
            INSERT INTO "DnevnikPromena" ("Id", "TipPromene", "Datum", "StaraProdajnaCena", "NovaProdajnaCena", "ArtikalId", "IDObjekat") VALUES
                (1, 'Nivelacija cena', '2026-06-01 12:00:00+00', 6000, 5000, 9006, 2082886995),
                (2, 'Nivelacija', '2026-06-10 12:00:00+00', 5000, 4500, 9006, 2082886995);
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return rows;
    }

    private static Dictionary<string, object?> FindCandidate(
        IReadOnlyCollection<Dictionary<string, object?>> rows,
        int cohortNo,
        int articleId) => Assert.Single(rows.Where(r => Text(r, "record_type") == "candidate_detail"
                                                         && Int(r, "cohort_no") == cohortNo
                                                         && Int(r, "candidate_article_id") == articleId));

    private static string Text(Dictionary<string, object?> row, string column) => (string)row[column]!;
    private static int Int(Dictionary<string, object?> row, string column) => Convert.ToInt32(row[column]);
    private static decimal Decimal(Dictionary<string, object?> row, string column) => Convert.ToDecimal(row[column]);

    private async Task DropDatabaseAsync(string databaseName)
    {
        var admin = new NpgsqlConnectionStringBuilder(_fixture.AdminConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string LoadQuery()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "tools", "markdown-pilot-shadow-control-candidates.sql");
            if (File.Exists(path))
                return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException("RQ612 shadow-control query was not found from the test output directory.");
    }
}
