using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class LostSalesValidationSourceStatusTests
{
    [Fact]
    public void BuildLostSalesValidation_Unavailable_IsNotGood_AndEstimateIsNull()
    {
        var dto = CachedAnalyticsEndpoints.BuildLostSalesValidationFromSnapshot(
            LostSalesSnapshot.Unavailable());

        Assert.Equal("insufficient_data", dto.Status);
        Assert.Equal(LostSalesSourceStatus.Unavailable, dto.SourceStatus);
        Assert.Null(dto.LostSalesEstimate);
        Assert.Null(dto.AffectedSku);
        Assert.DoesNotContain("nema znacajnog", dto.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildLostSalesValidation_TrueViewZero_IsGood()
    {
        var dto = CachedAnalyticsEndpoints.BuildLostSalesValidationFromSnapshot(
            LostSalesSnapshot.TrueZero(oosSkuCount: 0));

        Assert.Equal("good", dto.Status);
        Assert.Equal(LostSalesSourceStatus.TrueZero, dto.SourceStatus);
        Assert.Equal(0m, dto.LostSalesEstimate);
        Assert.Equal(0, dto.AffectedSku);
    }

    [Fact]
    public void BuildLostSalesValidation_FallbackZero_IsWarning_NotGood()
    {
        var dto = CachedAnalyticsEndpoints.BuildLostSalesValidationFromSnapshot(
            LostSalesSnapshot.FromFallback(oosSkuCount: 0, lostSalesEstimate: 0m));

        Assert.Equal("warning", dto.Status);
        Assert.Equal(LostSalesSourceStatus.Fallback, dto.SourceStatus);
        Assert.Equal(0m, dto.LostSalesEstimate);
        Assert.NotEqual("good", dto.Status);
    }

    [Theory]
    [InlineData(12_500.0, "warning")]
    [InlineData(75_000.0, "critical")]
    public void BuildLostSalesValidation_FallbackPositive_UsesAmountThresholds(
        double estimate,
        string expectedStatus)
    {
        var dto = CachedAnalyticsEndpoints.BuildLostSalesValidationFromSnapshot(
            LostSalesSnapshot.FromFallback(oosSkuCount: 3, lostSalesEstimate: (decimal)estimate));

        Assert.Equal(expectedStatus, dto.Status);
        Assert.Equal(LostSalesSourceStatus.Fallback, dto.SourceStatus);
        Assert.Equal((decimal)estimate, dto.LostSalesEstimate);
        Assert.Equal(3, dto.AffectedSku);
    }

    [Theory]
    [InlineData(8_000.0, "warning")]
    [InlineData(90_000.0, "critical")]
    public void BuildLostSalesValidation_ViewPositive_UsesAmountThresholds(
        double estimate,
        string expectedStatus)
    {
        var dto = CachedAnalyticsEndpoints.BuildLostSalesValidationFromSnapshot(
            LostSalesSnapshot.FromView(oosSkuCount: 5, lostSalesEstimate: (decimal)estimate));

        Assert.Equal(expectedStatus, dto.Status);
        Assert.Equal(LostSalesSourceStatus.View, dto.SourceStatus);
        Assert.Equal((decimal)estimate, dto.LostSalesEstimate);
    }
}
