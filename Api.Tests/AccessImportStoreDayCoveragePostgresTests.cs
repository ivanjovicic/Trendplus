using Infrastructure.Migrations;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "RQ611")]
public sealed class AccessImportStoreDayCoveragePostgresTests : IClassFixture<Rq607PostgresFixture>
{
    private readonly Rq607PostgresFixture _fixture;

    public AccessImportStoreDayCoveragePostgresTests(Rq607PostgresFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "RQ611 oracle distinguishes sales, verified-empty and missing days; newer overlap invalidates prior proof")]
    public async Task Oracle_UsesLatestSourceGenerationAndDoesNotCarryOldDayProofForward()
    {
        var connectionString = await CreateReadyDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SeedSalesRowsAsync(connection);
        var dayCounts = await ReadImportedSalesDayCountsAsync(connection);
        Assert.Equal((1, new DateOnly(2026, 7, 4), 2, 2), dayCounts[0]);
        Assert.Equal((2, new DateOnly(2026, 7, 4), 1, 1), dayCounts[1]);

        await InsertBatchAsync(connection, 23, "/source/current.accdb", "completed", new DateTime(2026, 8, 29, 10, 0, 0, DateTimeKind.Utc));
        await InsertBatchAsync(connection, 24, "/source/current.accdb", "completed", new DateTime(2026, 8, 29, 11, 0, 0, DateTimeKind.Utc));
        await InsertCoverageAsync(connection, 23, new DateOnly(2026, 7, 4), "observed_sales", 2, 3);
        await InsertCoverageAsync(connection, 24, new DateOnly(2026, 7, 4), "observed_sales", 2, 3);
        await InsertCoverageAsync(connection, 24, new DateOnly(2026, 7, 5), "verified_empty", 0, 0);

        var initial = await ReadOracleAsync(connection);
        Assert.Equal(2, initial.Count);
        Assert.Equal(24L, initial[0].EffectiveBatchId);
        Assert.Equal(1, initial[0].ObservedSalesDays);
        Assert.Equal(1, initial[0].VerifiedEmptyDays);
        Assert.Equal(26, initial[0].MissingDays);
        Assert.Equal(24L, initial[1].EffectiveBatchId);
        Assert.Equal(28, initial[1].MissingDays);

        await InsertBatchAsync(connection, 25, "/source/current.accdb", "completed", new DateTime(2026, 8, 30, 11, 0, 0, DateTimeKind.Utc));
        await InsertCoverageAsync(connection, 25, new DateOnly(2026, 7, 4), "partial", 2, 2);
        var corrected = await ReadOracleAsync(connection);
        Assert.Equal(25L, corrected[0].EffectiveBatchId);
        Assert.Equal(0, corrected[0].ObservedSalesDays);
        Assert.Equal(0, corrected[0].VerifiedEmptyDays);
        Assert.Equal(1, corrected[0].PartialDays);

        await InsertBatchAsync(connection, 26, "/source/current.accdb", "failed", new DateTime(2026, 8, 31, 11, 0, 0, DateTimeKind.Utc));
        var failed = await ReadOracleAsync(connection);
        Assert.Equal(26L, failed[0].EffectiveBatchId);
        Assert.Equal(28, failed[0].FailedDays);
        Assert.Equal(0, failed[0].ObservedSalesDays);
    }

    private async Task<string> CreateReadyDatabaseAsync()
    {
        Assert.True(_fixture.IsAvailable, "RQ611 PostgreSQL certification requires disposable PostgreSQL; unavailable infrastructure must fail, not skip.");
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_rq611_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var setup = new NpgsqlCommand("""
            CREATE TABLE "DataImportBatches" (
                "Id" bigint PRIMARY KEY,
                "SourceSystem" varchar(64) NOT NULL,
                "SourceFileName" varchar(300) NOT NULL,
                "SourceFilePath" varchar(800),
                "SourceStorageKey" varchar(1024),
                "SourceStorageProvider" varchar(32),
                "StartedAtUtc" timestamptz NOT NULL,
                "Status" varchar(32) NOT NULL
            );
            """ + AddAccessImportStoreDayCoverage.CreateSql, connection);
        await setup.ExecuteNonQueryAsync();
        return connectionString!;
    }

    private static async Task InsertBatchAsync(
        NpgsqlConnection connection,
        long id,
        string sourcePath,
        string status,
        DateTime startedAtUtc)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "DataImportBatches" ("Id", "SourceSystem", "SourceFileName", "SourceFilePath", "StartedAtUtc", "Status")
            VALUES (@id, 'access', 'current.accdb', @path, @started, @status);
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("path", sourcePath);
        command.Parameters.AddWithValue("started", startedAtUtc);
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedSalesRowsAsync(NpgsqlConnection connection)
    {
        await using var setup = new NpgsqlCommand("""
            CREATE TABLE prodaja_zaglavlje (
                id integer PRIMARY KEY,
                id_objekat integer NULL,
                datum_prodaje timestamp without time zone NOT NULL,
                broj_racuna varchar(32) NULL,
                data_origin varchar(32) NOT NULL,
                source_batch_id bigint NULL
            );
            CREATE TABLE prodaja_stavke (
                id integer PRIMARY KEY,
                id_prodaja integer NOT NULL,
                kolicina integer NOT NULL
            );
            INSERT INTO prodaja_zaglavlje (id, id_objekat, datum_prodaje, broj_racuna, data_origin, source_batch_id)
            VALUES (1, 1, '2026-07-04 10:00:00', 'DUG', 'access', 23),
                   (2, 1, '2026-07-04 11:00:00', 'KOREKCIJA', 'access', 23),
                   (3, 2, '2026-07-04 12:00:00', 'R-3', 'access', 23);
            INSERT INTO prodaja_stavke (id, id_prodaja, kolicina)
            VALUES (10, 1, 2), (11, 2, -1), (12, 3, 3);
            """, connection);
        await setup.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<(int? StoreId, DateOnly BusinessDate, int Receipts, int Lines)>> ReadImportedSalesDayCountsAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(AccessImportService.StoreDaySourceAggregationSql, connection);
        command.Parameters.AddWithValue("batch_id", 23L);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(int? StoreId, DateOnly BusinessDate, int Receipts, int Lines)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.IsDBNull(0) ? null : reader.GetInt32(0),
                reader.GetFieldValue<DateOnly>(1), reader.GetInt32(2), reader.GetInt32(3)));
        }

        return rows;
    }

    private static async Task InsertCoverageAsync(
        NpgsqlConnection connection,
        long batchId,
        DateOnly date,
        string status,
        int receipts,
        int lines)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO access_import_store_day_coverage
                (source_batch_id, store_id, business_date, business_timezone, coverage_status,
                 source_scope_fingerprint, source_generation, source_header_table, source_line_table,
                 receipt_count, sale_line_count, captured_at_utc)
            VALUES (@batch, 1, @date, 'Europe/Belgrade', @status,
                    repeat('a', 64), @generation, 'prodaja_zaglavlje', 'prodaja_stavke',
                    @receipts, @lines, now());
            """, connection);
        command.Parameters.AddWithValue("batch", batchId);
        command.Parameters.AddWithValue("date", date);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("generation", $"access-batch:{batchId}");
        command.Parameters.AddWithValue("receipts", receipts);
        command.Parameters.AddWithValue("lines", lines);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<OracleWindow>> ReadOracleAsync(NpgsqlConnection connection)
    {
        var root = FindRepoRoot();
        var sql = await File.ReadAllTextAsync(Path.Combine(root, "tools", "markdown-pilot-store-day-coverage-oracle.sql"));
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<OracleWindow>();
        while (await reader.ReadAsync())
        {
            result.Add(new OracleWindow(
                reader.GetInt64(3),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11)));
        }

        return result;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MASTER_ROADMAP.md")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root for the RQ611 SQL oracle.");
    }

    private sealed record OracleWindow(long EffectiveBatchId, int ObservedSalesDays, int VerifiedEmptyDays, int PartialDays, int FailedDays, int MissingDays);
}
