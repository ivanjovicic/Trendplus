using System.Diagnostics;
using System.Globalization;
using Api.Services.DataSources;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;
using Xunit;

namespace Api.Tests;

public sealed class SqlServerSourceDataSessionIntegrationTests : IClassFixture<SqlServerEngineFixture>
{
    private readonly SqlServerEngineFixture _fixture;

    public SqlServerSourceDataSessionIntegrationTests(SqlServerEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConnectionTest_Discovery_Types_Cursors_Cancellation_AndTimeout()
    {
        if (!_fixture.IsAvailable)
            return;

        await using var session = new SqlServerSourceDataSession(_fixture.ConnectionString, NullLogger.Instance);

        await session.TestConnectionAsync();

        var tables = await session.GetTablesAsync();
        Assert.Contains("dbo.Order", tables, StringComparer.OrdinalIgnoreCase);

        var columns = await session.GetColumnsAsync("dbo.Order");
        Assert.Equal(["Id", "Select", "User", "Price", "UpdatedAt"], columns);

        var count = await session.TryGetRowCountAsync("dbo.Order");
        Assert.True(count.IsExact);
        Assert.Equal(3, count.Count);

        var fullRows = await ReadAllAsync(session, "dbo.Order");
        Assert.Equal([1, 2, 3], fullRows.Select(IdOf).ToArray());

        var first = fullRows[0];
        Assert.True(first.TryGetValue("Select", out var selectNull));
        Assert.Null(selectNull);
        Assert.True(first.TryGetValue("Price", out var priceNull));
        Assert.Null(priceNull);

        var unicode = fullRows[2];
        Assert.True(unicode.TryGetValue("Select", out var cizma));
        Assert.Equal("čizma", cizma);
        Assert.True(unicode.TryGetValue("User", out var cyrillic));
        Assert.Equal("чизма", cyrillic);
        Assert.True(unicode.TryGetValue("Price", out var price));
        Assert.Equal(12.3456m, Convert.ToDecimal(price, CultureInfo.InvariantCulture));

        var afterId = await ReadAllAsync(
            session,
            "dbo.Order",
            new SourceReadQuery { CursorMode = "id", CursorId = 1, IdAliases = ["id"] });
        Assert.Equal([2, 3], afterId.Select(IdOf).ToArray());

        var cursorUtc = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Unspecified);
        var incremental = await ReadAllAsync(
            session,
            "dbo.Order",
            new SourceReadQuery
            {
                CursorMode = "timestamp_then_id",
                CursorTimestampUtc = cursorUtc,
                CursorTieBreakerId = 2,
                TimestampAliases = ["updatedat"],
                IdAliases = ["id"]
            });
        Assert.Equal([3], incremental.Select(IdOf).ToArray());

        await AssertCancellationAsync();
        await AssertCommandTimeoutAsync();
    }

    private async Task AssertCancellationAsync()
    {
        await using var blocker = new SqlConnection(_fixture.WriterConnectionString);
        await blocker.OpenAsync();
        await using var tran = (SqlTransaction)await blocker.BeginTransactionAsync();
        await using var lockCommand = new SqlCommand(
            "SELECT * FROM dbo.LockProbe WITH (TABLOCKX, HOLDLOCK)",
            blocker,
            tran);
        await lockCommand.ExecuteNonQueryAsync();

        using var cts = new CancellationTokenSource();
        await using var session = new SqlServerSourceDataSession(
            _fixture.ConnectionString,
            NullLogger.Instance,
            commandTimeoutSeconds: 30);

        var read = ReadAllAsync(session, "dbo.LockProbe", query: null, cts.Token);
        await Task.Delay(400);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);

        await tran.RollbackAsync();
    }

    private async Task AssertCommandTimeoutAsync()
    {
        await using var blocker = new SqlConnection(_fixture.WriterConnectionString);
        await blocker.OpenAsync();
        await using var tran = (SqlTransaction)await blocker.BeginTransactionAsync();
        await using var lockCommand = new SqlCommand(
            "SELECT * FROM dbo.LockProbe WITH (TABLOCKX, HOLDLOCK)",
            blocker,
            tran);
        await lockCommand.ExecuteNonQueryAsync();

        await using var session = new SqlServerSourceDataSession(
            _fixture.ConnectionString,
            NullLogger.Instance,
            commandTimeoutSeconds: 2);

        var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(async () =>
        {
            await foreach (var _ in session.ReadRowsAsync("dbo.LockProbe"))
            {
            }
        });
        Assert.Equal(-2, error.Number);

        await tran.RollbackAsync();
    }

    private static async Task<List<SourceDataRow>> ReadAllAsync(
        ISourceDataSession session,
        string table,
        SourceReadQuery? query = null,
        CancellationToken ct = default)
    {
        var rows = new List<SourceDataRow>();
        await foreach (var row in session.ReadRowsAsync(table, query, ct))
            rows.Add(row);
        return rows;
    }

    private static int IdOf(SourceDataRow row)
    {
        Assert.True(row.TryGetValue("Id", out var value));
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}

public sealed class SqlServerEngineFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private string? _ownedDatabaseName;
    private string _masterConnectionString = string.Empty;

    public bool IsAvailable { get; private set; }

    public string EngineKind { get; private set; } = "none";

    public string ConnectionString { get; private set; } = string.Empty;

    public string WriterConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (await TryStartTestcontainersAsync())
        {
            await SeedAsync();
            IsAvailable = true;
            return;
        }

        var fromEnv = Environment.GetEnvironmentVariable("SQLSERVER_TEST_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(fromEnv) && await TryAttachAsync(fromEnv, "env"))
        {
            await SeedAsync();
            IsAvailable = true;
            return;
        }

        if (await TryStartLocalDbAsync())
        {
            await SeedAsync();
            IsAvailable = true;
            return;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "SQL Server integration tests are mandatory in CI, but Testcontainers, LocalDB, and SQLSERVER_TEST_CONNECTION_STRING were unavailable.");
        }
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_ownedDatabaseName) && !string.IsNullOrWhiteSpace(_masterConnectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_masterConnectionString);
                await connection.OpenAsync();
                await using var command = new SqlCommand(
                    $"""
                    IF DB_ID(N'{_ownedDatabaseName}') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{_ownedDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{_ownedDatabaseName}];
                    END
                    """,
                    connection);
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Fixture cleanup must not hide test results.
            }
        }

        if (_container is not null)
            await _container.DisposeAsync();
    }

    private async Task<bool> TryStartTestcontainersAsync()
    {
        if (!await IsDockerAvailableAsync())
            return false;

        try
        {
            _container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();
            await _container.StartAsync();
            return await TryAttachAsync(_container.GetConnectionString(), "testcontainers");
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryStartLocalDbAsync()
    {
        try
        {
            var start = Process.Start(new ProcessStartInfo
            {
                FileName = "sqllocaldb",
                Arguments = "start MSSQLLocalDB",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            start?.WaitForExit(15_000);

            var localDb =
                @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;";
            return await TryAttachAsync(localDb, "localdb");
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryAttachAsync(string masterConnectionString, string engineKind)
    {
        try
        {
            var databaseName = "tp_qdb03_" + Guid.NewGuid().ToString("N");
            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync();
            await using (var create = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection))
            {
                await create.ExecuteNonQueryAsync();
            }

            var writer = new SqlConnectionStringBuilder(masterConnectionString)
            {
                InitialCatalog = databaseName,
                ApplicationIntent = ApplicationIntent.ReadWrite
            };
            var reader = new SqlConnectionStringBuilder(writer.ConnectionString)
            {
                ApplicationIntent = ApplicationIntent.ReadOnly
            };

            _masterConnectionString = masterConnectionString;
            _ownedDatabaseName = databaseName;
            WriterConnectionString = writer.ConnectionString;
            ConnectionString = reader.ConnectionString;
            EngineKind = engineKind;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SeedAsync()
    {
        await using var connection = new SqlConnection(WriterConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            CREATE TABLE dbo.[Order] (
                [Id] INT NOT NULL PRIMARY KEY,
                [Select] NVARCHAR(100) NULL,
                [User] NVARCHAR(100) NULL,
                [Price] DECIMAL(18,4) NULL,
                [UpdatedAt] DATETIME2 NOT NULL
            );

            INSERT INTO dbo.[Order] ([Id], [Select], [User], [Price], [UpdatedAt]) VALUES
                (3, N'čizma', N'чизма', 12.3456, '2026-08-01T10:00:00'),
                (1, NULL, N'boot', NULL, '2026-08-01T08:00:00'),
                (2, N'sneaker', N'патика', 99.5000, '2026-08-01T09:00:00');

            CREATE TABLE dbo.LockProbe (
                [Id] INT NOT NULL PRIMARY KEY,
                [Payload] NVARCHAR(20) NOT NULL
            );
            INSERT INTO dbo.LockProbe ([Id], [Payload]) VALUES (1, N'lock');
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsDockerAvailableAsync()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
                return false;

            var completed = await Task.Run(() => process.WaitForExit(3000));
            return completed && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
