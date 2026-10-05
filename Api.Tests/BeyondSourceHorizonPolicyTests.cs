using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class BeyondSourceHorizonPolicyTests
{
    [Fact]
    public void IsBeyond_FlagsPartialAndEntirelyUnobservedCurrentPeriods()
    {
        var horizon = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);

        Assert.False(BeyondSourceHorizonPolicy.IsBeyond(horizon.AddDays(1), horizon));
        Assert.True(BeyondSourceHorizonPolicy.IsBeyond(horizon.AddDays(2), horizon));
        Assert.True(BeyondSourceHorizonPolicy.IsBeyond(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), horizon));
    }
}
