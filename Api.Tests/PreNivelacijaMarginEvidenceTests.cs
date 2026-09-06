using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaMarginEvidenceTests
{
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
}
