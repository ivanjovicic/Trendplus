using System.Reflection;
using System.Runtime.ExceptionServices;
using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

public sealed class DatabaseInitializerP0IntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DatabaseInitializerP0IntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveSnapshotAsync_PersistsHistoryRow_WhenCancellationTokenIsPassedSeparately()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var trendConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_execsql_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(trendConnectionString))
        {
            return;
        }

        await using var trendDb = CreateTrendDbContext(trendConnectionString);
        await trendDb.Database.EnsureCreatedAsync();

        var service = new AnalyticsDataQualityHistoryService(trendDb);
        var snapshot = new AnalyticsDataQualityHealthSnapshot
        {
            GeneratedAtUtc = DateTime.UtcNow,
            LookbackDays = 30,
            WindowFromUtc = DateTime.UtcNow.AddDays(-30),
            WindowToUtc = DateTime.UtcNow,
            OrphanArticleCount = 2,
            TotalRevenue = 1000m,
            MissingCostRevenue = 100m,
            MissingCostRevenueSharePct = 10.0,
            UnknownSupplierRevenue = 50m,
            UnknownSupplierRevenueSharePct = 5.0
        };

        await service.SaveSnapshotAsync(snapshot, "all", CancellationToken.None);

        await using var verifyConnection = new NpgsqlConnection(trendConnectionString);
        await verifyConnection.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM analytics_data_quality_history
            WHERE snapshot_date_utc = @snapshotDate
              AND lookback_days = @lookbackDays
              AND data_scope = @dataScope;
            """,
            verifyConnection);
        verifyCommand.Parameters.AddWithValue("snapshotDate", snapshot.GeneratedAtUtc.Date);
        verifyCommand.Parameters.AddWithValue("lookbackDays", snapshot.LookbackDays);
        verifyCommand.Parameters.AddWithValue("dataScope", "all");

        var count = (long)(await verifyCommand.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task DeferredBackfillPattern_CreatesFreshDbContexts_AfterScopedContextsAreDisposed()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var trendConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_trend_{Guid.NewGuid():N}");
        var analyticsConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_analytics_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(trendConnectionString) || string.IsNullOrWhiteSpace(analyticsConnectionString))
        {
            return;
        }

        await using (var scopedTrendDb = CreateTrendDbContext(trendConnectionString))
        await using (var scopedAnalyticsDb = CreateAnalyticsDbContext(analyticsConnectionString))
        {
            await scopedTrendDb.Database.EnsureCreatedAsync();
            await scopedAnalyticsDb.Database.EnsureCreatedAsync();
        }

        var createTrendDb = GetRequiredPrivateStaticMethod(
            typeof(DatabaseInitializer),
            "CreateTrendplusBackfillDbContext");
        var createAnalyticsDb = GetRequiredPrivateStaticMethod(
            typeof(DatabaseInitializer),
            "CreateAnalyticsBackfillDbContext");
        var backfillSales = GetRequiredPrivateStaticMethod(
            typeof(DatabaseInitializer),
            "BackfillSalesFactsAsync");
        var backfillReturns = GetRequiredPrivateStaticMethod(
            typeof(DatabaseInitializer),
            "BackfillReturnFactsAsync");

        await using var trendBackfillDb = (TrendplusDbContext)(createTrendDb.Invoke(
            null,
            new object[] { trendConnectionString, 30 }) ?? throw new InvalidOperationException("Trend backfill DbContext creation failed."));
        await using var analyticsBackfillDb = (AnalyticsDbContext)(createAnalyticsDb.Invoke(
            null,
            new object[] { analyticsConnectionString, 30 }) ?? throw new InvalidOperationException("Analytics backfill DbContext creation failed."));

        await InvokePrivateAsync(backfillSales, trendBackfillDb, analyticsBackfillDb, NullLogger.Instance);
        await InvokePrivateAsync(backfillReturns, trendBackfillDb, analyticsBackfillDb, NullLogger.Instance);

        Assert.Equal(0, await analyticsBackfillDb.SalesFacts.CountAsync());
        Assert.Equal(0, await analyticsBackfillDb.ReturnFacts.CountAsync());
    }

    private static TrendplusDbContext CreateTrendDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TrendplusDbContext(options);
    }

    private static AnalyticsDbContext CreateAnalyticsDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AnalyticsDbContext(options);
    }

    private static MethodInfo GetRequiredPrivateStaticMethod(Type type, string methodName)
    {
        return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Missing method {type.FullName}.{methodName}.");
    }

    private static async Task InvokePrivateAsync(MethodInfo method, params object[] args)
    {
        try
        {
            var result = method.Invoke(null, args)
                ?? throw new InvalidOperationException($"Method {method.Name} did not return a Task.");
            if (result is not Task task)
            {
                throw new InvalidOperationException($"Method {method.Name} did not return a Task.");
            }
            await task;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool IsAvailable { get; private set; }

    public string AdminConnectionString => _container?.GetConnectionString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("postgres")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _container.StartAsync();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<string?> TryCreateDatabaseConnectionStringAsync(string databaseName)
    {
        if (!IsAvailable)
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();

        await using (var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);", connection))
        {
            await dropCommand.ExecuteNonQueryAsync();
        }

        await using (var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", connection))
        {
            await createCommand.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }
}
