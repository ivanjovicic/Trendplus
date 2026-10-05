using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaLocalizedCopyTests
{
    [Fact]
    public void PreNivelacijaUserFacingCopyAvoidsEnglishAndTechnicalTokens()
    {
        var scoring = ReadRepoFile("Api/Services/PreNivelacijaScoringService.cs");
        var endpoints = ReadRepoFile("Api/Endpoints/PreNivelacijaPriorityEndpoints.cs");
        var models = ReadRepoFile("Api/Models/PreNivelacijaPriorityModels.cs");

        Assert.DoesNotContain("\"Increase focus\"", scoring);
        Assert.DoesNotContain("\"Insufficient data\"", scoring);
        Assert.Contains("\"Nedovoljno podataka\"", scoring);

        Assert.DoesNotContain("\"Unassigned\"", endpoints);
        Assert.DoesNotContain("high-priority SKU", endpoints);
        Assert.DoesNotContain(" WoW", endpoints);
        Assert.DoesNotContain("velocity (", endpoints);
        Assert.Contains("ponovljena sniženja", endpoints);
        Assert.Contains("brzinu prodaje", endpoints);
        Assert.Contains("SKU visokog prioriteta", endpoints);
        Assert.Contains("nedeljna promena", endpoints);
        Assert.Contains("\"Nedodeljeno\"", endpoints);

        Assert.DoesNotContain("= \"Insufficient data\"", models);
        Assert.DoesNotContain("= \"Unassigned\"", models);
        Assert.Contains("= \"Nedovoljno podataka\"", models);
        Assert.Contains("= \"Nedodeljeno\"", models);
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
