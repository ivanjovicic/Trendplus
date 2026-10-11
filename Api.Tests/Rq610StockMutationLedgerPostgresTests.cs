using System.Collections.Concurrent;
using System.Globalization;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "RQ610")]
public sealed class Rq610StockMutationLedgerPostgresTests : IClassFixture<Rq610StockMutationPostgresFixture>
{
    private readonly Rq610StockMutationPostgresFixture _fixture;

    public Rq610StockMutationLedgerPostgresTests(Rq610StockMutationPostgresFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "RQ610 ledger captures writer origin, quantity boundaries, store changes and DST without SourceBatchId generation")]
    public async Task Ledger_CapturesMutationLineageWithoutDuplicateStoreAttribution()
    {
        var connectionString = await _fixture.CreateDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await CreateExperimentSchemaAsync(connection);

        var accessTransaction = await ExecuteMutationAsync(
            connection,
            "access_import",
            new DateTimeOffset(2026, 3, 29, 0, 59, 59, TimeSpan.Zero),
            "UPDATE \"Artikli\" SET \"Kolicina\" = @quantity WHERE \"Id\" = @id",
            ("quantity", 0),
            ("id", 1));

        // A retry that replays the already committed value is not a new stock mutation.
        await ExecuteMutationAsync(
            connection,
            "access_import",
            new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero),
            "UPDATE \"Artikli\" SET \"Kolicina\" = @quantity WHERE \"Id\" = @id",
            ("quantity", 0),
            ("id", 1));

        var manualTransaction = await ExecuteMutationAsync(
            connection,
            "manual",
            new DateTimeOffset(2026, 10, 25, 0, 59, 59, TimeSpan.Zero),
            "UPDATE \"Artikli\" SET \"Kolicina\" = @quantity WHERE \"Id\" = @id",
            ("quantity", -2),
            ("id", 1));

        // No writer context is deliberately supplied: the trigger must preserve UNKNOWN.
        await ExecuteMutationAsync(
            connection,
            null,
            new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero),
            "UPDATE \"Artikli\" SET \"Kolicina\" = NULL WHERE \"Id\" = @id",
            ("id", 2));

        var storeChangeTransaction = await ExecuteMutationAsync(
            connection,
            "manual",
            new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero),
            "UPDATE \"Artikli\" SET \"IDObjekat\" = @store WHERE \"Id\" = @id",
            ("store", 303),
            ("id", 1));

        var transferTransaction = await ExecuteMutationBatchAsync(
            connection,
            "transfer",
            new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.Zero),
            """
            UPDATE "Artikli" SET "Kolicina" = "Kolicina" - 1 WHERE "Id" = 1;
            UPDATE "Artikli" SET "Kolicina" = COALESCE("Kolicina", 0) + 1 WHERE "Id" = 2;
            """);

        var rows = await ReadLedgerAsync(connection);

        Assert.Equal(6, rows.Count); // one replay was intentionally suppressed
        Assert.Equal(rows.Count, rows.Select(row => row.Generation).Distinct().Count());
        Assert.All(rows, row => Assert.True(row.Generation > 0));
        Assert.Equal(5, rows.Count(row => row.OriginStatus == "known"));
        Assert.Equal(1, rows.Count(row => row.OriginStatus == "unknown"));
        Assert.Contains(rows, row => row.ArticleId == 1 && row.PreviousQuantity == 5 && row.NewQuantity == 0
            && row.Origin == "access_import" && row.TransactionId == accessTransaction);
        Assert.Contains(rows, row => row.ArticleId == 1 && row.PreviousQuantity == 0 && row.NewQuantity == -2
            && row.Origin == "manual" && row.TransactionId == manualTransaction);
        Assert.Contains(rows, row => row.ArticleId == 2 && row.PreviousQuantity == 7 && row.NewQuantity is null
            && row.Origin == "unknown");
        Assert.Contains(rows, row => row.ArticleId == 1 && row.StoreBefore == 101 && row.StoreAfter == 303
            && row.PreviousQuantity == -2 && row.NewQuantity == -2
            && row.TransactionId == storeChangeTransaction);

        var transferRows = rows.Where(row => row.TransactionId == transferTransaction).ToArray();
        Assert.Equal(2, transferRows.Length);
        Assert.Equal(new[] { 1, 2 }, transferRows.Select(row => row.ArticleId).OrderBy(id => id));
        Assert.Equal(new int?[] { -3, 1 }, transferRows.Select(row => row.NewQuantity).OrderBy(quantity => quantity));
        Assert.All(transferRows, row => Assert.Equal("transfer", row.Origin));

        await using (var columns = new NpgsqlCommand("""
            SELECT count(*)
              FROM information_schema.columns
             WHERE table_schema = 'public'
               AND table_name = 'stock_mutation_ledger'
               AND column_name = 'source_batch_id';
            """, connection))
        {
            Assert.Equal(0L, (long)(await columns.ExecuteScalarAsync())!);
        }

        await using (var sourceIsolation = new NpgsqlCommand("""
            SELECT "Kolicina" FROM "Artikli" WHERE "Id" = 1;
            """, connection))
        {
            Assert.Equal(-3, (int)(await sourceIsolation.ExecuteScalarAsync())!);
        }

        await using (var businessDays = new NpgsqlCommand("""
            SELECT count(*) FILTER (WHERE business_date = DATE '2026-03-29'),
                   count(*) FILTER (WHERE business_date = DATE '2026-10-25'),
                   count(DISTINCT (observed_at_utc AT TIME ZONE business_time_zone)::date)
              FROM stock_mutation_ledger;
            """, connection))
        await using (var reader = await businessDays.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(5L, reader.GetInt64(1));
            Assert.Equal(2L, reader.GetInt64(2));
        }

        Assert.Equal(4, rows.Count(row => row.ArticleId == 1));
        Assert.DoesNotContain(rows, row => row.ArticleId == 2 && row.Origin == "manual");
    }

    [Fact(DisplayName = "RQ610 ledger rolls back with Artikli and survives concurrent writers and same-value retry")]
    public async Task Ledger_RollsBackAtomicallyAndCapturesConcurrentTransactions()
    {
        var connectionString = await _fixture.CreateDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await CreateExperimentSchemaAsync(connection);

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetWriterContextAsync(
                connection,
                transaction,
                "manual",
                new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero));
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "UPDATE \"Artikli\" SET \"Kolicina\" = 55 WHERE \"Id\" = 1");

            Assert.Equal(1L, await CountLedgerAsync(connection, transaction));
            await transaction.RollbackAsync();
        }

        Assert.Equal(0L, await CountLedgerAsync(connection));
        Assert.Equal(5, await ReadQuantityAsync(connection, 1));

        await using var first = new NpgsqlConnection(connectionString);
        await using var second = new NpgsqlConnection(connectionString);
        await first.OpenAsync();
        await second.OpenAsync();
        await using var firstTransaction = await first.BeginTransactionAsync();
        await using var secondTransaction = await second.BeginTransactionAsync();
        await SetWriterContextAsync(
            first,
            firstTransaction,
            "manual",
            new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));
        await SetWriterContextAsync(
            second,
            secondTransaction,
            "pos_sql",
            new DateTimeOffset(2026, 11, 2, 10, 0, 1, TimeSpan.Zero));
        await ExecuteNonQueryAsync(
            first,
            firstTransaction,
            "UPDATE \"Artikli\" SET \"Kolicina\" = 6 WHERE \"Id\" = 1");
        await ExecuteNonQueryAsync(
            second,
            secondTransaction,
            "UPDATE \"Artikli\" SET \"Kolicina\" = 8 WHERE \"Id\" = 2");

        var firstTransactionId = await ReadTransactionIdAsync(first, firstTransaction);
        var secondTransactionId = await ReadTransactionIdAsync(second, secondTransaction);
        await Task.WhenAll(firstTransaction.CommitAsync(), secondTransaction.CommitAsync());

        Assert.NotEqual(firstTransactionId, secondTransactionId);
        Assert.Equal(2L, await CountLedgerAsync(connection));
        Assert.Equal(6, await ReadQuantityAsync(connection, 1));
        Assert.Equal(8, await ReadQuantityAsync(connection, 2));

        // Replaying both final values must not create duplicate mutation events.
        await ExecuteMutationAsync(
            connection,
            "pos_sql",
            new DateTimeOffset(2026, 11, 2, 10, 1, 0, TimeSpan.Zero),
            "UPDATE \"Artikli\" SET \"Kolicina\" = @quantity WHERE \"Id\" = @id",
            ("quantity", 6),
            ("id", 1));
        await ExecuteMutationAsync(
            connection,
            "pos_sql",
            new DateTimeOffset(2026, 11, 2, 10, 1, 1, TimeSpan.Zero),
            "UPDATE \"Artikli\" SET \"Kolicina\" = @quantity WHERE \"Id\" = @id",
            ("quantity", 8),
            ("id", 2));
        Assert.Equal(2L, await CountLedgerAsync(connection));

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var committedLedgerRow = new NpgsqlCommand("""
                SELECT ledger_id
                  FROM stock_mutation_ledger
                 WHERE transaction_id = @transaction_id
                   AND article_id = @article_id;
                """, connection);
            committedLedgerRow.Parameters.AddWithValue("transaction_id", firstTransactionId);
            committedLedgerRow.Parameters.AddWithValue("article_id", 1);
            var committedLedgerId = Assert.IsType<long>(await committedLedgerRow.ExecuteScalarAsync());

            await using var command = new NpgsqlCommand(
                "UPDATE stock_mutation_ledger SET origin = 'tampered' WHERE ledger_id = @ledger_id;",
                connection);
            command.Parameters.AddWithValue("ledger_id", committedLedgerId);
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("P0001", exception.SqlState);
        Assert.Equal(2L, await CountLedgerAsync(connection));
    }

    private static async Task CreateExperimentSchemaAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            CREATE TABLE "Artikli" (
                "Id" integer PRIMARY KEY,
                "PLU" text NOT NULL,
                "IDObjekat" integer NULL,
                "Kolicina" integer NULL
            );
            INSERT INTO "Artikli" ("Id", "PLU", "IDObjekat", "Kolicina") VALUES
                (1, 'PLU-SAME', 101, 5),
                (2, 'PLU-SAME', 202, 7),
                (3, 'PLU-OTHER', 101, NULL);

            CREATE SEQUENCE stock_mutation_generation_seq AS bigint START WITH 1;
            CREATE TABLE stock_mutation_ledger (
                ledger_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                article_id integer NOT NULL,
                plu text NOT NULL,
                store_id_before integer NULL,
                store_id_after integer NULL,
                previous_quantity integer NULL,
                new_quantity integer NULL,
                observed_at_utc timestamptz NOT NULL,
                business_date date NOT NULL,
                business_time_zone text NOT NULL,
                generation bigint NOT NULL UNIQUE,
                transaction_id bigint NOT NULL,
                origin text NOT NULL,
                origin_status text NOT NULL,
                CHECK (origin_status IN ('known', 'unknown'))
            );

            CREATE OR REPLACE FUNCTION capture_stock_mutation() RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
                mutation_time timestamptz;
                mutation_origin text;
                mutation_origin_status text;
            BEGIN
                IF TG_OP = 'UPDATE'
                   AND NEW."Kolicina" IS NOT DISTINCT FROM OLD."Kolicina"
                   AND NEW."IDObjekat" IS NOT DISTINCT FROM OLD."IDObjekat" THEN
                    RETURN NEW;
                END IF;

                mutation_time := COALESCE(
                    NULLIF(current_setting('trendplus.stock_mutation_observed_at_utc', true), ''),
                    clock_timestamp()::text)::timestamptz;
                mutation_origin := lower(COALESCE(
                    NULLIF(current_setting('trendplus.stock_mutation_origin', true), ''),
                    'unknown'));
                IF mutation_origin IN ('access_import', 'manual', 'transfer', 'pos_sql') THEN
                    mutation_origin_status := 'known';
                ELSE
                    mutation_origin := 'unknown';
                    mutation_origin_status := 'unknown';
                END IF;

                INSERT INTO stock_mutation_ledger (
                    article_id, plu, store_id_before, store_id_after,
                    previous_quantity, new_quantity, observed_at_utc,
                    business_date, business_time_zone, generation,
                    transaction_id, origin, origin_status)
                VALUES (
                    NEW."Id", NEW."PLU",
                    CASE WHEN TG_OP = 'UPDATE' THEN OLD."IDObjekat" ELSE NULL END,
                    NEW."IDObjekat",
                    CASE WHEN TG_OP = 'UPDATE' THEN OLD."Kolicina" ELSE NULL END,
                    NEW."Kolicina", mutation_time,
                    (mutation_time AT TIME ZONE 'Europe/Belgrade')::date,
                    'Europe/Belgrade', nextval('stock_mutation_generation_seq'),
                    txid_current(), mutation_origin, mutation_origin_status);
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER capture_stock_mutation_after_change
                AFTER INSERT OR UPDATE ON "Artikli"
                FOR EACH ROW EXECUTE FUNCTION capture_stock_mutation();

            CREATE OR REPLACE FUNCTION reject_ledger_mutation() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'stock_mutation_ledger is append-only';
            END;
            $$;

            CREATE TRIGGER reject_stock_mutation_ledger_change
                BEFORE UPDATE OR DELETE ON stock_mutation_ledger
                FOR EACH ROW EXECUTE FUNCTION reject_ledger_mutation();
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ExecuteMutationAsync(
        NpgsqlConnection connection,
        string? origin,
        DateTimeOffset observedAtUtc,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await SetWriterContextAsync(connection, transaction, origin, observedAtUtc);
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }

            await command.ExecuteNonQueryAsync();
        }

        var transactionId = await ReadTransactionIdAsync(connection, transaction);
        await transaction.CommitAsync();
        return transactionId;
    }

    private static async Task<long> ExecuteMutationBatchAsync(
        NpgsqlConnection connection,
        string? origin,
        DateTimeOffset observedAtUtc,
        string sql)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await SetWriterContextAsync(connection, transaction, origin, observedAtUtc);
        await ExecuteNonQueryAsync(connection, transaction, sql);
        var transactionId = await ReadTransactionIdAsync(connection, transaction);
        await transaction.CommitAsync();
        return transactionId;
    }

    private static async Task SetWriterContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string? origin,
        DateTimeOffset observedAtUtc)
    {
        await using var command = new NpgsqlCommand("""
            SELECT set_config('trendplus.stock_mutation_origin', @origin, true),
                   set_config('trendplus.stock_mutation_observed_at_utc', @observed_at_utc, true);
            """, connection, transaction);
        command.Parameters.AddWithValue("origin", origin ?? string.Empty);
        command.Parameters.AddWithValue(
            "observed_at_utc",
            observedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ReadTransactionIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand("SELECT txid_current();", connection, transaction);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> CountLedgerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand("SELECT count(*) FROM stock_mutation_ledger;", connection, transaction);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> ReadQuantityAsync(NpgsqlConnection connection, int articleId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT \"Kolicina\" FROM \"Artikli\" WHERE \"Id\" = @id;",
            connection);
        command.Parameters.AddWithValue("id", articleId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<IReadOnlyList<LedgerRow>> ReadLedgerAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT generation, article_id, store_id_before, store_id_after,
                   previous_quantity, new_quantity, observed_at_utc,
                   business_date, transaction_id, origin, origin_status
              FROM stock_mutation_ledger
             ORDER BY generation;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<LedgerRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new LedgerRow(
                reader.GetInt64(0),
                reader.GetInt32(1),
                NullableInt(reader, 2),
                NullableInt(reader, 3),
                NullableInt(reader, 4),
                NullableInt(reader, 5),
                reader.GetDateTime(6),
                DateOnly.FromDateTime(reader.GetDateTime(7)),
                reader.GetInt64(8),
                reader.GetString(9),
                reader.GetString(10)));
        }

        return rows;
    }

    private static int? NullableInt(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private sealed record LedgerRow(
        long Generation,
        int ArticleId,
        int? StoreBefore,
        int? StoreAfter,
        int? PreviousQuantity,
        int? NewQuantity,
        DateTime ObservedAtUtc,
        DateOnly BusinessDate,
        long TransactionId,
        string Origin,
        string OriginStatus);
}

public sealed class Rq610StockMutationPostgresFixture : IAsyncLifetime
{
    // The ledger fixture may create and drop databases. Never silently fall back
    // to the developer's local PostgreSQL cluster when the test target is missing.
    private readonly ConcurrentBag<string> _databases = new();
    private string _adminConnectionString = string.Empty;
    private bool _isAvailable;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("RQ610_LEDGER_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "RQ610 ledger PostgreSQL tests require an explicitly provisioned disposable test server via RQ610_LEDGER_TEST_POSTGRES. " +
                "No developer/business database will be used as a default.");
        }

        _adminConnectionString = configured;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = "postgres",
                Pooling = false
            };
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            _isAvailable = true;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "RQ610 stock-mutation PostgreSQL certification requires a reachable PostgreSQL server; unavailable infrastructure must fail, not skip.",
                exception);
        }
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var databaseName = $"tp_rq610_ledger_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", admin);
            await create.ExecuteNonQueryAsync();
        }

        _databases.Add(databaseName);
        var databaseBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };
        return databaseBuilder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (!_isAvailable)
        {
            return;
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        foreach (var databaseName in _databases)
        {
            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);",
                admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
