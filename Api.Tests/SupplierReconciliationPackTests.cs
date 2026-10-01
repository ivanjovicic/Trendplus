using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using Xunit;

namespace Api.Tests;

/// <summary>
/// RQ524: executes scripts/check_supplier_reconciliation_pack.sql against deterministic
/// PostgreSQL fixtures and pins every verdict, owner and the read-only guarantee.
/// </summary>
public sealed class SupplierReconciliationPackTests : IClassFixture<PostgresContainerFixture>
{
    private const string PackPath = "scripts/check_supplier_reconciliation_pack.sql";
    private const string BootstrapPath = "scripts/fixtures/supplier-reconciliation-pack-bootstrap.sql";
    private const string VendorViewHistoryId = "Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql";
    private const string ScorecardRefreshHistoryId = "Database/Migrations/018_AddSupplierDecisionHubViews.sql#full-build";

    private static readonly string[] ExpectedCheckIds = Enumerable.Range(1, 16)
        .Select(i => $"SUP-{i:000}")
        .ToArray();

    private readonly PostgresContainerFixture _fixture;

    public SupplierReconciliationPackTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdversarialFixture_ProducesExpectedVerdictForEveryCheck_AndStaysReadOnly()
    {
        await using var db = await TryCreateDatabaseAsync("tp_sup_pack_adversarial");
        if (db is null)
        {
            return;
        }

        await ExecuteAsync(db.Connection, ReadRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql"));
        await ExecuteAsync(db.Connection, ReadRepoFile(BootstrapPath));
        await ExecuteAsync(
            db.Connection,
            $"""
            INSERT INTO "__StartupSqlScriptHistory" ("ScriptPath", "ScriptHash") VALUES
                ('{VendorViewHistoryId}', 'fixture'),
                ('{ScorecardRefreshHistoryId}', 'fixture');
            """);
        await SeedAdversarialAsync(db.Connection, db.Anchor);

        var before = await SnapshotAsync(db.Connection);
        var results = await RunPackAsync(db.Connection, db.Anchor);
        var after = await SnapshotAsync(db.Connection);

        Assert.Equal(before, after);
        AssertEveryCheckMapped(results);
        AssertVerdicts(results, new Dictionary<string, string>
        {
            ["SUP-001"] = "EXPLAINED",
            ["SUP-002"] = "EXPLAINED",
            ["SUP-003"] = "EXPLAINED",
            ["SUP-004"] = "EXPLAINED",
            ["SUP-005"] = "PASS",
            ["SUP-006"] = "PASS",
            ["SUP-007"] = "EXPLAINED",
            ["SUP-008"] = "EXPLAINED",
            ["SUP-009"] = "EXPLAINED",
            ["SUP-010"] = "PASS",
            ["SUP-011"] = "PASS",
            ["SUP-012"] = "PASS",
            ["SUP-013"] = "EXPLAINED",
            ["SUP-014"] = "EXPLAINED",
            ["SUP-015"] = "PASS",
            ["SUP-016"] = "PASS",
        });

        Assert.Equal("1", results["SUP-001"].Observed);
        Assert.Equal("1", results["SUP-002"].Observed);
        Assert.Equal("80.00%", results["SUP-003"].Observed);
        Assert.Equal("1", results["SUP-004"].Observed);
        Assert.Equal("1", results["SUP-007"].Observed);
        Assert.Equal("1", results["SUP-008"].Observed);
        Assert.Equal("1", results["SUP-009"].Observed);
        Assert.Equal("1", results["SUP-010"].Observed);
        Assert.Equal("0 missing of 4", results["SUP-011"].Observed);
        Assert.Equal("2 of 2 comparable; 2 not comparable", results["SUP-012"].Observed);
        Assert.Equal("1 of 4", results["SUP-014"].Observed);
        Assert.Equal("1", results["SUP-015"].Observed);
        Assert.Equal("immature_no_post=1; mature_no_post=2; violations=0", results["SUP-016"].Observed);
    }

    [Fact]
    public async Task MissingSchemaObjects_ReportOwnedFailuresInsteadOfAbortingThePack()
    {
        await using var db = await TryCreateDatabaseAsync("tp_sup_pack_missing");
        if (db is null)
        {
            return;
        }

        var t = db.Anchor;
        await ExecuteAsync(
            db.Connection,
            $"""
            INSERT INTO "Dobavljaci" VALUES (1, 'Alfa');
            INSERT INTO "Artikli" VALUES (101, 'A-101', 'Patika', 'Obuca', 1, 50, 0.5);
            INSERT INTO prodaja_zaglavlje VALUES (1, {Ts(t.AddDays(-10))}, 'R1', 1, 'existing');
            INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale)
                VALUES (1, 101, 1, 100, 50, 1);
            INSERT INTO "DnevnikPromena" ("Id", "ArtikalId", "Datum", "TipPromene", "StaraProdajnaCena", "NovaProdajnaCena", "DobavljacId", "IDObjekat")
                VALUES (1, 101, {Ts(t.AddDays(-60))}, 'Nivelacija', 120, 100, 1, NULL);
            """);

        var results = await RunPackAsync(db.Connection, t);

        AssertEveryCheckMapped(results);
        AssertVerdict(results, "SUP-005", "FAIL", "RQ518");
        Assert.Equal("missing", results["SUP-005"].Observed);
        AssertVerdict(results, "SUP-006", "FAIL", "RQ519");
        AssertVerdict(results, "SUP-010", "EXPLAINED", "RQ519");
        AssertVerdict(results, "SUP-011", "FAIL", "RQ522");
        Assert.Equal("1 missing of 1", results["SUP-011"].Observed);
        AssertVerdict(results, "SUP-012", "EXPLAINED", "RQ527");
        AssertVerdict(results, "SUP-013", "EXPLAINED", "RQ528");
        AssertVerdict(results, "SUP-014", "EXPLAINED", "RQ520");
        AssertVerdict(results, "SUP-015", "EXPLAINED", "RQ518");
        AssertVerdict(results, "SUP-016", "EXPLAINED", "RQ520");
    }

    [Fact]
    public async Task TamperedAssortmentView_FailsComparableTotalsAndFakeZeroImmatureContract()
    {
        await using var db = await TryCreateDatabaseAsync("tp_sup_pack_tampered");
        if (db is null)
        {
            return;
        }

        var t = db.Anchor;
        await ExecuteAsync(db.Connection, ReadRepoFile(BootstrapPath));
        await ExecuteAsync(
            db.Connection,
            $"""
            CREATE VIEW vw_vendor_sales_nivelacija AS
            SELECT * FROM (VALUES
                -- mature row whose change disagrees with post - pre
                (DATE '{t.AddDays(-60):yyyy-MM-dd}', 100::numeric(18,2), 90::numeric(18,2), -5::numeric, 0.1::numeric, true, NULL::text),
                -- immature row without post sales zero-filled instead of unknown
                (DATE '{t.AddDays(-5):yyyy-MM-dd}', 50::numeric(18,2), 0::numeric(18,2), -50::numeric, NULL::numeric, true, NULL::text)
            ) AS v(event_date, pre_revenue, post_revenue, change_revenue, coverage_post30, has_revenue_baseline, revenue_baseline_reason);
            """);

        var results = await RunPackAsync(db.Connection, t);

        AssertEveryCheckMapped(results);
        AssertVerdict(results, "SUP-003", "EXPLAINED", "RQ521");
        Assert.Equal("n/a (0 retail lines)", results["SUP-003"].Observed);
        AssertVerdict(results, "SUP-006", "PASS", "RQ519");
        AssertVerdict(results, "SUP-012", "FAIL", "RQ527");
        Assert.Equal("1 of 2 comparable; 0 not comparable", results["SUP-012"].Observed);
        AssertVerdict(results, "SUP-016", "FAIL", "RQ520");
        Assert.Equal("immature_no_post=1; mature_no_post=0; violations=1", results["SUP-016"].Observed);
    }

    [Fact]
    public void Pack_IsSelectOnlyInsideReadOnlyTransaction_AndHistoryIdsMatchStartup()
    {
        var pack = ReadRepoFile(PackPath);
        var statements = StripComments(pack);

        Assert.StartsWith("BEGIN TRANSACTION READ ONLY;", statements.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("ROLLBACK;", statements.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex(@"\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|REFRESH|GRANT|REVOKE|COPY|CALL|DO)\b", RegexOptions.IgnoreCase),
            Regex.Replace(statements, @"'(?:[^']|'')*'", "''"));

        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");
        Assert.Contains($"\"{VendorViewHistoryId}\"", initializer);
        Assert.Contains($"\"{ScorecardRefreshHistoryId}\"", initializer);
        Assert.Contains(VendorViewHistoryId, pack);
        Assert.Contains(ScorecardRefreshHistoryId, pack);
    }

    private static void AssertEveryCheckMapped(IReadOnlyDictionary<string, PackResult> results)
    {
        Assert.Equal(ExpectedCheckIds, results.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        foreach (var result in results.Values)
        {
            Assert.Contains(result.Verdict, new[] { "PASS", "FAIL", "EXPLAINED" });
            Assert.Matches(@"^RQ\d+$", result.Owner);
            Assert.False(string.IsNullOrWhiteSpace(result.Observed), $"{result.CheckId} has no observed value.");
        }
    }

    private static void AssertVerdicts(
        IReadOnlyDictionary<string, PackResult> results,
        IReadOnlyDictionary<string, string> expected)
    {
        var actual = results.ToDictionary(r => r.Key, r => $"{r.Value.Verdict} ({r.Value.Observed})");
        foreach (var (checkId, verdict) in expected)
        {
            Assert.True(
                results[checkId].Verdict == verdict,
                $"{checkId}: expected {verdict}, got {actual[checkId]}. All: {string.Join("; ", actual.Select(a => $"{a.Key}={a.Value}"))}");
        }
    }

    private static void AssertVerdict(
        IReadOnlyDictionary<string, PackResult> results,
        string checkId,
        string verdict,
        string owner)
    {
        var result = results[checkId];
        Assert.True(
            result.Verdict == verdict && result.Owner == owner,
            $"{checkId}: expected {verdict}/{owner}, got {result.Verdict}/{result.Owner} ({result.Observed}).");
    }

    private static async Task SeedAdversarialAsync(NpgsqlConnection connection, DateOnly t)
    {
        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO "Dobavljaci" VALUES (1, 'Alfa'), (2, 'Beta'), (3, 'Gama');
            INSERT INTO "Artikli" VALUES
                (101, 'A-101', 'Patika', 'Obuca', 1, 50, 0.5),
                (102, 'B-102', 'Cizma', 'Obuca', 2, 1000, 1),
                (103, 'U-103', 'Sandala', 'Obuca', NULL, 40, NULL),
                (104, 'G-104', 'Papuca', 'Obuca', 3, 30, NULL),
                (105, 'A-105', 'Mokasina', 'Obuca', 1, NULL, NULL);

            INSERT INTO prodaja_zaglavlje VALUES
                (1,  {Ts(t.AddDays(-10))},  'R1',   1, 'existing'),
                (2,  {Ts(t.AddDays(-10))},  'R2',   1, 'existing'),
                (3,  {Ts(t.AddDays(-9))},   'R3',   2, 'existing'),
                (4,  {Ts(t.AddDays(-8))},   'DUG',  1, 'existing'),
                (5,  {Ts(t.AddDays(-7))},   'R5',   1, 'existing'),
                (6,  {Ts(t.AddDays(-6))},   'R6',   1, 'existing'),
                (7,  {Ts(t.AddDays(-40))},  'R7',   1, 'existing'),
                (8,  {Ts(t.AddDays(-40))},  'R8',   1, 'existing'),
                (9,  {Ts(t.AddDays(-75))},  'R9',   1, 'existing'),
                (10, {Ts(t.AddDays(-210))}, 'R10',  1, 'existing');

            INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale) VALUES
                (1, 101, 2, 100, 50, 1),
                -- sale-time supplier differs from current master supplier (SUP-009)
                (2, 102, 1, 2000, NULL, 1),
                -- unknown supplier (SUP-001)
                (3, 103, 1, 80, 40, NULL),
                -- DUG receipt (SUP-002)
                (4, 101, 1, 100, 50, 1),
                -- signed return (SUP-007)
                (5, 101, -1, 100, 50, 1),
                -- no cost on line or product (SUP-003)
                (6, 105, 1, 60, NULL, 1),
                -- previous window only: supplier 3 (SUP-008); supplier 1 also sells now
                (7, 104, 1, 90, 30, 3),
                (8, 101, 1, 100, 50, 1),
                (9, 101, 1, 120, 50, 1),
                (10, 102, 1, 2200, NULL, 2);

            INSERT INTO "DnevnikPromena" ("Id", "ArtikalId", "Datum", "TipPromene", "StaraProdajnaCena", "NovaProdajnaCena", "DobavljacId", "IDObjekat") VALUES
                -- mature with post sales
                (1, 101, {Ts(t.AddDays(-60))},  'Nivelacija', 120, 100, 1, 1),
                -- mature without post sales: explicit zero
                (2, 102, {Ts(t.AddDays(-200))}, 'Nivelacija', 2200, 2000, 2, 1),
                -- immature without post sales: unknown, never zero
                (3, 105, {Ts(t.AddDays(-5))},   'Nivelacija', 70, 60, 1, 1),
                -- mature without pre window: baseline flagged
                (4, 103, {Ts(t.AddDays(-100))}, 'Nivelacija', 90, 80, NULL, 2);
            """);
    }

    private static async Task<Dictionary<string, PackResult>> RunPackAsync(NpgsqlConnection connection, DateOnly anchor)
    {
        var fromUtc = anchor.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00Z";
        var toUtc = anchor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T23:59:59.999999Z";
        var sql = ReadRepoFile(PackPath)
            .Replace(":'from_utc'", $"'{fromUtc}'", StringComparison.Ordinal)
            .Replace(":'to_utc'", $"'{toUtc}'", StringComparison.Ordinal)
            .Replace(":'store_id'", "'NULL'", StringComparison.Ordinal);

        var results = new Dictionary<string, PackResult>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            do
            {
                if (reader.FieldCount == 0 || reader.GetName(0) != "check_id")
                {
                    continue;
                }

                while (await reader.ReadAsync())
                {
                    var result = new PackResult(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        reader.GetString(4));
                    results.Add(result.CheckId, result);
                }
            }
            while (await reader.NextResultAsync());
        }

        Assert.Equal("off", await ScalarAsync<string>(connection, "SELECT current_setting('transaction_read_only');"));
        return results;
    }

    private static async Task<string> SnapshotAsync(NpgsqlConnection connection) =>
        await ScalarAsync<string>(
            connection,
            """
            SELECT concat_ws('|',
                (SELECT md5(COALESCE(string_agg(t::text, ',' ORDER BY t::text), '')) FROM "Dobavljaci" t),
                (SELECT md5(COALESCE(string_agg(t::text, ',' ORDER BY t::text), '')) FROM "Artikli" t),
                (SELECT md5(COALESCE(string_agg(t::text, ',' ORDER BY t::text), '')) FROM "DnevnikPromena" t),
                (SELECT md5(COALESCE(string_agg(t::text, ',' ORDER BY t::text), '')) FROM prodaja_zaglavlje t),
                (SELECT md5(COALESCE(string_agg(t::text, ',' ORDER BY t::text), '')) FROM prodaja_stavke t),
                (SELECT md5(COALESCE(string_agg(t::text, ',' ORDER BY t::text), '')) FROM "__StartupSqlScriptHistory" t),
                (SELECT md5(COALESCE(string_agg(t::text, ',' ORDER BY t::text), '')) FROM mv_supplier_decision_score_cache t));
            """);

    private async Task<PackDatabase?> TryCreateDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable)
        {
            return null;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE "Dobavljaci" ("Id" integer PRIMARY KEY, "Naziv" text);
            CREATE TABLE "Artikli" (
                "Id" integer PRIMARY KEY,
                "PLU" text,
                "Naziv" text,
                "Kategorija" text,
                "IDDobavljac" integer,
                "NabavnaCenaDin" numeric(18,2),
                "NabavnaCena" numeric(18,2)
            );
            CREATE TABLE "DnevnikPromena" (
                "Id" integer PRIMARY KEY,
                "ArtikalId" integer,
                "Datum" timestamp NOT NULL,
                "TipPromene" text NOT NULL,
                "StaraProdajnaCena" numeric(18,2),
                "NovaProdajnaCena" numeric(18,2),
                "DobavljacId" integer,
                "IDObjekat" integer,
                "DataOrigin" text,
                "BrojRacuna" text
            );
            CREATE TABLE prodaja_zaglavlje (
                id integer PRIMARY KEY,
                datum_prodaje timestamp NOT NULL,
                broj_racuna text,
                id_objekat integer,
                data_origin text
            );
            CREATE TABLE prodaja_stavke (
                id serial PRIMARY KEY,
                id_prodaja integer NOT NULL,
                id_artikal integer NOT NULL,
                kolicina integer NOT NULL,
                cena numeric(18,2) NOT NULL,
                nabavna_cena numeric(18,2),
                supplier_id_at_sale integer
            );
            """);

        var anchor = DateOnly.FromDateTime(await ScalarAsync<DateTime>(connection, "SELECT CURRENT_DATE::timestamp;"));
        return new PackDatabase(connection, anchor);
    }

    private static string StripComments(string sql) =>
        string.Join('\n', sql.Split('\n').Select(line =>
        {
            var index = line.IndexOf("--", StringComparison.Ordinal);
            return index >= 0 ? line[..index] : line;
        }));

    private static string Ts(DateOnly day) => $"TIMESTAMP '{day:yyyy-MM-dd} 12:00:00'";

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Expected scalar result for SQL: {sql}"));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
        return File.ReadAllText(Path.Combine(root, relativePath)).ReplaceLineEndings("\n");
    }

    private sealed record PackResult(string CheckId, string Verdict, string Observed, string Owner);

    private sealed record PackDatabase(NpgsqlConnection Connection, DateOnly Anchor) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
