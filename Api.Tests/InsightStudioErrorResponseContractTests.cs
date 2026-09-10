using Xunit;

namespace Api.Tests;

public sealed class InsightStudioErrorResponseContractTests
{
    [Fact]
    public void InsightStudioV1_HandledErrorsSanitizeResponseDetails()
    {
        var source = ReadRepoFile("Api/Endpoints/InsightStudioEndpoints.cs");

        Assert.DoesNotContain("Results.Problem(detail: ex.Message", source, StringComparison.Ordinal);
        Assert.Equal(7, CountOccurrences(source, "return await CreateSafeErrorResponseAsync("));
        Assert.Contains(
            "Insight Studio trenutno nije dostupan. Pokušajte ponovo ili kontaktirajte podršku.",
            source,
            StringComparison.Ordinal);
        Assert.Contains("HandledErrorLogging.PersistHandledExceptionAsync", source, StringComparison.Ordinal);
        Assert.Contains("Greška KPI snapshot", source, StringComparison.Ordinal);
        Assert.Contains("Greška reorder plan", source, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
        => text.Split(value, StringSplitOptions.None).Length - 1;

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
