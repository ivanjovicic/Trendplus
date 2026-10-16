using Api.Services.Startup;
using Infrastructure.Analytics;
using Infrastructure.Database;
using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Collections.Concurrent;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class SupplierDecisionWindowedColdStartRegressionTests
{
    private readonly IntelligencePostgresFixture _fixture;

    public SupplierDecisionWindowedColdStartRegressionTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MissingOptionalSupplierCaches_DoNotGateReadiness_AndScorecardFailsClosed()
    {
        var defaultDb = await _fixture.CreateDatabaseAsync("rq634_readiness_default");
        var analyticsDb = await _fixture.CreateDatabaseAsync("rq634_readiness_analytics");
        await SupplierDecisionSchemaReadinessIntegrationTests.SeedSupplierSchemaPrerequisitesAsync(
            analyticsDb.ConnectionString);
        await CreateReadinessContractTableAsync(defaultDb.ConnectionString);

        var configuration = BuildConfiguration(
            defaultDb.ConnectionString,
            analyticsDb.ConnectionString,
            buildWindowedViews: false,
            buildSupplierCaches: false);
        using var services = CreateDatabaseServices(defaultDb.ConnectionString, analyticsDb.ConnectionString);

        await DatabaseInitializer.EnsureAnalyticsSupplierDecisionSchemaAsync(
            services,
            configuration,
            NullLogger.Instance);

        await using var connection = new NpgsqlConnection(analyticsDb.ConnectionString);
        await connection.OpenAsync();

        Assert.True(await RelationExistsAsync(connection, "vw_supplier_decision_score"));
        foreach (var cache in new[]
        {
            "mv_supplier_markdown_dependency_cache",
            "mv_supplier_decision_score_cache",
            "mv_supplier_recommendations_cache",
            "mv_supplier_decision_score_cache_90d",
            "mv_supplier_decision_score_cache_180d"
        })
        {
            Assert.False(await RelationExistsAsync(connection, cache));
        }

        var missingWindow = await PostgresMaterializedViewCapabilityReader.InspectAsync(
            connection,
            "mv_supplier_decision_score_cache_90d",
            SupplierDecisionMaterializedViewContract.DecisionScoreRequiredColumns);
        Assert.Equal("MISSING_OBJECT", missingWindow.ErrorCode);

        var failure = SupplierDecisionHubEndpoints.BuildDecisionScoreCapabilityException(missingWindow, 90);
        Assert.Equal("MISSING_OBJECT", failure.ErrorCode);
        Assert.Equal("supplier-scorecard-cache:90d", failure.ReadinessId);

        var readiness = new StartupReadinessState();
        readiness.MarkDatabaseInitializationNotRequired();
        Assert.Null(await PurchaseCostProvenanceSchemaContract.ValidateAsync(defaultDb.ConnectionString));
        var readinessLogs = new CapturingLoggerProvider();
        using var readinessLoggerFactory = LoggerFactory.Create(builder => builder.AddProvider(readinessLogs));
        using var warmup = new ReadinessWarmupHostedService(
            configuration,
            readiness,
            readinessLoggerFactory.CreateLogger<ReadinessWarmupHostedService>());
        await warmup.StartAsync(CancellationToken.None);
        await warmup.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.True(
            readiness.IsReady,
            $"reason={readiness.Reason}; defaultDbOk={readiness.DefaultDb.Ok} defaultError={readiness.DefaultDb.Error}; " +
            $"analyticsDbOk={readiness.AnalyticsDb.Ok} analyticsError={readiness.AnalyticsDb.Error}; " +
            $"initialization={readiness.DatabaseInitialization.State}; logs={string.Join(" | ", readinessLogs.Messages)}");
        Assert.Equal("ready", readiness.Reason);
        Assert.Equal("not_required", readiness.DatabaseInitialization.State);
        Assert.True(readiness.DefaultDb.Ok);
        Assert.True(readiness.AnalyticsDb.Ok);
    }

    [Fact]
    public async Task ExplicitMaintenanceOverrides_Build018And029CachesWithBoundedTimeouts()
    {
        var defaultDb = await _fixture.CreateDatabaseAsync("rq634_override_default");
        var analyticsDb = await _fixture.CreateDatabaseAsync("rq634_override_analytics");
        await SupplierDecisionSchemaReadinessIntegrationTests.SeedSupplierSchemaPrerequisitesAsync(
            analyticsDb.ConnectionString);

        var configuration = BuildConfiguration(
            defaultDb.ConnectionString,
            analyticsDb.ConnectionString,
            buildWindowedViews: true,
            buildSupplierCaches: true);
        var logProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(logProvider);
        });
        using var services = CreateDatabaseServices(defaultDb.ConnectionString, analyticsDb.ConnectionString);

        await DatabaseInitializer.EnsureAnalyticsSupplierDecisionSchemaAsync(
            services,
            configuration,
            loggerFactory.CreateLogger("RQ634"));

        await using var connection = new NpgsqlConnection(analyticsDb.ConnectionString);
        await connection.OpenAsync();
        foreach (var cache in new[]
        {
            "mv_supplier_markdown_dependency_cache",
            "mv_supplier_decision_score_cache",
            "mv_supplier_recommendations_cache",
            "mv_supplier_decision_score_cache_90d",
            "mv_supplier_decision_score_cache_180d"
        })
        {
            Assert.True(await IsPopulatedMaterializedViewAsync(connection, cache), cache);
        }

        Assert.Contains(logProvider.Messages, message =>
            message.Contains("Script=Database/Migrations/018_AddSupplierDecisionHubViews.sql", StringComparison.Ordinal)
            && message.Contains("CommandTimeout=300", StringComparison.Ordinal));
        Assert.Contains(logProvider.Messages, message =>
            message.Contains("Script=Database/Migrations/018_AddSupplierDecisionHubViews.sql", StringComparison.Ordinal)
            && message.Contains("CommandTimeout=30", StringComparison.Ordinal));
        Assert.Contains(logProvider.Messages, message =>
            message.Contains("Script=Database/Migrations/029_AddSupplierDecisionWindowedViews.sql", StringComparison.Ordinal)
            && message.Contains("CommandTimeout=300", StringComparison.Ordinal));
        Assert.DoesNotContain(logProvider.Messages, message =>
            message.Contains("018_AddSupplierDecisionHubViews.sql", StringComparison.Ordinal)
            && message.Contains("CommandTimeout=0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeferredInitializerAndSupplierRepair_SerializeTheirSplitAnalyticsSchemaWork()
    {
        var defaultDb = await _fixture.CreateDatabaseAsync("rq634_split_default");
        var analyticsDb = await _fixture.CreateDatabaseAsync("rq634_split_analytics");
        var configuration = BuildConfiguration(
            defaultDb.ConnectionString,
            analyticsDb.ConnectionString,
            buildWindowedViews: false,
            buildSupplierCaches: false,
            failFast: true);
        using var services = CreateDatabaseServices(defaultDb.ConnectionString, analyticsDb.ConnectionString);

        await _fixture.RunFreshBootstrapAsync(analyticsDb.ConnectionString, async _ =>
        {
            var outcome = await DatabaseInitializer.InitializeDatabasesAsync(
                services,
                configuration,
                NullLogger.Instance);
            Assert.False(outcome.CompletedWithErrors);
        });

        var initializer = DatabaseInitializer.InitializeDatabasesAsync(
            services,
            configuration,
            NullLogger.Instance);
        var repair = DatabaseInitializer.EnsureAnalyticsSupplierDecisionSchemaAsync(
            services,
            configuration,
            NullLogger.Instance);
        var outcomeAfterRestart = await initializer;
        await repair;

        Assert.False(outcomeAfterRestart.CompletedWithErrors);
        await using var connection = new NpgsqlConnection(analyticsDb.ConnectionString);
        await connection.OpenAsync();
        Assert.True(await RelationExistsAsync(connection, "vw_supplier_decision_score"));
        Assert.False(await RelationExistsAsync(connection, "mv_supplier_decision_score_cache"));
        Assert.False(await RelationExistsAsync(connection, "mv_supplier_decision_score_cache_90d"));
        Assert.False(await RelationExistsAsync(connection, "mv_supplier_decision_score_cache_180d"));
    }

    [Fact]
    public async Task SchemaInitializationLock_IsDatabaseScopedAndHonorsCancellation()
    {
        var unifiedDb = await _fixture.CreateDatabaseAsync("rq634_lock_unified");
        var splitAnalyticsDb = await _fixture.CreateDatabaseAsync("rq634_lock_split");
        var pooledUnifiedConnectionString = new NpgsqlConnectionStringBuilder(unifiedDb.ConnectionString)
        {
            Pooling = true
        }.ConnectionString;
        var logger = NullLogger.Instance;

        var initializerLock = await DatabaseInitializer.AcquireSupplierDecisionSchemaInitializationLockAsync(
            pooledUnifiedConnectionString,
            logger);
        var repairLockTask = DatabaseInitializer.AcquireSupplierDecisionSchemaInitializationLockAsync(
            pooledUnifiedConnectionString,
            logger);
        var splitDatabaseLock = await DatabaseInitializer.AcquireSupplierDecisionSchemaInitializationLockAsync(
            splitAnalyticsDb.ConnectionString,
            logger).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(repairLockTask.IsCompleted);
        await splitDatabaseLock.DisposeAsync();
        await initializerLock.DisposeAsync();
        var repairLock = await repairLockTask.WaitAsync(TimeSpan.FromSeconds(5));
        await repairLock.DisposeAsync();

        var lockHolder = await DatabaseInitializer.AcquireSupplierDecisionSchemaInitializationLockAsync(
            pooledUnifiedConnectionString,
            logger);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DatabaseInitializer.AcquireSupplierDecisionSchemaInitializationLockAsync(
                pooledUnifiedConnectionString,
                logger,
                cancellation.Token));
        await lockHolder.DisposeAsync();

        var afterCancellation = await DatabaseInitializer.AcquireSupplierDecisionSchemaInitializationLockAsync(
            pooledUnifiedConnectionString,
            logger).WaitAsync(TimeSpan.FromSeconds(5));
        await afterCancellation.DisposeAsync();
    }

    private static IConfiguration BuildConfiguration(
        string defaultConnectionString,
        string analyticsConnectionString,
        bool buildWindowedViews,
        bool buildSupplierCaches,
        bool failFast = false) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = defaultConnectionString,
            ["ConnectionStrings:AnalyticsConnection"] = analyticsConnectionString,
            ["DatabaseInitialization:FailFast"] = failFast.ToString(),
            ["DatabaseInitialization:RunFullSupplierDecisionHubOnStartup"] = "false",
            ["DatabaseInitialization:BuildSupplierDecisionHubCachesOnStartup"] = buildSupplierCaches.ToString(),
            ["DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup"] = buildWindowedViews.ToString(),
            ["Analytics:AllowLoopbackInProduction"] = "true",
            ["StartupReadiness:MaxWarmupAttempts"] = "1",
            ["StartupReadiness:RetryDelaySeconds"] = "1"
        })
        .Build();

    private static ServiceProvider CreateDatabaseServices(
        string defaultConnectionString,
        string analyticsConnectionString) => new ServiceCollection()
        .AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(defaultConnectionString).IgnoreManyServiceProvidersCreatedWarning())
        .AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(analyticsConnectionString).IgnoreManyServiceProvidersCreatedWarning())
        .BuildServiceProvider();

    private static async Task CreateReadinessContractTableAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "CREATE TABLE public.prodaja_stavke (purchase_cost_provenance varchar(64) NULL);",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> RelationExistsAsync(NpgsqlConnection connection, string relationName)
    {
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass(format('public.%I', @relationName)) IS NOT NULL;",
            connection);
        command.Parameters.AddWithValue("relationName", relationName);
        return (bool)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Could not inspect relation {relationName}."));
    }

    private static async Task<bool> IsPopulatedMaterializedViewAsync(NpgsqlConnection connection, string relationName)
    {
        await using var command = new NpgsqlCommand(
            "SELECT ispopulated FROM pg_matviews WHERE schemaname = 'public' AND matviewname = @relationName;",
            connection);
        command.Parameters.AddWithValue("relationName", relationName);
        var result = await command.ExecuteScalarAsync();
        return result is bool isPopulated && isPopulated;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
            }
        }
    }
}
