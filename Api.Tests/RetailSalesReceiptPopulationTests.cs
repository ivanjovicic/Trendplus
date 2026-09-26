using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class RetailSalesReceiptPopulationTests
{
    [Theory]
    [InlineData("DUG")]
    [InlineData("dug")]
    [InlineData(" Dug ")]
    [InlineData("KOREKCIJA")]
    [InlineData("korekcija")]
    [InlineData(" KoReKcIjA ")]
    public void IsExcludedFromRetailSales_MatchesTrimmedCaseInsensitiveCanonicals(string receipt)
    {
        Assert.True(RetailSalesReceiptPopulation.IsExcludedFromRetailSales(receipt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("DUG-1")]
    [InlineData("KOREKCIJAX")]
    [InlineData("ABC")]
    public void IsExcludedFromRetailSales_AllowsStandardAndUnrelatedReceipts(string? receipt)
    {
        Assert.False(RetailSalesReceiptPopulation.IsExcludedFromRetailSales(receipt));
    }

    [Fact]
    public void DebtAndCorrectionHelpers_AreDistinct()
    {
        Assert.True(RetailSalesReceiptPopulation.IsDebtReceiptNumber(" dug "));
        Assert.False(RetailSalesReceiptPopulation.IsDebtReceiptNumber("KOREKCIJA"));
        Assert.True(RetailSalesReceiptPopulation.IsCorrectionReceiptNumber("korekcija"));
        Assert.False(RetailSalesReceiptPopulation.IsCorrectionReceiptNumber("DUG"));
    }

    [Fact]
    public void SqlExclusionPredicate_TargetsCanonicalUpperForms()
    {
        Assert.Contains("DUG", RetailSalesReceiptPopulation.SqlExclusionPredicate, StringComparison.Ordinal);
        Assert.Contains("KOREKCIJA", RetailSalesReceiptPopulation.SqlExclusionPredicate, StringComparison.Ordinal);
        Assert.Contains("broj_racuna", RetailSalesReceiptPopulation.SqlExclusionPredicate, StringComparison.Ordinal);
    }
}
