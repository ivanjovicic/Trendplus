using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Api.Tests;

public sealed class DatabaseMigrationBootstrapLifecycleSmokeTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DatabaseMigrationBootstrapLifecycleSmokeTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FreshBootstrapRepeatAndRestartRemainIdempotent()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_bootstrap_smoke_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:AnalyticsConnection"] = connectionString,
                ["DatabaseInitialization:FailFast"] = "true",
                ["DatabaseInitialization:RunFullSupplierDecisionHubOnStartup"] = "false",
            })
            .Build();

        await RunBootstrapAsync(connectionString, configuration);
        await RunBootstrapAsync(connectionString, configuration);

        // A new service provider models a process restart, rather than reusing
        // the scoped contexts from the first two bootstrap calls.
        await RunBootstrapAsync(connectionString, configuration);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Assert.True(await TableExistsAsync(connection, "__EFMigrationsHistory"));
        Assert.True(await TableExistsAsync(connection, "Artikli"));
        Assert.True(await TableExistsAsync(connection, "SalesFacts"));
        Assert.True(await TableExistsAsync(connection, "analytics_data_quality_history"));

        Assert.True(await MigrationExistsAsync(connection, "20251224163406_InitialPostgreSQL"));
        Assert.True(await MigrationExistsAsync(connection, "20251229125031_AddProductsDimTimestamp"));
        Assert.Equal(0, await DuplicateIndexCountAsync(connection));

        // pgvector is optional for this lifecycle smoke: no vector-backed
        // operation is exercised, so its absence is not a product failure.
        _ = await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_extension
                WHERE extname = 'vector'
            );
            """);
    }

    private static async Task RunBootstrapAsync(
        string connectionString,
        IConfiguration configuration)
    {
        using var services = new ServiceCollection()
            .AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(connectionString))
            .AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(connectionString))
            .BuildServiceProvider();

        await DatabaseInitializer.InitializeDatabasesAsync(
            services,
            configuration,
            NullLogger.Instance);
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string tableName)
    {
        return await ScalarAsync<bool>(
            connection,
            """
            SELECT to_regclass(format('public.%I', @tableName)) IS NOT NULL;
            """,
            ("tableName", tableName));
    }

    private static async Task<bool> MigrationExistsAsync(NpgsqlConnection connection, string migrationId)
    {
        return await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = @migrationId
            );
            """,
            ("migrationId", migrationId));
    }

    private static async Task<int> DuplicateIndexCountAsync(NpgsqlConnection connection)
    {
        return await ScalarAsync<int>(
            connection,
            """
            SELECT COUNT(*)
            FROM (
                SELECT indexname
                FROM pg_indexes
                WHERE schemaname = 'public'
                GROUP BY indexname
                HAVING COUNT(*) > 1
            ) duplicate_indexes;
            """);
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Expected scalar result for SQL: {sql}"));
    }
}
