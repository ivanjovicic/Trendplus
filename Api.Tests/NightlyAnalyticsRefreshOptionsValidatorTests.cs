using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class NightlyAnalyticsRefreshOptionsValidatorTests
{
    [Theory]
    [InlineData("00:00")]
    [InlineData("00:10")]
    [InlineData("23:59")]
    public void Validate_AcceptsTwentyFourHourUtcTimes(string runAtUtc)
    {
        var result = new NightlyAnalyticsRefreshOptionsValidator().Validate(
            Options.DefaultName,
            new NightlyAnalyticsRefreshOptions { RunAtUtc = runAtUtc });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1:10")]
    [InlineData("24:00")]
    [InlineData("00:60")]
    [InlineData("not-a-time")]
    public void Validate_RejectsMalformedUtcTimes(string runAtUtc)
    {
        var result = new NightlyAnalyticsRefreshOptionsValidator().Validate(
            Options.DefaultName,
            new NightlyAnalyticsRefreshOptions { RunAtUtc = runAtUtc });

        Assert.True(result.Failed);
        Assert.Contains("RunAtUtc", result.FailureMessage ?? string.Empty);
    }
}
