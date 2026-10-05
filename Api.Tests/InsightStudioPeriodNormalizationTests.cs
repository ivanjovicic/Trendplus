using Xunit;

namespace Api.Tests;

public sealed class InsightStudioPeriodNormalizationTests
{
    [Fact]
    public void ToUtc_PreservesExplicitUtcValue()
    {
        var value = new DateTime(2026, 8, 1, 12, 30, 0, DateTimeKind.Utc);

        var normalized = Trendplus2.Endpoints.InsightStudioPeriod.ToUtc(value);

        Assert.Equal(value, normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }

    [Fact]
    public void ToUtc_ConvertsExplicitLocalValue()
    {
        var value = new DateTime(2026, 8, 1, 12, 30, 0, DateTimeKind.Local);

        var normalized = Trendplus2.Endpoints.InsightStudioPeriod.ToUtc(value);

        Assert.Equal(value.ToUniversalTime(), normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }

    [Fact]
    public void ToUtc_InterpretsDateOnlyValueInMachineLocalZone()
    {
        var value = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var normalized = Trendplus2.Endpoints.InsightStudioPeriod.ToUtc(value);

        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(value, TimeZoneInfo.Local), normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }

    [Fact]
    public void InsightStudioEndpointFamiliesShareTheSamePeriodNormalizer()
    {
        var v1 = ReadRepoFile("Api/Endpoints/InsightStudioEndpoints.cs");
        var v2 = ReadRepoFile("Api/Endpoints/InsightStudioV2Endpoints.cs");
        var period = ReadRepoFile("Api/Endpoints/InsightStudioPeriod.cs");

        Assert.True(CountOccurrences(v1, "ResolveInclusiveUtcRange") >= 6);
        Assert.Contains("ToUtcExclusiveEnd", period, StringComparison.Ordinal);
        Assert.True(CountOccurrences(v2, "InsightStudioPeriod.ToUtc") >= 14);
        Assert.DoesNotContain("DateTime.SpecifyKind", v1, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.SpecifyKind", v2, StringComparison.Ordinal);
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
