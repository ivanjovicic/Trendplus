using Xunit;

namespace Api.Tests;

public sealed class DatabaseMigrationOwnershipTests
{
    [Fact]
    public void ProgramDoesNotRunASecondSynchronousEfMigration()
    {
        var program = ReadRepoFile("Api/Program.cs");
        var deferredService = ReadRepoFile("Api/Services/Startup/DeferredStartupTasksHostedService.cs");

        Assert.DoesNotContain("Database.Migrate()", program, StringComparison.Ordinal);
        Assert.Contains("DeferredStartupTasksHostedService", program, StringComparison.Ordinal);
        Assert.Contains("DatabaseInitializer.InitializeDatabasesAsync", deferredService, StringComparison.Ordinal);
    }

    [Fact]
    public void DeferredDatabaseInitializationIsEligibleForWebProcessWhenRegistered()
    {
        var catalog = ReadRepoFile("Infrastructure/Services/WorkerRegistryCatalog.cs");

        Assert.Contains(
            "WorkerName: \"DeferredStartupTasksHostedService\"",
            catalog,
            StringComparison.Ordinal);

        var deferredBlock = catalog[catalog.IndexOf(
            "WorkerName: \"DeferredStartupTasksHostedService\"",
            StringComparison.Ordinal)..];

        Assert.Contains("RegistersInWorkerProcess: true", deferredBlock, StringComparison.Ordinal);
        Assert.Contains("RegistersInWebProcess: true", deferredBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void AdvisoryLockTimeoutFailsClosedAndStopsDeferredStartup()
    {
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");
        var deferredService = ReadRepoFile("Api/Services/Startup/DeferredStartupTasksHostedService.cs");

        Assert.Contains("throw timeoutException;", initializer, StringComparison.Ordinal);
        Assert.Contains("catch (DatabaseInitializationLockTimeoutException ex)", deferredService, StringComparison.Ordinal);
        Assert.Contains("catch (StartupMigrationSequenceException ex)", deferredService, StringComparison.Ordinal);
        Assert.Contains("_hostApplicationLifetime.StopApplication();", deferredService, StringComparison.Ordinal);
        Assert.DoesNotContain("Skipping database initialization because advisory startup lock", initializer, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalStartupMigrationsUseExplicitDependencyOrder()
    {
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");

        Assert.Contains("012 -> 017 -> 019", initializer, StringComparison.Ordinal);
        Assert.Contains("EnsureStartupSqlDependencyAsync", initializer, StringComparison.Ordinal);
        Assert.Contains("failClosed: true", initializer, StringComparison.Ordinal);
        Assert.Contains("StartupMigrationSequenceException", initializer, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.WhenAll(independentTasks)", initializer, StringComparison.Ordinal);
        Assert.DoesNotContain("Parallel migration {File} encountered an error; continuing.", initializer, StringComparison.Ordinal);
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
}
