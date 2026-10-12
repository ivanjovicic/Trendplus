using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class StockProxyEvidenceClassificationTests
{
    [Theory]
    [InlineData(null, "missing_baseline")]
    [InlineData(-10.5, "negative_proxy")]
    [InlineData(0.0, "zero")]
    [InlineData(4.25, "positive")]
    public void ClassifyStockProxyEvidence_DistinguishesMissingNegativeZeroAndPositive(
        double? stockQty,
        string expected)
    {
        decimal? value = stockQty.HasValue ? Convert.ToDecimal(stockQty.Value) : null;
        Assert.Equal(expected, AnalyticsIntelligenceEndpoints.ClassifyStockProxyEvidence(value));
    }
}
