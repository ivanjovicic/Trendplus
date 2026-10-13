using Infrastructure.Analytics;
using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Api.Tests;

public sealed class SupplierWindowedSql029LifecycleTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public SupplierWindowedSql029LifecycleTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void PreflightScript_IsReadOnlyAndDefinesMaintenanceVerdict()
    {
        var sql = ReadRepoFile("tools/supplier-windowed-mv-preflight.sql");

        Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maintenance_verdict", sql, StringComparison.Ordinal);
        Assert.Contains("mv_supplier_decision_score_cache_90d", sql, StringComparison.Ordinal);
        Assert.Contains("mv_supplier_decision_score_cache_180d", sql, StringComparison.Ordinal);
        Assert.Contains("__StartupSqlScriptHistory", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MaintenanceGuide_ReferencesPreflightToolAndPostcheck()
    {
        var guide = ReadRepoFile("docs/qa/SUPPLIER_WINDOWED_SQL029_MAINTENANCE.md");

        Assert.Contains("tools/supplier-windowed-mv-preflight.sql", guide, StringComparison.Ordinal);
        Assert.Contains("relispopulated", guide, StringComparison.Ordinal);
        Assert.Contains("STAB16", guide, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingBothWindowedCaches_PreflightContinue_Then029_IsIdempotent()
    {
        var prepared = await TryCreatePreparedDatabaseAsync();
        if (prepared is null)
        {
            return;
        }

        var connectionString = prepared;
        await EnsureSupplierHubWithoutWindowedAsync(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var before = await RunPreflightAsync(connection);
        Assert.Equal("CONTINUE", before.MaintenanceVerdict);
        Assert.False(before.Mv90Exists);
        Assert.False(before.Mv180Exists);

        await ApplySql029Async(connection);
        var afterFirst = await RunPreflightAsync(connection);
        Assert.Equal("READY", afterFirst.MaintenanceVerdict);

        await ApplySql029Async(connection);
        var afterSecond = await RunPreflightAsync(connection);
        Assert.Equal("READY", afterSecond.MaintenanceVerdict);

        await AssertWindowedCachesMatchAllTimeGrainAsync(connection);
    }

    [Fact]
    public async Task Only90dPresent_029Completes180d()
    {
        var prepared = await TryCreatePreparedDatabaseAsync();
        if (prepared is null)
        {
            return;
        }

        var connectionString = prepared;
        await EnsureSupplierHubWithoutWindowedAsync(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ApplySql029Async(connection);
        await ExecuteAsync(connection, "DROP MATERIALIZED VIEW IF EXISTS mv_supplier_decision_score_cache_180d;");

        var before = await RunPreflightAsync(connection);
        Assert.Equal("CONTINUE", before.MaintenanceVerdict);
        Assert.True(before.Mv90Exists);
        Assert.False(before.Mv180Exists);

        await ApplySql029Async(connection);
        var after = await RunPreflightAsync(connection);
        Assert.Equal("READY", after.MaintenanceVerdict);
    }

    [Fact]
    public async Task Legacy90dDefinition_029ReplacesRequiredColumnContract()
    {
        var prepared = await TryCreatePreparedDatabaseAsync();
        if (prepared is null)
        {
            return;
        }

        var connectionString = prepared;
        await EnsureSupplierHubWithoutWindowedAsync(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE MATERIALIZED VIEW mv_supplier_decision_score_cache_90d AS
            SELECT 1::integer AS legacy_sold_units_in_period;
            """);

        var before = await RunPreflightAsync(connection);
        Assert.Equal("CONTINUE", before.MaintenanceVerdict);
        Assert.True(before.Mv90MissingColumns > 0);

        await ApplySql029Async(connection);
        var capability = await PostgresMaterializedViewCapabilityReader.InspectAsync(
            connection,
            "mv_supplier_decision_score_cache_90d",
            SupplierDecisionMaterializedViewContract.DecisionScoreRequiredColumns);
        Assert.True(capability.IsReady);
    }

    [Fact]
    public async Task RolledBackTransaction_DoesNotBlockSubsequent029Application()
    {
        var prepared = await TryCreatePreparedDatabaseAsync();
        if (prepared is null)
        {
            return;
        }

        var connectionString = prepared;
        await EnsureSupplierHubWithoutWindowedAsync(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var command = new NpgsqlCommand(
                """
                CREATE TABLE supplier_sql029_transaction_probe (id integer NOT NULL);
                CREATE UNIQUE INDEX CONCURRENTLY ux_supplier_sql029_transaction_probe
                    ON supplier_sql029_transaction_probe (id);
                """,
                connection,
                transaction);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("25001", exception.SqlState);
            await transaction.RollbackAsync();
        }

        await ApplySql029Async(connection);
        var after = await RunPreflightAsync(connection);
        Assert.Equal("READY", after.MaintenanceVerdict);
    }

    private async Task<string?> TryCreatePreparedDatabaseAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return null;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_sql029_{Guid.NewGuid():N}");
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    private static async Task EnsureSupplierHubWithoutWindowedAsync(string connectionString)
    {
        await SupplierDecisionSchemaReadinessIntegrationTests.SeedSupplierSchemaPrerequisitesAsync(connectionString);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:AnalyticsConnection"] = connectionString,
                ["DatabaseInitialization:BuildSupplierDecisionHubCachesOnStartup"] = "true",
                ["DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup"] = "false",
            })
            .Build();

        using var services = new ServiceCollection()
            .AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(connectionString))
            .BuildServiceProvider();

        await DatabaseInitializer.EnsureAnalyticsSupplierDecisionSchemaAsync(
            services,
            configuration,
            NullLogger.Instance);
    }

    private static async Task ApplySql029Async(NpgsqlConnection connection)
    {
        foreach (var batch in ReadRepoFile("Database/Migrations/029_AddSupplierDecisionWindowedViews.sql")
                     .Split("-- SQL_BATCH_BREAK", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await ExecuteAsync(connection, batch);
        }
    }

    private static async Task<PreflightResult> RunPreflightAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(ReadRepoFile("tools/supplier-windowed-mv-preflight.sql"), connection)
        {
            CommandTimeout = 60,
        };
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return new PreflightResult(
            reader.GetString(reader.GetOrdinal("maintenance_verdict")),
            reader.GetString(reader.GetOrdinal("verdict_reason_code")),
            reader.GetBoolean(reader.GetOrdinal("mv_90_exists")),
            reader.GetBoolean(reader.GetOrdinal("mv_180_exists")),
            reader.GetInt32(reader.GetOrdinal("mv_90_missing_columns")),
            reader.GetInt32(reader.GetOrdinal("mv_180_missing_columns")));
    }

    private static async Task AssertWindowedCachesMatchAllTimeGrainAsync(NpgsqlConnection connection)
    {
        foreach (var relation in new[]
        {
            "mv_supplier_decision_score_cache_90d",
            "mv_supplier_decision_score_cache_180d",
        })
        {
            var capability = await PostgresMaterializedViewCapabilityReader.InspectAsync(
                connection,
                relation,
                SupplierDecisionMaterializedViewContract.DecisionScoreRequiredColumns);
            Assert.True(capability.IsReady, $"{relation} state={capability.ErrorCode}");
        }

        await using var grainCommand = new NpgsqlCommand(
            """
            SELECT
                (SELECT COUNT(*) FROM mv_supplier_decision_score_cache) AS all_time_rows,
                (SELECT COUNT(*) FROM mv_supplier_decision_score_cache_90d) AS windowed_90_rows,
                (SELECT COUNT(*) FROM mv_supplier_decision_score_cache_180d) AS windowed_180_rows;
            """,
            connection);
        await using var reader = await grainCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(reader.GetInt64(0), reader.GetInt64(1));
        Assert.Equal(reader.GetInt64(0), reader.GetInt64(2));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection)
        {
            CommandTimeout = 180,
        };
        await command.ExecuteNonQueryAsync();
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private sealed record PreflightResult(
        string MaintenanceVerdict,
        string VerdictReasonCode,
        bool Mv90Exists,
        bool Mv180Exists,
        int Mv90MissingColumns,
        int Mv180MissingColumns);
}
