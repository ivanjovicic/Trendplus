using Application.Analytics;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class AnalyticsActionConstantsTests
{
    [Fact]
    public void SourceTypes_ContainsAllValidTypes()
    {
        var types = AnalyticsActionConstants.SourceTypes.AllValues;
        Assert.Contains("dashboard", types);
        Assert.Contains("product", types);
        Assert.Contains("supplier", types);
        Assert.Contains("inventory", types);
        Assert.Contains("nivelacija", types);
        Assert.Contains("data_quality", types);
        Assert.Equal(6, types.Length);
    }

    [Fact]
    public void Priorities_ContainsAllValid()
    {
        var priorities = AnalyticsActionConstants.Priorities.AllValues;
        Assert.Contains("P1", priorities);
        Assert.Contains("P2", priorities);
        Assert.Contains("P3", priorities);
        Assert.Equal(3, priorities.Length);
    }

    [Fact]
    public void Statuses_ContainsAllValid()
    {
        var statuses = AnalyticsActionConstants.Statuses.AllValues;
        Assert.Contains("new", statuses);
        Assert.Contains("accepted", statuses);
        Assert.Contains("deferred", statuses);
        Assert.Contains("rejected", statuses);
        Assert.Contains("done", statuses);
        Assert.Equal(5, statuses.Length);
    }

    [Fact]
    public void Statuses_OpenStatusesCorrect()
    {
        var openStatuses = AnalyticsActionConstants.Statuses.OpenStatuses;
        Assert.Contains("new", openStatuses);
        Assert.Contains("accepted", openStatuses);
        Assert.Contains("deferred", openStatuses);
        Assert.DoesNotContain("rejected", openStatuses);
        Assert.DoesNotContain("done", openStatuses);
    }

    [Fact]
    public void DataQualityStatuses_ContainsCanonicalValues()
    {
        var statuses = AnalyticsActionConstants.DataQualityStatuses.AllValues;
        Assert.Contains("good", statuses);
        Assert.Contains("warning", statuses);
        Assert.Contains("critical", statuses);
        Assert.Contains("insufficient_data", statuses);
        Assert.Equal(4, statuses.Length);
    }

    [Fact]
    public void DataQualityStatuses_LegacyMappings()
    {
        var mappings = AnalyticsActionConstants.DataQualityStatuses.LegacyMappings;
        Assert.Equal("warning", mappings["fair"]);
        Assert.Equal("critical", mappings["poor"]);
    }

    [Theory]
    [InlineData("dashboard", true)]
    [InlineData("product", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidSourceType_WorksCorrectly(string sourceType, bool expected)
    {
        var result = AnalyticsActionConstants.IsValidSourceType(sourceType);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("P1", true)]
    [InlineData("P2", true)]
    [InlineData("P4", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPriority_WorksCorrectly(string priority, bool expected)
    {
        var result = AnalyticsActionConstants.IsValidPriority(priority);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("new", true)]
    [InlineData("done", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidStatus_WorksCorrectly(string status, bool expected)
    {
        var result = AnalyticsActionConstants.IsValidStatus(status);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("good", true)]
    [InlineData("warning", true)]
    [InlineData("critical", true)]
    [InlineData("insufficient_data", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidDataQualityStatus_WorksCorrectly(string status, bool expected)
    {
        var result = AnalyticsActionConstants.IsValidDataQualityStatus(status);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("fair", "warning")]
    [InlineData("poor", "critical")]
    [InlineData("good", "good")]
    [InlineData("warning", "warning")]
    [InlineData("GOOD", "good")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeDataQualityStatus_WorksCorrectly(string rawValue, string expected)
    {
        var result = AnalyticsActionConstants.NormalizeDataQualityStatus(rawValue);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeDataQualityStatus_LegacyFair_MapsToWarning()
    {
        var result = AnalyticsActionConstants.NormalizeDataQualityStatus("fair");
        Assert.Equal("warning", result);
    }

    [Fact]
    public void NormalizeDataQualityStatus_LegacyPoor_MapsToCritical()
    {
        var result = AnalyticsActionConstants.NormalizeDataQualityStatus("poor");
        Assert.Equal("critical", result);
    }
}
