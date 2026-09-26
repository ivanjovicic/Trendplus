using Api.Services.Startup;
using Xunit;

namespace Api.Tests;

public sealed class ProductionMigrationStartupContractTests
{
    [Fact]
    public void RenderWebServiceOwnsFailFastDatabaseInitializationAndReadinessGate()
    {
        var render = ReadRepoFile("render.yaml");
        var webStart = render.IndexOf("name: trendplus-api", StringComparison.Ordinal);
        var workerStart = render.IndexOf("name: trendplus-worker", StringComparison.Ordinal);

        Assert.True(webStart >= 0);
        Assert.True(workerStart > webStart);

        var web = render[webStart..workerStart];
        Assert.Contains("healthCheckPath: /ready", web, StringComparison.Ordinal);
        Assert.Contains("Database__AutoMigrate", web, StringComparison.Ordinal);
        Assert.Contains("DatabaseInitialization__FailFast", web, StringComparison.Ordinal);
        Assert.Contains("StartupTasks__RunDatabaseInitialization", web, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadinessCannotBecomeReadyBeforeRequiredDatabaseInitializationCompletes()
    {
        var readiness = new StartupReadinessState();
        readiness.RequireDatabaseInitialization();
        readiness.ReportProbe(
            new StartupReadinessState.DatabaseProbeState { Ok = true },
            new StartupReadinessState.DatabaseProbeState { Ok = true });

        readiness.MarkReady();

        Assert.False(readiness.IsReady);
        Assert.Equal("database_initialization", readiness.Reason);

        readiness.MarkDatabaseInitializationCompleted();

        Assert.True(readiness.IsReady);
        Assert.Equal("ready", readiness.Reason);
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
