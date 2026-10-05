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

    [Fact]
    public void InsightStudioEndpointFamiliesUseCorrectUnknownGenderLiteral()
    {
        var v1 = ReadRepoFile("Api/Endpoints/InsightStudioEndpoints.cs");
        var v2 = ReadRepoFile("Api/Endpoints/InsightStudioV2Endpoints.cs");

        Assert.DoesNotContain("NeodreÄ‘eno", v1, StringComparison.Ordinal);
        Assert.DoesNotContain("NeodreÄ‘eno", v2, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(v1, "Neodređeno"));
        Assert.Equal(3, CountOccurrences(v2, "Neodređeno"));
    }

    [Fact]
    public void InsightStudioEndpointFamiliesUseCorrectUrgencyAndAgingLiterals()
    {
        var v1 = ReadRepoFile("Api/Endpoints/InsightStudioEndpoints.cs");
        var v2 = ReadRepoFile("Api/Endpoints/InsightStudioV2Endpoints.cs");

        // Residual RQ581 gap: urgency/aging labels were left as mojibake after the gender-literal fix,
        // which broke frontend filters that match the correct Serbian forms.
        Assert.DoesNotContain("KRITIÄŒNO", v1, StringComparison.Ordinal);
        Assert.DoesNotContain("KRITIÄŒNO", v2, StringComparison.Ordinal);
        Assert.DoesNotContain("PREPORUÄŒUJE SE", v1, StringComparison.Ordinal);
        Assert.DoesNotContain("PREPORUÄŒUJE SE", v2, StringComparison.Ordinal);
        Assert.DoesNotContain("KritiÄno", v1, StringComparison.Ordinal);

        Assert.Contains("KRITIČNO", v1, StringComparison.Ordinal);
        Assert.Contains("KRITIČNO", v2, StringComparison.Ordinal);
        Assert.Contains("PREPORUČUJE SE", v1, StringComparison.Ordinal);
        Assert.Contains("PREPORUČUJE SE", v2, StringComparison.Ordinal);
        Assert.Contains("Kritično", v1, StringComparison.Ordinal);
        Assert.True(CountOccurrences(v1, "KRITIČNO") >= 2);
        Assert.True(CountOccurrences(v2, "KRITIČNO") >= 3);
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
