using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class ShoeTypeIdentityPolicyTests
{
    [Fact]
    public void UnknownIdentity_IsNullIdOnly()
    {
        Assert.True(ShoeTypeIdentityPolicy.IsUnknownId(null));
        Assert.False(ShoeTypeIdentityPolicy.IsUnknownId(7));
    }

    [Fact]
    public void DisplayName_DoesNotMakeNamedNepoznatoUnknown()
    {
        Assert.Equal("Nepoznato", ShoeTypeIdentityPolicy.DisplayName(12, "Nepoznato"));
        Assert.False(ShoeTypeIdentityPolicy.IsUnknownId(12));
        Assert.Equal(string.Empty, ShoeTypeIdentityPolicy.DisplayName(12, "  "));
        Assert.Equal("Nepoznato", ShoeTypeIdentityPolicy.DisplayName(null, "Cipele"));
    }

    [Fact]
    public void BucketKey_RoundTripsKnownAndUnknown()
    {
        Assert.Equal("unknown", ShoeTypeIdentityPolicy.BucketKey(null));
        Assert.Equal("id:42", ShoeTypeIdentityPolicy.BucketKey(42));
        Assert.Null(ShoeTypeIdentityPolicy.TryParseBucketKey("unknown"));
        Assert.Equal(42, ShoeTypeIdentityPolicy.TryParseBucketKey("id:42"));
    }
}
