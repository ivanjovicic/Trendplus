using Api.Services;
using Xunit;

namespace Trendplus2.Tests;

public sealed class SalesReceiptPopulationPolicyTests
{
    [Theory]
    [InlineData("DUG", true)]
    [InlineData(" dug ", true)]
    [InlineData("DuG", true)]
    [InlineData(" K o r e k c i j a ", false)]
    [InlineData("KOREKCIJA", true)]
    [InlineData(" korekcija ", true)]
    [InlineData("KoReKcIjA", true)]
    public void IsExcluded_RecognizesTrimmedCaseInsensitiveNonStandardDocuments(string receiptNumber, bool expected)
    {
        Assert.Equal(expected, SalesReceiptPopulationPolicy.IsExcluded(receiptNumber));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("DUG-1")]
    public void IsExcluded_KeepsRetailReceiptsAndMissingNumbers(string? receiptNumber)
    {
        Assert.False(SalesReceiptPopulationPolicy.IsExcluded(receiptNumber));
    }
}
