using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class ProductDecisionDisplayLabelTests
{
    [Theory]
    [InlineData(null, "Nije poznato")]
    [InlineData("", "Nije poznato")]
    [InlineData("missing_cost", "Nedostaje nabavna cena")]
    [InlineData("future_warning", "future_warning")]
    public void WarningCodeLabel_NeverReturnsNull(string? code, string expected)
    {
        Assert.Equal(expected, CachedAnalyticsEndpoints.DescribeProductDecisionWarningCode(code));
    }

    [Theory]
    [InlineData(null, "Nije poznato")]
    [InlineData("", "Nije poznato")]
    [InlineData("good", "dobar")]
    [InlineData("future_quality", "future_quality")]
    public void DataQualityLabel_NeverReturnsNull(string? status, string expected)
    {
        Assert.Equal(expected, CachedAnalyticsEndpoints.DescribeProductDecisionDataQualityStatus(status));
    }
}
