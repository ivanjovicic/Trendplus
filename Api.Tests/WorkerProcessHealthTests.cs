using Xunit;

namespace Api.Tests;

public sealed class WorkerProcessHealthTests
{
    [Fact]
    public void AllCoreHostEntrypointsStopTheHostWhenBackgroundServiceFails()
    {
        var apiProgram = ReadRepoFile("Api/Program.cs");
        var legacyProgram = ReadRepoFile("Trendplus2/Program.cs");

        AssertHostStopsOnBackgroundFailure(apiProgram);
        AssertHostStopsOnBackgroundFailure(legacyProgram);
    }

    private static void AssertHostStopsOnBackgroundFailure(string program)
    {
        Assert.Contains("Configure<HostOptions>", program, StringComparison.Ordinal);
        Assert.Contains(
            "options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BackgroundServiceExceptionBehavior.Ignore",
            program,
            StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
