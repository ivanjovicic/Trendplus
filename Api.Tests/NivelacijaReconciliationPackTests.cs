using System.Text.RegularExpressions;
using Npgsql;
using Xunit;

namespace Api.Tests;

/// <summary>Fixture proof for the read-only RQ547 evidence queries.</summary>
public sealed class NivelacijaReconciliationPackTests : IClassFixture<PostgresContainerFixture>
{
    private const string BootstrapPath = "scripts/fixtures/nivelacija-reconciliation-bootstrap.sql";
    private const string ScriptDirectory = "scripts/sql/nivelacija-reconciliation";
    private static readonly string[] ScriptNames = Enumerable.Range(1, 9)
        .Select(index => $"R{index}-{new[] { "event-type-inventory", "numeric-receipt-length", "history-and-control-match", "event-overlap-and-price-shape", "null-store-by-origin", "vendor-view-parity", "sale-timestamp-and-local-band", "price-and-cost-vat-sample", "view-access-and-columns" }[index - 1]}.sql")
        .ToArray();
    private static readonly string[] CheckIds = Enumerable.Range(1, 9).Select(index => $"NV-P1-R{index}").ToArray();

    private readonly PostgresContainerFixture _fixture;

    public NivelacijaReconciliationPackTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PassingFixture_ReportsEvidenceForEveryCheck_AndLeavesSourceUntouched()
    {
        await using var db = await TryCreateDatabaseAsync("tp_nivelacija_recon_pass");
        if (db is null) return;

        await SeedPassingFixtureAsync(db);
        var before = await SnapshotAsync(db.Connection);
        var results = await RunAllAsync(db.Connection);
        var after = await SnapshotAsync(db.Connection);

        Assert.Equal(before, after);
        Assert.Equal(CheckIds, results.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        Assert.All(results.Values, result => Assert.Equal("PASS", result.Verdict));
        Assert.Contains("max_raw_digits=n/a", results["NV-P1-R2"].Observed);
        Assert.Contains("independent_window_days=30", results["NV-P1-R6"].Observed);
        Assert.Contains("timestamp with time zone", results["NV-P1-R7"].Observed);
        Assert.Contains("select_privilege=", results["NV-P1-R9"].Observed);
    }

    [Fact]
    public async Task AdverseFixture_ReportsEachDataAnomalyWithoutWriting()
    {
        await using var db = await TryCreateDatabaseAsync("tp_nivelacija_recon_adverse");
        if (db is null) return;

        await SeedPassingFixtureAsync(db);
        await ExecuteAsync(db.Connection,
            """
            INSERT INTO "DnevnikPromena" VALUES
                (2, 101, DATE '2026-07-15', 'Re-nivelacija', 'manual-ref', 110, 110, 1, NULL, 'legacy'),
                (3, 101, DATE '2026-07-15', 'Nivelacija', '123456789012', 110, 120, 1, NULL, 'existing');
            INSERT INTO price_history VALUES (2, 101);
            INSERT INTO nivelacija_did_fixture VALUES (101, 101);
            INSERT INTO nivelacija_view_projection VALUES (2, DATE '2026-07-15', 101, 30, 30);
            UPDATE nivelacija_view_projection SET post_qty = 999 WHERE price_event_id = 1;
            INSERT INTO prodaja_zaglavlje VALUES (1000, TIMESTAMPTZ '2026-09-15 22:30:00+00', 'R-LATE', 1, 'imported');
            INSERT INTO prodaja_stavke VALUES (1000, 101, 1, 111, 90);
            """);

        var before = await SnapshotAsync(db.Connection);
        var results = await RunAllAsync(db.Connection);
        var after = await SnapshotAsync(db.Connection);

        Assert.Equal(before, after);
        Assert.Equal("WARN", results["NV-P1-R1"].Verdict);
        Assert.Equal("FAIL", results["NV-P1-R2"].Verdict);
        Assert.Equal("FAIL", results["NV-P1-R3"].Verdict);
        Assert.Equal("WARN", results["NV-P1-R4"].Verdict);
        Assert.Equal("WARN", results["NV-P1-R5"].Verdict);
        Assert.Equal("WARN", results["NV-P1-R6"].Verdict);
        Assert.Equal("WARN", results["NV-P1-R7"].Verdict);
        Assert.Equal("WARN", results["NV-P1-R8"].Verdict);
        Assert.Equal("PASS", results["NV-P1-R9"].Verdict);
        Assert.Contains("integer_overflow_rows=1", results["NV-P1-R2"].Observed);
        Assert.Contains("next_event_within_30d=1", results["NV-P1-R4"].Observed);
        Assert.Contains("null_store=1", results["NV-P1-R5"].Observed);
        Assert.Contains("view_query_skipped_for_cast_risk=t", results["NV-P1-R6"].Observed);
        Assert.Contains("sales_22_00_to_02_00_local=1", results["NV-P1-R7"].Observed);
    }

    [Fact]
    public async Task MissingVendorView_IsAStableR9Failure()
    {
        await using var db = await TryCreateDatabaseAsync("tp_nivelacija_recon_missing_view");
        if (db is null) return;

        await ExecuteAsync(db.Connection, "DROP VIEW vw_vendor_sales_nivelacija;");
        var result = await RunScriptAsync(db.Connection, "R9-view-access-and-columns.sql");

        Assert.Equal("FAIL", result.Verdict);
        Assert.Contains("to_regclass=missing", result.Observed);
        var parity = await RunScriptAsync(db.Connection, "R6-vendor-view-parity.sql");
        Assert.Equal("FAIL", parity.Verdict);
        Assert.Contains("view_exists=f;", parity.Observed);
    }

    [Fact]
    public async Task TamperedProjection_FailsR6IndependentThirtyDayParity()
    {
        await using var db = await TryCreateDatabaseAsync("tp_nivelacija_recon_tampered_view");
        if (db is null) return;

        await SeedPassingFixtureAsync(db);
        await ExecuteAsync(db.Connection, "UPDATE nivelacija_view_projection SET pre_qty = 999 WHERE price_event_id = 1;");
        var result = await RunScriptAsync(db.Connection, "R6-vendor-view-parity.sql");

        Assert.Equal("FAIL", result.Verdict);
        Assert.Contains("pre_qty_mismatches=1", result.Observed);
    }

    [Fact]
    public async Task UnprivilegedCaller_IsAStableR9Failure()
    {
        await using var db = await TryCreateDatabaseAsync("tp_nivelacija_recon_no_select");
        if (db is null) return;

        var role = $"rq547_reader_{Guid.NewGuid():N}";
        await ExecuteAsync(db.Connection, $"CREATE ROLE {role}; GRANT USAGE ON SCHEMA public TO {role}; SET ROLE {role};");
        CheckResult result;
        try
        {
            result = await RunScriptAsync(db.Connection, "R9-view-access-and-columns.sql");
        }
        finally
        {
            await ExecuteAsync(db.Connection, "RESET ROLE;");
        }

        Assert.Equal("FAIL", result.Verdict);
        Assert.Contains("select_privilege=f;", result.Observed);
    }

    [Fact]
    public void EverySqlFile_IsSelectOnlyInsideReadOnlyTransaction()
    {
        foreach (var name in ScriptNames)
        {
            var sql = StripComments(ReadRepoFile($"{ScriptDirectory}/{name}"));
            Assert.StartsWith("BEGIN TRANSACTION READ ONLY;", sql.TrimStart(), StringComparison.Ordinal);
            Assert.EndsWith("ROLLBACK;", sql.TrimEnd(), StringComparison.Ordinal);
            var withoutLiterals = Regex.Replace(sql, @"'(?:[^']|'')*'", "''");
            Assert.DoesNotMatch(
                new Regex(@"\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|REFRESH|GRANT|REVOKE|COPY|CALL|DO)\b", RegexOptions.IgnoreCase),
                withoutLiterals);
        }
    }

    private async Task<FixtureDatabase?> TryCreateDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable) return null;
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, ReadRepoFile(BootstrapPath));
        return new FixtureDatabase(connection);
    }

    private static async Task SeedPassingFixtureAsync(FixtureDatabase db)
    {
        await ExecuteAsync(db.Connection,
            """
            INSERT INTO "Artikli" VALUES (101, 'A-101', 'Patika', 'Obuca', 1, 110, 91.67);
            INSERT INTO "DnevnikPromena" VALUES
                (1, 101, DATE '2026-07-01', 'Nivelacija', 'receipt-A', 100, 110, 1, 1, 'existing');
            INSERT INTO price_history VALUES (1, 101);
            INSERT INTO nivelacija_did_fixture VALUES (101, 202);
            INSERT INTO nivelacija_view_projection VALUES (1, DATE '2026-07-01', 101, 30, 30);

            INSERT INTO prodaja_zaglavlje (id, datum_prodaje, broj_racuna, id_objekat, data_origin)
            SELECT day_number + 1,
                   ((DATE '2026-06-01' + day_number)::timestamp + TIME '12:00') AT TIME ZONE 'UTC',
                   'PRE-' || day_number, 1, 'existing'
            FROM generate_series(0, 29) AS day_number;
            INSERT INTO prodaja_zaglavlje (id, datum_prodaje, broj_racuna, id_objekat, data_origin)
            SELECT day_number + 101,
                   ((DATE '2026-07-01' + day_number)::timestamp + TIME '12:00') AT TIME ZONE 'UTC',
                   'POST-' || day_number, 1, 'existing'
            FROM generate_series(0, 29) AS day_number;
            INSERT INTO prodaja_zaglavlje VALUES
                (500, TIMESTAMPTZ '2026-05-31 12:00:00+00', 'PRE-OUTSIDE', 1, 'existing'),
                (501, TIMESTAMPTZ '2026-07-31 12:00:00+00', 'POST-OUTSIDE', 1, 'existing');
            INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena, nabavna_cena)
            SELECT id, 101, 1, 110, 91.67 FROM prodaja_zaglavlje;
            """);
    }

    private static async Task<Dictionary<string, CheckResult>> RunAllAsync(NpgsqlConnection connection)
    {
        var results = new Dictionary<string, CheckResult>(StringComparer.Ordinal);
        foreach (var name in ScriptNames)
        {
            var result = await RunScriptAsync(connection, name);
            results.Add(result.CheckId, result);
        }
        return results;
    }

    private static async Task<CheckResult> RunScriptAsync(NpgsqlConnection connection, string name)
    {
        CheckResult result;
        await using (var command = new NpgsqlCommand(ReadRepoFile($"{ScriptDirectory}/{name}"), connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            do
            {
                if (reader.FieldCount < 6 || reader.GetName(0) != "check_id") continue;
                Assert.True(await reader.ReadAsync(), $"{name} returned no evidence row.");
                result = new CheckResult(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5));
                goto Found;
            }
            while (await reader.NextResultAsync());
            throw new InvalidOperationException($"{name} did not return a check row.");
        }

    Found:
        Assert.Equal("off", await ScalarAsync<string>(connection, "SELECT current_setting('transaction_read_only');"));
        return result;
    }

    private static async Task<string> SnapshotAsync(NpgsqlConnection connection) =>
        await ScalarAsync<string>(connection,
            """
            SELECT concat_ws('|',
                (SELECT md5(COALESCE(string_agg(to_jsonb(t)::text, ',' ORDER BY to_jsonb(t)::text), '')) FROM "Artikli" t),
                (SELECT md5(COALESCE(string_agg(to_jsonb(t)::text, ',' ORDER BY to_jsonb(t)::text), '')) FROM "DnevnikPromena" t),
                (SELECT md5(COALESCE(string_agg(to_jsonb(t)::text, ',' ORDER BY to_jsonb(t)::text), '')) FROM prodaja_zaglavlje t),
                (SELECT md5(COALESCE(string_agg(to_jsonb(t)::text, ',' ORDER BY to_jsonb(t)::text), '')) FROM prodaja_stavke t),
                (SELECT md5(COALESCE(string_agg(to_jsonb(t)::text, ',' ORDER BY to_jsonb(t)::text), '')) FROM price_history t),
                (SELECT md5(COALESCE(string_agg(to_jsonb(t)::text, ',' ORDER BY to_jsonb(t)::text), '')) FROM nivelacija_did_fixture t),
                (SELECT md5(COALESCE(string_agg(to_jsonb(t)::text, ',' ORDER BY to_jsonb(t)::text), '')) FROM nivelacija_view_projection t));
            """);

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected SQL scalar."));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private static string StripComments(string sql) =>
        string.Join('\n', sql.Split('\n').Select(line =>
        {
            var index = line.IndexOf("--", StringComparison.Ordinal);
            return index >= 0 ? line[..index] : line;
        }));

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
        return File.ReadAllText(Path.Combine(root, relativePath)).ReplaceLineEndings("\n");
    }

    private sealed record CheckResult(string CheckId, string Verdict, string Observed, string Expected, string Owner, string Detail);

    private sealed record FixtureDatabase(NpgsqlConnection Connection) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
