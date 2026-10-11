using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

[CollectionDefinition("isolated intelligence PostgreSQL", DisableParallelization = true)]
public sealed class IntelligencePostgresCollection : ICollectionFixture<IntelligencePostgresFixture>
{
    public const string Name = "isolated intelligence PostgreSQL";
}

/// <summary>
/// Owns a disposable PostgreSQL container for intelligence integration tests. It deliberately
/// ignores appsettings and RQ602_TEST_POSTGRES so these tests can never target a shared database.
/// </summary>
public sealed class IntelligencePostgresFixture : IAsyncLifetime
{
    private readonly Dictionary<string, DatabaseState> _databases = new(StringComparer.Ordinal);
    private PostgreSqlContainer? _container;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<IntelligenceTestDatabase> CreateDatabaseAsync(string purpose)
    {
        var container = RequireContainer();
        var safePurpose = string.Concat(purpose.Where(char.IsAsciiLetterOrDigit)).ToLowerInvariant();
        if (safePurpose.Length == 0)
        {
            safePurpose = "test";
        }

        var databaseName = $"rq614_{safePurpose}_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = databaseName,
            Pooling = false
        };
        var database = new IntelligenceTestDatabase(databaseName, connectionStringBuilder.ConnectionString);
        _databases.Add(database.ConnectionString, new DatabaseState(database));
        return database;
    }

    /// <summary>
    /// Creates a business-looking database name only inside this fixture's disposable container.
    /// It is intentionally not registered as a test database, so the bootstrap guard must reject it.
    /// </summary>
    public async Task<string> CreateUnregisteredBusinessNamedDatabaseForSafetyTestAsync()
    {
        const string databaseName = "trendplus";
        var container = RequireContainer();
        await using (var admin = new NpgsqlConnection(container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = databaseName,
            Pooling = false
        };
        return connectionStringBuilder.ConnectionString;
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(IntelligenceTestDatabase database)
    {
        RequireRegistered(database.ConnectionString);
        var connection = new NpgsqlConnection(database.ConnectionString);
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Runs a database bootstrap once, and only after proving that the exact target was issued by
    /// this fixture and has no pre-existing analytics_intel schema. Failure is propagated.
    /// </summary>
    public async Task RunFreshBootstrapAsync(string connectionString, Func<string, Task> bootstrap)
    {
        var state = RequireRegistered(connectionString);
        if (state.BootstrapAttempted)
        {
            throw new InvalidOperationException("A fresh bootstrap has already been attempted for this test database.");
        }

        await using (var connection = new NpgsqlConnection(state.Database.ConnectionString))
        {
            await connection.OpenAsync();
            await EnsureIntelligenceSchemaIsAbsentAsync(connection);
        }

        state.BootstrapAttempted = true;
        await bootstrap(state.Database.ConnectionString);
        state.BootstrapCompleted = true;
    }

    /// <summary>
    /// Allows the lifecycle test to repeat bootstrap only after its first guarded bootstrap
    /// completed in this fixture-owned database during the same test run.
    /// </summary>
    public async Task RunOwnedBootstrapRepeatAsync(IntelligenceTestDatabase database, Func<string, Task> bootstrap)
    {
        var state = RequireRegistered(database.ConnectionString);
        if (!state.BootstrapAttempted || !state.BootstrapCompleted)
        {
            throw new InvalidOperationException("Bootstrap repeats require a successful fresh bootstrap in this test run.");
        }

        await bootstrap(state.Database.ConnectionString);
    }

    public async Task ExecuteIntelligenceScriptsAsync(
        IntelligenceTestDatabase database,
        params string[] relativePaths)
    {
        await RunFreshBootstrapAsync(database.ConnectionString, async connectionString =>
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            foreach (var relativePath in relativePaths)
            {
                var filePath = FindRepositoryFile(relativePath);
                var sql = await File.ReadAllTextAsync(filePath);
                await using var command = new NpgsqlCommand(sql, connection)
                {
                    CommandTimeout = 0
                };
                await command.ExecuteNonQueryAsync();
            }
        });
    }

    private DatabaseState RequireRegistered(string connectionString)
    {
        if (!_databases.TryGetValue(connectionString, out var state))
        {
            throw new InvalidOperationException(
                "Database target was not issued by the disposable intelligence PostgreSQL fixture.");
        }

        var candidate = new NpgsqlConnectionStringBuilder(connectionString);
        var expected = new NpgsqlConnectionStringBuilder(RequireContainer().GetConnectionString())
        {
            Database = state.Database.Name,
            Pooling = false
        };

        if (!string.Equals(candidate.Host, expected.Host, StringComparison.OrdinalIgnoreCase)
            || candidate.Port != expected.Port
            || !string.Equals(candidate.Database, state.Database.Name, StringComparison.Ordinal)
            || !string.Equals(candidate.Username, expected.Username, StringComparison.Ordinal)
            || !string.Equals(candidate.Password, expected.Password, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Database connection does not match the fixture-issued disposable target.");
        }

        return state;
    }

    private static async Task EnsureIntelligenceSchemaIsAbsentAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT to_regnamespace('analytics_intel') IS NOT NULL;", connection);
        var exists = (bool)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Could not verify analytics_intel schema state."));
        if (exists)
        {
            throw new InvalidOperationException(
                "Refusing intelligence bootstrap because analytics_intel already exists in the target database.");
        }
    }

    private PostgreSqlContainer RequireContainer() =>
        _container ?? throw new InvalidOperationException("The disposable PostgreSQL container has not started.");

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private sealed class DatabaseState(IntelligenceTestDatabase database)
    {
        public IntelligenceTestDatabase Database { get; } = database;
        public bool BootstrapAttempted { get; set; }
        public bool BootstrapCompleted { get; set; }
    }
}

public sealed record IntelligenceTestDatabase(string Name, string ConnectionString);
