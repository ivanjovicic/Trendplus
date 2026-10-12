using Api.Middleware;
using Xunit;

namespace Api.Tests;

public sealed class RequestPerformanceLoggingMiddlewareTruncationTests
{
    [Theory]
    [InlineData(null, 3, null)]
    [InlineData("", 3, "")]
    [InlineData("abc", 3, "abc")]
    [InlineData("abcdef", 5, "ab...")]
    [InlineData("abcdef", 3, "abc")]
    [InlineData("abcdef", 2, "ab")]
    [InlineData("abcdef", 0, "")]
    public void Truncate_RespectsRequestedMaximum(string? value, int maxLength, string? expected)
    {
        Assert.Equal(expected, RequestPerformanceLoggingMiddleware.Truncate(value, maxLength));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2000)]
    [InlineData(3900)]
    public void Truncate_NeverExceedsColumnLimit(int maxLength)
    {
        var longValue = new string('x', maxLength + 100);
        var truncated = RequestPerformanceLoggingMiddleware.Truncate(longValue, maxLength);

        Assert.NotNull(truncated);
        Assert.Equal(maxLength, truncated.Length);
        Assert.EndsWith("...", truncated);
        Assert.Equal(new string('x', maxLength - 3) + "...", truncated);
    }

    [Fact]
    public void Truncate_RejectsNegativeMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RequestPerformanceLoggingMiddleware.Truncate("value", -1));
    }
}
