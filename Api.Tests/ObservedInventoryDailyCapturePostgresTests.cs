using System.Diagnostics;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "RQ610")]
public sealed class ObservedInventoryDailyCapturePostgresTests : IClassFixture<Rq607PostgresFixture>
{
    private readonly Rq607PostgresFixture _fixture;

    public ObservedInventoryDailyCapturePostgresTests(Rq607PostgresFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "RQ610 captures observed zero and negative stock, omits null, and never fans global stock out to two stores")]
    public async Task Capture_PreservesQuantityAndStoreGrainEvidence()
    {
        var connectionString = await CreateReadyDatabaseAsync("grain");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SeedStoresAndProductsAsync(connection);

        var observation = new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero);
        var runId = await StartRunAsync(connection, new DateOnly(2026, 10, 10), observation);
        Assert.Equal(2, await CaptureAsync(connection, runId));
        Assert.Equal(2, await CaptureAsync(connection, await StartRunAsync(connection, new DateOnly(2026, 10, 10), observation)));

        await using var verify = new NpgsqlCommand("""
            SELECT article_id, store_id, on_hand_qty, observed_at_utc, source_batch_id,
                   source_row_updated_through_utc, source_data_origin, run_state
              FROM analytics_intel.inventory_observed_daily_snapshot
             ORDER BY article_id;
            """, connection);
        await using var reader = await verify.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(101, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
        Assert.Equal(0m, reader.GetDecimal(2));
        Assert.Equal(observation.UtcDateTime, reader.GetDateTime(3));
        Assert.True(reader.IsDBNull(4)); // no source batch id exists in ProductsDim
        Assert.Equal(new DateTime(2026, 10, 10, 22, 0, 0, DateTimeKind.Utc), reader.GetDateTime(5));
        Assert.Equal("access", reader.GetString(6));
        Assert.Equal("succeeded", reader.GetString(7));

        Assert.True(await reader.ReadAsync());
        Assert.Equal(102, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
        Assert.Equal(-2m, reader.GetDecimal(2));
        Assert.False(await reader.ReadAsync()); // NULL article quantity is not a verified zero

        await reader.DisposeAsync();
        await using var storeProof = new NpgsqlCommand("""
            SELECT count(*) FILTER (WHERE store_id IN (11, 22)), count(*) FILTER (WHERE store_id = 0)
              FROM analytics_intel.inventory_observed_daily_snapshot;
            """, connection);
        await using var counts = await storeProof.ExecuteReaderAsync();
        Assert.True(await counts.ReadAsync());
        Assert.Equal(0L, counts.GetInt64(0));
        Assert.Equal(2L, counts.GetInt64(1));
    }

    [Fact(DisplayName = "RQ610 excludes late imports from earlier observations and same-day retry sees the later source generation")]
    public async Task Capture_DoesNotBackdateLateSourceRows()
    {
        var connectionString = await CreateReadyDatabaseAsync("late");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SeedStoresAndProductsAsync(connection);

        var firstObservation = new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero);
        Assert.Equal(2, await CaptureAsync(connection, await StartRunAsync(connection, new DateOnly(2026, 10, 10), firstObservation)));
        await using (var lateImport = new NpgsqlCommand("""
            INSERT INTO "ProductsDim" ("ProductId", "Kolicina", "Timestamp", "DataOrigin")
            VALUES (103, 8, '2026-10-11 00:10:00+00', 'access');
            """, connection))
        {
            await lateImport.ExecuteNonQueryAsync();
        }

        Assert.Equal(2, await CaptureAsync(connection, await StartRunAsync(connection, new DateOnly(2026, 10, 10), firstObservation)));
        await using (var countBeforeLateObservation = new NpgsqlCommand(
                         "SELECT count(*) FROM analytics_intel.inventory_observed_daily_snapshot WHERE article_id = 103;", connection))
            Assert.Equal(0L, (long)(await countBeforeLateObservation.ExecuteScalarAsync())!);

        var laterObservation = new DateTimeOffset(2026, 10, 11, 0, 11, 0, TimeSpan.Zero);
        Assert.Equal(3, await CaptureAsync(connection, await StartRunAsync(connection, new DateOnly(2026, 10, 10), laterObservation)));
        await using var verify = new NpgsqlCommand("""
            SELECT snapshot_date, on_hand_qty, observed_at_utc, source_row_updated_through_utc
              FROM analytics_intel.inventory_observed_daily_snapshot
             WHERE article_id = 103;
            """, connection);
        await using var reader = await verify.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(new DateTime(2026, 10, 10), reader.GetDateTime(0));
        Assert.Equal(8m, reader.GetDecimal(1));
        Assert.Equal(laterObservation.UtcDateTime, reader.GetDateTime(2));
        Assert.Equal(new DateTime(2026, 10, 11, 0, 10, 0, DateTimeKind.Utc), reader.GetDateTime(3));
    }

    [Fact(DisplayName = "RQ610 concurrent same-day captures serialize and keep one row per source grain")]
    public async Task Capture_ConcurrentRetriesRemainIdempotent()
    {
        var connectionString = await CreateReadyDatabaseAsync("concurrency");
        await using var connectionOne = new NpgsqlConnection(connectionString);
        await using var connectionTwo = new NpgsqlConnection(connectionString);
        await connectionOne.OpenAsync();
        await connectionTwo.OpenAsync();
        await SeedStoresAndProductsAsync(connectionOne);
        var observation = new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero);
        var firstRun = await StartRunAsync(connectionOne, new DateOnly(2026, 10, 10), observation);
        var secondRun = await StartRunAsync(connectionOne, new DateOnly(2026, 10, 10), observation);

        var results = await Task.WhenAll(
            CaptureAsync(connectionOne, firstRun),
            CaptureAsync(connectionTwo, secondRun));

        Assert.Equal(new[] { 2, 2 }, results.Order().ToArray());
        await using var verify = new NpgsqlCommand("""
            SELECT count(*), count(DISTINCT (article_id, store_id, snapshot_date))
              FROM analytics_intel.inventory_observed_daily_snapshot;
            """, connectionOne);
        await using var reader = await verify.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(2L, reader.GetInt64(1));
    }

    [Fact(DisplayName = "RQ610 failed capture rolls back partial rows, preserves prior snapshot and exposes incomplete state")]
    public async Task Capture_RollsBackAndMarksFailedRun()
    {
        var connectionString = await CreateReadyDatabaseAsync("rollback");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SeedStoresAndProductsAsync(connection);
        var observation = new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero);
        Assert.Equal(2, await CaptureAsync(connection, await StartRunAsync(connection, new DateOnly(2026, 10, 10), observation)));

        await using (var trigger = new NpgsqlCommand("""
            CREATE FUNCTION analytics_intel.reject_test_capture() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.article_id = 103 THEN RAISE EXCEPTION 'forced capture rollback'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_test_capture BEFORE INSERT OR UPDATE ON analytics_intel.inventory_observed_daily_snapshot
                FOR EACH ROW EXECUTE FUNCTION analytics_intel.reject_test_capture();
            INSERT INTO "ProductsDim" ("ProductId", "Kolicina", "Timestamp", "DataOrigin")
            VALUES (103, 8, '2026-10-10 22:30:00+00', 'access');
            """, connection))
            await trigger.ExecuteNonQueryAsync();

        var failedRunId = await StartRunAsync(connection, new DateOnly(2026, 10, 10), observation.AddMinutes(1));
        await using var tx = await connection.BeginTransactionAsync();
        var failure = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var command = new NpgsqlCommand(
                "SELECT analytics_intel.capture_observed_inventory_daily(@run_id);", connection, tx);
            command.Parameters.AddWithValue("run_id", failedRunId);
            await command.ExecuteScalarAsync();
        });
        Assert.Equal("P0001", failure.SqlState);
        await tx.RollbackAsync();
        await using (var markFailed = new NpgsqlCommand("""
            UPDATE analytics_intel.inventory_observed_daily_capture_run
               SET run_state = 'failed', error_code = 'P0001', completed_at_utc = clock_timestamp()
             WHERE run_id = @run_id;
            """, connection))
        {
            markFailed.Parameters.AddWithValue("run_id", failedRunId);
            await markFailed.ExecuteNonQueryAsync();
        }

        await using var verify = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM analytics_intel.inventory_observed_daily_snapshot),
                   (SELECT count(*) FROM analytics_intel.inventory_observed_daily_snapshot WHERE article_id = 103),
                   (SELECT run_state FROM analytics_intel.inventory_observed_daily_capture_run WHERE run_id = @run_id);
            """, connection);
        verify.Parameters.AddWithValue("run_id", failedRunId);
        await using var reader = await verify.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(0L, reader.GetInt64(1));
        Assert.Equal("failed", reader.GetString(2));
    }

    [Fact(DisplayName = "RQ610 SQL rejects a business date that would backdate a later observation")]
    public async Task Capture_RejectsMismatchedHistoricalBusinessDate()
    {
        var connectionString = await CreateReadyDatabaseAsync("date_guard");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SeedStoresAndProductsAsync(connection);
        var runId = await StartRunAsync(
            connection,
            new DateOnly(2026, 10, 9),
            new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero));

        var error = await Assert.ThrowsAsync<PostgresException>(async () => await CaptureAsync(connection, runId));
        Assert.Equal("22023", error.SqlState);
        await using var verify = new NpgsqlCommand("""
            SELECT count(*) FROM analytics_intel.inventory_observed_daily_snapshot;
            """, connection);
        Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync())!);
    }

    [Fact(DisplayName = "RQ610 observed capture handles a 2,000 article generation within the local performance budget")]
    public async Task Capture_PerformanceForTwoThousandArticles()
    {
        var connectionString = await CreateReadyDatabaseAsync("performance");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SeedStoresAndProductsAsync(connection);
        await using (var bulk = new NpgsqlCommand("""
            INSERT INTO "ProductsDim" ("ProductId", "Kolicina", "Timestamp", "DataOrigin")
            SELECT 1000 + n, n % 17, '2026-10-10 21:00:00+00', 'access'
              FROM generate_series(1, 2000) n;
            """, connection))
            await bulk.ExecuteNonQueryAsync();

        var timer = Stopwatch.StartNew();
        var count = await CaptureAsync(
            connection,
            await StartRunAsync(connection, new DateOnly(2026, 10, 10), new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero)));
        timer.Stop();
        Assert.Equal(2002, count);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(15), $"2,002-row disposable PostgreSQL capture took {timer.Elapsed}.");
    }

    private async Task<string> CreateReadyDatabaseAsync(string suffix)
    {
        Assert.True(_fixture.IsAvailable, "RQ610 certification requires PostgreSQL; unavailable infrastructure must fail, not skip.");
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_rq610_{suffix}_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var setup = new NpgsqlCommand("""
            CREATE SCHEMA analytics_intel;
            CREATE TABLE "ProductsDim" (
                "ProductId" integer NOT NULL,
                "Kolicina" integer NULL,
                "Timestamp" timestamptz NOT NULL,
                "DataOrigin" varchar(32) NOT NULL
            );
            CREATE INDEX ix_test_products_timestamp_product ON "ProductsDim" ("Timestamp", "ProductId");
            CREATE TABLE "StoresDim" ("StoreId" integer PRIMARY KEY);
            CREATE VIEW analytics_intel.vw_inventory_risk_signals_v1 AS
                SELECT 0::integer AS article_id, DATE '2000-01-01' AS date, 0::numeric AS stock_qty WHERE false;
            """, connection);
        await setup.ExecuteNonQueryAsync();
        var root = FindRepoRoot();
        var sql = await File.ReadAllTextAsync(Path.Combine(root, "Database", "Analytics", "Intelligence", "025_observed_inventory_daily_snapshot_v1.sql"));
        await using var apply = new NpgsqlCommand(sql, connection);
        await apply.ExecuteNonQueryAsync();
        await using var rerun = new NpgsqlCommand(sql, connection);
        await rerun.ExecuteNonQueryAsync();
        return connectionString!;
    }

    private static async Task SeedStoresAndProductsAsync(NpgsqlConnection connection)
    {
        await using var seed = new NpgsqlCommand("""
            INSERT INTO "StoresDim" ("StoreId") VALUES (11), (22);
            INSERT INTO "ProductsDim" ("ProductId", "Kolicina", "Timestamp", "DataOrigin") VALUES
                (101, 0, '2026-10-10 21:00:00+00', 'access'),
                (102, -2, '2026-10-10 21:00:00+00', 'access'),
                (104, 3, '2026-10-10 21:00:00+00', 'access'),
                (104, NULL, '2026-10-10 22:00:00+00', 'access');
            """, connection);
        await seed.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> StartRunAsync(NpgsqlConnection connection, DateOnly businessDate, DateTimeOffset observedAtUtc)
    {
        var runId = Guid.NewGuid();
        await using var command = new NpgsqlCommand("""
            INSERT INTO analytics_intel.inventory_observed_daily_capture_run
                (run_id, business_date, business_time_zone, observed_at_utc, source_system, run_state)
            VALUES (@run_id, @business_date, 'America/Los_Angeles', @observed_at_utc, 'products_dim_current', 'running');
            """, connection);
        command.Parameters.AddWithValue("run_id", runId);
        command.Parameters.AddWithValue("business_date", businessDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("observed_at_utc", observedAtUtc);
        await command.ExecuteNonQueryAsync();
        return runId;
    }

    private static async Task<int> CaptureAsync(NpgsqlConnection connection, Guid runId)
    {
        await using var command = new NpgsqlCommand("SELECT analytics_intel.capture_observed_inventory_daily(@run_id);", connection);
        command.Parameters.AddWithValue("run_id", runId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
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

        throw new DirectoryNotFoundException("Could not locate repository root from test output.");
    }
}
