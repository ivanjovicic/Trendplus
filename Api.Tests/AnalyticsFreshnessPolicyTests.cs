using Api.Config;
using Api.Services;

namespace Api.Tests;

public sealed class AnalyticsFreshnessPolicyTests
{
    private static readonly AnalyticsFreshnessOptions DefaultOptions = new();

    [Fact]
    public void Resolve_ReturnsFresh_WhenImportIsWithinWarningThreshold()
    {
        var nowUtc = DateTime.UtcNow;

        var status = AnalyticsFreshnessPolicy.Resolve(
            nowUtc.AddHours(-48),
            null,
            nowUtc,
            DefaultOptions);

        Assert.Equal("fresh", status);
    }

    [Fact]
    public void Resolve_ReturnsStale_WhenImportIsOlderThanWarningThreshold()
    {
        var nowUtc = DateTime.UtcNow;

        var status = AnalyticsFreshnessPolicy.Resolve(
            nowUtc.AddHours(-48).AddSeconds(-1),
            null,
            nowUtc,
            DefaultOptions);

        Assert.Equal("stale", status);
    }

    [Fact]
    public void Resolve_ReturnsCritical_WhenImportReachesCriticalThreshold()
    {
        var nowUtc = DateTime.UtcNow;

        var status = AnalyticsFreshnessPolicy.Resolve(
            nowUtc.AddHours(-168),
            null,
            nowUtc,
            DefaultOptions);

        Assert.Equal("critical", status);
    }

    [Fact]
    public void Resolve_ReturnsUnknown_WhenNoSuccessfulImportExists()
    {
        var status = AnalyticsFreshnessPolicy.Resolve(
            null,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow,
            DefaultOptions);

        Assert.Equal("unknown", status);
    }

    [Fact]
    public void Resolve_ReturnsCritical_WhenFailureIsNewerThanSuccessfulImport()
    {
        var nowUtc = DateTime.UtcNow;

        var status = AnalyticsFreshnessPolicy.Resolve(
            nowUtc.AddHours(-1),
            nowUtc.AddMinutes(-5),
            nowUtc,
            DefaultOptions);

        Assert.Equal("critical", status);
    }

    [Fact]
    public void Resolve_UsesConfiguredThresholds()
    {
        var nowUtc = DateTime.UtcNow;
        var options = new AnalyticsFreshnessOptions
        {
            WarningAfterHours = 2,
            CriticalAfterHours = 4
        };

        var status = AnalyticsFreshnessPolicy.Resolve(
            nowUtc.AddHours(-3),
            null,
            nowUtc,
            options);

        Assert.Equal("stale", status);
    }
}
