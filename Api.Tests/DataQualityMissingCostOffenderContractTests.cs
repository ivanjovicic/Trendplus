using Application.Analytics.Queries.GetDataQualityIssues;
using Infrastructure.Services;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class DataQualityMissingCostOffenderContractTests
{
    [Theory]
    [InlineData(DataQualityIssueTypes.MissingSupplier)]
    [InlineData(DataQualityIssueTypes.MissingShoeType)]
    [InlineData(DataQualityIssueTypes.InvalidName)]
    [InlineData(DataQualityIssueTypes.MissingCost)]
    public void TryNormalizeTopOffender_AcceptsKnownTypes(string issueType)
    {
        Assert.True(DataQualityIssueTypes.TryNormalizeTopOffender(issueType, out var normalized));
        Assert.Equal(issueType, normalized);
        Assert.Equal(issueType, AnalyticsDataQualityHealthService.NormalizeTopOffenderIssueType(issueType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing_cost")]
    [InlineData("unknownType")]
    [InlineData("missingSupplierr")]
    public void TryNormalizeTopOffender_RejectsUnknownTypes_WithoutSilentSupplierFallback(string? issueType)
    {
        Assert.False(DataQualityIssueTypes.TryNormalizeTopOffender(issueType, out _));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AnalyticsDataQualityHealthService.NormalizeTopOffenderIssueType(issueType));

        // Issues-list Normalize remains backward-compatible and must not be used for top offenders.
        Assert.Equal(DataQualityIssueTypes.MissingSupplier, DataQualityIssueTypes.Normalize(issueType));
    }

    [Fact]
    public void TopOffendersSql_SupportsMissingCostIndependentOfSupplierCase()
    {
        var sql = AnalyticsDataQualityHealthService.TopOffendersSql;

        Assert.Contains("is_missing_cost", sql, StringComparison.Ordinal);
        Assert.Contains("a.\"NabavnaCena\" IS NULL OR a.\"NabavnaCena\" <= 0", sql, StringComparison.Ordinal);
        Assert.Contains("@issueType = 'missingCost' AND is_missing_cost", sql, StringComparison.Ordinal);
        Assert.Contains("@issueType <> 'missingCost' AND issue_type = @issueType", sql, StringComparison.Ordinal);
    }
}
