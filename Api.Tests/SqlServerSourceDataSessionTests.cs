using System.Data;
using Api.Services.DataSources;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Api.Tests;

[CollectionDefinition(CollectionName)]
public sealed class SqlServerSourceDataSessionCollection : ICollectionFixture<SqlServerSourceDataSessionFixture>
{
    public const string CollectionName = "sqlserver-source-session";
}

[Collection(SqlServerSourceDataSessionCollection.CollectionName)]
public sealed class SqlServerSourceDataSessionTests
{
    private readonly SqlServerSourceDataSessionFixture _fixture;

    public SqlServerSourceDataSessionTests(SqlServerSourceDataSessionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DiscoveryAndStreaming_QuoteReservedIdentifiersAndPreserveValues()
    {
        const string tableName = "[dbo].[Order Details]";
        await CreateDiscoveryTableAsync(tableName);

        await using var session = new SqlServerSourceDataSession(_fixture.ConnectionString);

        Assert.Equal("sqlserver", session.Provider);
        Assert.Equal("readonly", session.Mode);
        Assert.True(session.Capabilities.SchemaDiscovery);
        Assert.True(session.Capabilities.PredicatePushdown);
        Assert.Contains("sqlserver://", session.SourceIdentity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", session.SourceIdentity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User ID=", session.SourceIdentity, StringComparison.OrdinalIgnoreCase);

        await session.TestConnectionAsync();

        var tables = await session.GetTablesAsync();
        Assert.Contains("dbo.Order Details", tables);

        var columns = await session.GetColumnsAsync("dbo.Order Details");
        Assert.Equal(["Order Id", "Naziv", "Iznos", "RecordedAt", "Napomena"], columns);

        var rowCount = await session.TryGetRowCountAsync("dbo.Order Details");
        Assert.True(rowCount.IsExact);
        Assert.Equal(3, rowCount.Count);

        var ids = new List<int>();
        var names = new List<string>();
        var amounts = new List<decimal?>();
        var timestamps = new List<DateTime>();
        var notes = new List<string?>();

        await foreach (var row in session.ReadRowsAsync("dbo.Order Details"))
        {
            Assert.True(row.TryGetValue("Order Id", out var idValue));
            Assert.True(row.TryGetValue("Naziv", out var nameValue));
            Assert.True(row.TryGetValue("Iznos", out var amountValue));
            Assert.True(row.TryGetValue("RecordedAt", out var timestampValue));
            Assert.True(row.TryGetValue("Napomena", out var noteValue));

            ids.Add((int)idValue!);
            names.Add((string)nameValue!);
            amounts.Add(amountValue is null ? null : (decimal)amountValue);
            timestamps.Add((DateTime)timestampValue!);
            notes.Add(noteValue as string);
        }

        Assert.Equal([1, 2, 3], ids);
        Assert.Equal(["Patika", "Čarapa", "Čizma"], names);
        Assert.Equal([null, 12.50m, 19.95m], amounts);
        Assert.Equal([
            new DateTime(2026, 8, 19, 9, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 8, 19, 9, 30, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Unspecified)],
            timestamps);
        Assert.Equal(["Napomena prva", "Napomena druga", "Napomena treća"], notes);
    }

    [Fact]
    public async Task IncrementalCursor_MatchesIdTimestampAndCompositeFallbackRules()
    {
        const string tableName = "[dbo].[Cursor Proof]";
        await CreateCursorTableAsync(tableName);

        await using var session = new SqlServerSourceDataSession(_fixture.ConnectionString);

        var fullScanIds = await CollectIdsAsync(session.ReadRowsAsync("dbo.Cursor Proof"));
        Assert.Equal([1, 2, 3], fullScanIds);

        var idCursorIds = await CollectIdsAsync(
            session.ReadRowsAsync(
                "dbo.Cursor Proof",
                new SourceReadQuery
                {
                    CursorMode = "id",
                    CursorId = 1,
                    IdAliases = ["Id"]
                }));
        Assert.Equal([2, 3], idCursorIds);

        var timestampCursorIds = await CollectIdsAsync(
            session.ReadRowsAsync(
                "dbo.Cursor Proof",
                new SourceReadQuery
                {
                    CursorMode = "timestamp",
                    CursorTimestampUtc = new DateTime(2026, 8, 19, 9, 0, 0, DateTimeKind.Utc),
                    TimestampAliases = ["UpdatedAt"]
                }));
        Assert.Equal([3], timestampCursorIds);

        var compositeCursorIds = await CollectIdsAsync(
            session.ReadRowsAsync(
                "dbo.Cursor Proof",
                new SourceReadQuery
                {
                    CursorMode = "timestamp_then_id",
                    CursorTimestampUtc = new DateTime(2026, 8, 19, 9, 0, 0, DateTimeKind.Utc),
                    CursorTieBreakerId = 1,
                    TimestampAliases = ["UpdatedAt"],
                    IdAliases = ["Id"]
                }));
        Assert.Equal([2, 3], compositeCursorIds);

        var fallbackIds = await CollectIdsAsync(
            session.ReadRowsAsync(
                "dbo.Cursor Proof",
                new SourceReadQuery
                {
                    CursorMode = "timestamp_then_id",
                    CursorTimestampUtc = new DateTime(2026, 8, 19, 9, 0, 0, DateTimeKind.Utc),
                    CursorTieBreakerId = 1,
                    TimestampAliases = ["MissingTimestamp"],
                    IdAliases = ["MissingId"]
                }));
        Assert.Equal([1, 2, 3], fallbackIds);
    }

    [Fact]
    public async Task ReadRows_RespectsCancellationAndCommandTimeoutWhileLocked()
    {
        const string tableName = "[dbo].[Locked Items]";
        await CreateBlockingTableAsync(tableName);

        await using var lockHandle = await AcquireExclusiveLockAsync(tableName);

        await using var cancellableSession = new SqlServerSourceDataSession(_fixture.ConnectionString, commandTimeoutSeconds: 30);
        using (var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(250)))
        {
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await foreach (var _ in cancellableSession.ReadRowsAsync("dbo.Locked Items", cancellationSource.Token))
                {
                    Assert.Fail("Cancelled read must not yield rows.");
                }
            });
        }

        await using var timeoutSession = new SqlServerSourceDataSession(_fixture.ConnectionString, commandTimeoutSeconds: 1);
        var timeout = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await foreach (var _ in timeoutSession.ReadRowsAsync("dbo.Locked Items"))
            {
                Assert.Fail("Timed-out read must not yield rows.");
            }
        });

        Assert.Contains("timeout", timeout.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateDiscoveryTableAsync(string tableName)
    {
        await ExecuteNonQueryAsync($$"""
            IF OBJECT_ID({{SqlLiteral(tableName)}}, 'U') IS NOT NULL
                DROP TABLE {{tableName}};

            CREATE TABLE {{tableName}} (
                [Order Id] INT NOT NULL PRIMARY KEY,
                [Naziv] NVARCHAR(100) NOT NULL,
                [Iznos] DECIMAL(10, 2) NULL,
                [RecordedAt] DATETIME2(0) NOT NULL,
                [Napomena] NVARCHAR(100) NULL
            );

            INSERT INTO {{tableName}} ([Order Id], [Naziv], [Iznos], [RecordedAt], [Napomena])
            VALUES
                (3, N'Čizma', 19.95, '2026-08-19T10:00:00', N'Napomena treća'),
                (1, N'Patika', NULL, '2026-08-19T09:00:00', N'Napomena prva'),
                (2, N'Čarapa', 12.50, '2026-08-19T09:30:00', N'Napomena druga');
            """);
    }

    private async Task CreateCursorTableAsync(string tableName)
    {
        await ExecuteNonQueryAsync($$"""
            IF OBJECT_ID({{SqlLiteral(tableName)}}, 'U') IS NOT NULL
                DROP TABLE {{tableName}};

            CREATE TABLE {{tableName}} (
                [Id] BIGINT NOT NULL PRIMARY KEY,
                [UpdatedAt] DATETIME2(0) NOT NULL,
                [Name] NVARCHAR(50) NOT NULL
            );

            INSERT INTO {{tableName}} ([Id], [UpdatedAt], [Name])
            VALUES
                (3, '2026-08-19T10:00:00', N'Three'),
                (1, '2026-08-19T09:00:00', N'One'),
                (2, '2026-08-19T09:00:00', N'Two');
            """);
    }

    private async Task CreateBlockingTableAsync(string tableName)
    {
        await ExecuteNonQueryAsync($$"""
            IF OBJECT_ID({{SqlLiteral(tableName)}}, 'U') IS NOT NULL
                DROP TABLE {{tableName}};

            CREATE TABLE {{tableName}} (
                [Id] INT NOT NULL PRIMARY KEY,
                [Name] NVARCHAR(50) NOT NULL
            );

            INSERT INTO {{tableName}} ([Id], [Name])
            VALUES (1, N'Locked row');
            """);
    }

    private async Task<SqlLockHandle> AcquireExclusiveLockAsync(string tableName)
    {
        var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"UPDATE {tableName} SET [Name] = [Name] WHERE [Id] = 1";
        await command.ExecuteNonQueryAsync();

        return new SqlLockHandle(connection, transaction);
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string SqlLiteral(string value)
        => $"N'{value.Replace("'", "''")}'";

    private static async Task<IReadOnlyList<int>> CollectIdsAsync(IAsyncEnumerable<SourceDataRow> rows)
    {
        var ids = new List<int>();
        await foreach (var row in rows)
        {
            Assert.True(row.TryGetValue("Id", out var value));
            ids.Add((int)(long)value!);
        }

        return ids;
    }

    private sealed class SqlLockHandle : IAsyncDisposable
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;
        private bool _disposed;

        public SqlLockHandle(SqlConnection connection, SqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;

            try
            {
                _transaction.Rollback();
            }
            catch
            {
                // Ignore rollback failures during cleanup.
            }

            _transaction.Dispose();
            _connection.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class SqlServerSourceDataSessionFixture
{
    public string ConnectionString { get; } =
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TRENDPLUS_SQLSERVER_TEST_CONNECTION_STRING"))
            ? new SqlConnectionStringBuilder
            {
                DataSource = "localhost",
                InitialCatalog = "tempdb",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                Encrypt = false
            }.ConnectionString
            : Environment.GetEnvironmentVariable("TRENDPLUS_SQLSERVER_TEST_CONNECTION_STRING")!;
}
