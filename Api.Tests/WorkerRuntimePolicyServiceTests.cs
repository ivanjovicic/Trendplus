using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Api.Tests;

public sealed class WorkerRuntimePolicyServiceTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public WorkerRuntimePolicyServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SchemaGuard_CreatesWorkerRuntimeSettings_WithExpectedColumnsAndUniqueWorkerName()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_worker_settings_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var db = CreateTrendDbContext(connectionString!);
        var schemaReady = await WorkerRuntimeSettingsSchemaGuard.EnsureSchemaAsync(
            db,
            NullLogger.Instance,
            CancellationToken.None);
        Assert.True(schemaReady);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string columnSql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND lower(table_name) = lower('WorkerRuntimeSettings');
            """;

        await using (var columnCommand = new NpgsqlCommand(columnSql, connection))
        await using (var reader = await columnCommand.ExecuteReaderAsync())
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            Assert.Contains("Id", columns);
            Assert.Contains("WorkerName", columns);
            Assert.Contains("IsManuallyStopped", columns);
            Assert.Contains("IsScheduleEnabled", columns);
            Assert.Contains("Notes", columns);
            Assert.Contains("UpdatedAtUtc", columns);
            Assert.Contains("UpdatedBy", columns);
        }

        db.WorkerRuntimeSettings.Add(new Domain.Model.WorkerRuntimeSettings
        {
            WorkerName = "AccessImportBackgroundWorker",
            IsScheduleEnabled = true,
            IsManuallyStopped = false,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = "test"
        });
        await db.SaveChangesAsync();

        db.WorkerRuntimeSettings.Add(new Domain.Model.WorkerRuntimeSettings
        {
            WorkerName = "AccessImportBackgroundWorker",
            IsScheduleEnabled = true,
            IsManuallyStopped = false,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = "test"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task GetPolicyAsync_CreatesDefaults_WhenRowDoesNotExist()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_worker_policy_defaults_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var provider = CreateProvider(connectionString!);
        var service = provider.GetRequiredService<WorkerRuntimePolicyService>();
        var policy = await service.GetPolicyAsync("TestWorker");

        Assert.True(policy.CanRunNow);
        Assert.True(policy.IsScheduleEnabled);
        Assert.False(policy.IsManuallyStopped);
        Assert.Equal("TestWorker", policy.WorkerName);

        await using var verifyDb = CreateTrendDbContext(connectionString!);
        var row = await verifyDb.WorkerRuntimeSettings.SingleAsync(x => x.WorkerName == "TestWorker");
        Assert.False(row.IsManuallyStopped);
        Assert.True(row.IsScheduleEnabled);
    }

    [Fact]
    public async Task GetPolicyAsync_BlocksWorker_WhenManuallyStopped()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_worker_policy_manual_stop_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using (var setupDb = CreateTrendDbContext(connectionString!))
        {
            await WorkerRuntimeSettingsSchemaGuard.EnsureSchemaAsync(setupDb, NullLogger.Instance, CancellationToken.None);
            setupDb.WorkerRuntimeSettings.Add(new Domain.Model.WorkerRuntimeSettings
            {
                WorkerName = "TestWorker",
                IsScheduleEnabled = true,
                IsManuallyStopped = true,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedBy = "test"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var provider = CreateProvider(connectionString!);
        var service = provider.GetRequiredService<WorkerRuntimePolicyService>();
        var policy = await service.GetPolicyAsync("TestWorker");

        Assert.False(policy.CanRunNow);
        Assert.True(policy.IsManuallyStopped);
        Assert.NotNull(policy.PauseReason);
        Assert.Contains("ru", policy.PauseReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingTable_DoesNotCrashAndRecovers_WhenSchemaWasDropped()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_worker_policy_missing_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using (var setupDb = CreateTrendDbContext(connectionString!))
        {
            await WorkerRuntimeSettingsSchemaGuard.EnsureSchemaAsync(setupDb, NullLogger.Instance, CancellationToken.None);
        }

        await using (var dropConn = new NpgsqlConnection(connectionString))
        {
            await dropConn.OpenAsync();
            await using var dropCmd = new NpgsqlCommand("""DROP TABLE IF EXISTS "WorkerRuntimeSettings";""", dropConn);
            await dropCmd.ExecuteNonQueryAsync();
        }

        await using var provider = CreateProvider(connectionString!);
        var service = provider.GetRequiredService<WorkerRuntimePolicyService>();

        var first = await service.GetPolicyAsync("RecoveredWorker");
        var second = await service.GetPolicyAsync("RecoveredWorker");

        Assert.True(first.CanRunNow);
        Assert.True(second.CanRunNow);
        Assert.Equal("RecoveredWorker", second.WorkerName);
    }

    [Fact]
    public async Task MissingRelationDetection_RecognizesPostgres42P01_ForWorkerRuntimeSettings()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_worker_detect_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        PostgresException? captured = null;
        try
        {
            await using var command = new NpgsqlCommand("""SELECT * FROM "WorkerRuntimeSettings";""", connection);
            await command.ExecuteReaderAsync();
        }
        catch (PostgresException ex)
        {
            captured = ex;
        }

        Assert.NotNull(captured);
        Assert.True(WorkerRuntimeSettingsSchemaGuard.IsMissingRelationException(captured!));
    }

    [Fact]
    public void ReportMissingSchema_LogsDetailedErrorOnlyOnce()
    {
        var collector = new TestLogCollector();
        var logger = collector.CreateLogger<WorkerRuntimePolicyService>();
        var ex = new InvalidOperationException("42P01: relation \"WorkerRuntimeSettings\" does not exist");

        WorkerRuntimeSettingsSchemaGuard.ReportMissingSchema(logger, ex, "test-1");
        WorkerRuntimeSettingsSchemaGuard.ReportMissingSchema(logger, ex, "test-2");

        Assert.True(collector.Entries.Count(e => e.Level == LogLevel.Error) <= 1);
        Assert.True(collector.Entries.Count(e => e.Level == LogLevel.Debug) >= 1);
    }

    private static TrendplusDbContext CreateTrendDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TrendplusDbContext(options);
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<WorkerRuntimePolicyService>();
        return services.BuildServiceProvider();
    }

    private sealed class TestLogCollector
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public ILogger<T> CreateLogger<T>()
        {
            return new CollectorLogger<T>(Entries);
        }
    }

    private sealed class CollectorLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel Level, string Message)> _entries;

        public CollectorLogger(List<(LogLevel Level, string Message)> entries)
        {
            _entries = entries;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
