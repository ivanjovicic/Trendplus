using Npgsql;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class AnalyticsIntelligenceDatabaseSafetyTests
{
    private readonly IntelligencePostgresFixture _fixture;

    public AnalyticsIntelligenceDatabaseSafetyTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BusinessNamedDatabaseIsRejectedBeforeBootstrapAndRemainsUnchanged()
    {
        // This database exists only inside the disposable Testcontainers instance.
        var connectionString = await _fixture.CreateUnregisteredBusinessNamedDatabaseForSafetyTestAsync();
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand(
                "CREATE TABLE rq614_sentinel (value integer NOT NULL); INSERT INTO rq614_sentinel VALUES (614);",
                connection);
            await seed.ExecuteNonQueryAsync();
        }

        var bootstrapCalls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fixture.RunFreshBootstrapAsync(connectionString, _ =>
            {
                bootstrapCalls++;
                return Task.CompletedTask;
            }));

        Assert.Equal(0, bootstrapCalls);
        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();
        Assert.Equal(614, await ScalarAsync<int>(verify, "SELECT value FROM rq614_sentinel;"));
        Assert.False(await ScalarAsync<bool>(verify, "SELECT to_regnamespace('analytics_intel') IS NOT NULL;"));
    }

    [Fact]
    public async Task UnreachablePostgresConnectionFailsInsteadOfPassingSilently()
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "postgres",
            Username = "rq614_unreachable",
            Password = "not-a-real-credential",
            Timeout = 1,
            Pooling = false
        }.ConnectionString;

        await using var connection = new NpgsqlConnection(connectionString);
        await Assert.ThrowsAnyAsync<NpgsqlException>(() => connection.OpenAsync());
    }

    [Fact]
    public async Task PreExistingIntelligenceSchemaIsRejectedWithoutMutation()
    {
        var database = await _fixture.CreateDatabaseAsync("existing_schema");
        await using (var connection = await _fixture.OpenConnectionAsync(database))
        {
            await using var seed = new NpgsqlCommand(
                "CREATE SCHEMA analytics_intel; CREATE TABLE analytics_intel.rq614_sentinel (value integer NOT NULL); INSERT INTO analytics_intel.rq614_sentinel VALUES (614);",
                connection);
            await seed.ExecuteNonQueryAsync();
        }

        var bootstrapCalls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fixture.RunFreshBootstrapAsync(database.ConnectionString, _ =>
            {
                bootstrapCalls++;
                return Task.CompletedTask;
            }));

        Assert.Equal(0, bootstrapCalls);
        await using var verify = await _fixture.OpenConnectionAsync(database);
        Assert.Equal(614, await ScalarAsync<int>(verify, "SELECT value FROM analytics_intel.rq614_sentinel;"));
        Assert.True(await ScalarAsync<bool>(verify, "SELECT to_regnamespace('analytics_intel') IS NOT NULL;"));
        Assert.False(await ScalarAsync<bool>(verify, "SELECT to_regclass('analytics_intel.vw_product_demand_signals_v1') IS NOT NULL;"));
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Expected a scalar result for '{sql}'.");
        return (T)value;
    }
}
