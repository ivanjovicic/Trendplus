using Xunit;

namespace Api.Tests;

public sealed class AnalyticsDetailLocalizedLabelTests
{
    [Fact]
    public void DetailProjectionsDoNotExposeResidualEnglishOrAsciiLabels()
    {
        var source = ReadRepoFile("Api/Services/AnalyticsDetailReadService.cs");

        Assert.DoesNotContain("Label = \"Insufficient data\"", source);
        Assert.DoesNotContain("nivelacija impact %", source);
        Assert.DoesNotContain("snapshot troskom", source);
        Assert.DoesNotContain("snapshot troškom", source);
        Assert.DoesNotContain("fallback troška", source);
        Assert.DoesNotContain("fallback troškom", source);
        Assert.DoesNotContain("\"Napomena za marzu\"", source);
        Assert.DoesNotContain("\"Procenjena marza %\"", source);
        Assert.Contains("Label = \"Nedovoljno podataka\"", source);
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
