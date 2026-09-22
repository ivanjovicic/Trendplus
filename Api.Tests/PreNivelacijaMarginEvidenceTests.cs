using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaMarginEvidenceTests
{
    [Theory]
    [InlineData(null, "all")]
    [InlineData("", "all")]
    [InlineData(" IMPORTED ", "imported")]
    [InlineData("existing", "existing")]
    [InlineData("unsupported", "all")]
    public void NormalizeDataScope_UsesCanonicalValues(string? rawScope, string expected)
    {
        Assert.Equal(expected, PreNivelacijaPriorityEndpoints.NormalizeDataScope(rawScope));
    }

    [Fact]
    public void ResolveMarginEvidence_NullPurchaseCost_IsIncomplete_NotHundredPercent()
    {
        var evidence = PreNivelacijaPriorityEndpoints.ResolveMarginEvidence(sellingPrice: 1000m, purchasePrice: null);

        Assert.False(evidence.HasCompleteEvidence);
        Assert.Equal("missing_purchase_cost", evidence.EvidenceReason);
        Assert.Null(evidence.GrossMarginPctEst);
        Assert.Equal(1000m, evidence.SellingPriceForScenarios);
        Assert.Equal(0m, evidence.PurchasePriceForScenarios);
    }

    [Fact]
    public void ResolveMarginEvidence_ZeroPurchaseCost_IsIncomplete_NotHundredPercent()
    {
        var evidence = PreNivelacijaPriorityEndpoints.ResolveMarginEvidence(sellingPrice: 1000m, purchasePrice: 0m);

        Assert.False(evidence.HasCompleteEvidence);
        Assert.Equal("non_positive_purchase_cost", evidence.EvidenceReason);
        Assert.Null(evidence.GrossMarginPctEst);
    }

    [Fact]
    public void ResolveMarginEvidence_NegativePurchaseCost_IsIncomplete()
    {
        var evidence = PreNivelacijaPriorityEndpoints.ResolveMarginEvidence(sellingPrice: 1000m, purchasePrice: -50m);

        Assert.False(evidence.HasCompleteEvidence);
        Assert.Equal("non_positive_purchase_cost", evidence.EvidenceReason);
        Assert.Null(evidence.GrossMarginPctEst);
    }

    [Fact]
    public void ResolveMarginEvidence_PositiveCost_ComputesMargin()
    {
        var evidence = PreNivelacijaPriorityEndpoints.ResolveMarginEvidence(sellingPrice: 1000m, purchasePrice: 400m);

        Assert.True(evidence.HasCompleteEvidence);
        Assert.Null(evidence.EvidenceReason);
        Assert.Equal(60m, evidence.GrossMarginPctEst);
        Assert.Equal(400m, evidence.PurchasePriceForScenarios);
    }

    [Fact]
    public void ResolveMarginEvidence_EqualSellAndCost_IsGenuineZeroMargin()
    {
        var evidence = PreNivelacijaPriorityEndpoints.ResolveMarginEvidence(sellingPrice: 500m, purchasePrice: 500m);

        Assert.True(evidence.HasCompleteEvidence);
        Assert.Equal(0m, evidence.GrossMarginPctEst);
        Assert.Null(evidence.EvidenceReason);
    }

    [Fact]
    public void ResolveMarginEvidence_MissingSellingPrice_IsIncomplete()
    {
        var evidence = PreNivelacijaPriorityEndpoints.ResolveMarginEvidence(sellingPrice: null, purchasePrice: 200m);

        Assert.False(evidence.HasCompleteEvidence);
        Assert.Equal("missing_selling_price", evidence.EvidenceReason);
        Assert.Null(evidence.GrossMarginPctEst);
    }

    [Theory]
    [InlineData(false, 0, 0, false, "no_sales_in_window", "no_sales_in_window")]
    [InlineData(true, 0, 0, false, "zero_net_sales", "zero_net_sales")]
    [InlineData(true, -2, -5, false, "non_positive_net_with_returns", "signed_sales_non_positive")]
    [InlineData(true, 8, -2, false, "signed_adjustment", "signed_sales_adjustment")]
    [InlineData(true, 8, 0, true, "positive_net_sales", null)]
    public void ResolveSalesEvidence_DistinguishesSignedAndMissingSignals(
        bool hasSalesRows,
        int signedUnits,
        int negativeUnits,
        bool expectedComplete,
        string expectedStatus,
        string? expectedReason)
    {
        var evidence = PreNivelacijaPriorityEndpoints.ResolveSalesEvidence(hasSalesRows, signedUnits, negativeUnits);

        Assert.Equal(expectedComplete, evidence.IsComplete);
        Assert.Equal(expectedStatus, evidence.Status);
        Assert.Equal(expectedReason, evidence.Reason);
    }

    [Fact]
    public void CalculateWeekOverWeekRiskDelta_ReturnsUnavailableForNonPositiveDenominator()
    {
        Assert.Null(PreNivelacijaPriorityEndpoints.CalculateWeekOverWeekRiskDelta(last7Units: 4, previous7Units: 0));
        Assert.Null(PreNivelacijaPriorityEndpoints.CalculateWeekOverWeekRiskDelta(last7Units: -2, previous7Units: -1));
        Assert.Equal(50m, PreNivelacijaPriorityEndpoints.CalculateWeekOverWeekRiskDelta(last7Units: 5, previous7Units: 10));
    }
}
