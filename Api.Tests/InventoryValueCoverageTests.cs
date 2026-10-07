using Application.Analytics;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public sealed class InventoryValueCoverageTests
{
    [Fact]
    public void Aggregate_MixedKnownAndUnknown_ReportsKnownSubtotalAndCoverage()
    {
        var result = InventoryValueCoverage.Aggregate([100m, null, 0m]);
        Assert.Equal(100m, result.Value);
        Assert.Equal(2, result.KnownRows);
        Assert.Equal(1, result.UnknownRows);
        Assert.Equal(66.7m, result.CoveragePct);
    }

    [Fact]
    public void Aggregate_AllUnknown_DoesNotManufactureZero()
    {
        var result = InventoryValueCoverage.Aggregate([null, null]);
        Assert.Null(result.Value);
        Assert.Equal(0, result.KnownRows);
        Assert.Equal(2, result.UnknownRows);
        Assert.Equal(0m, result.CoveragePct);
    }

    [Fact]
    public void Aggregate_PreservesMeasuredZero()
    {
        var result = InventoryValueCoverage.Aggregate([0m, null]);
        Assert.Equal(0m, result.Value);
        Assert.Equal(1, result.KnownRows);
        Assert.Equal(50m, result.CoveragePct);
    }

    [Fact]
    public void Abc_ExcludesUnknownValuesFromMonetaryClasses()
    {
        var classes = InventoryValueCoverage.ClassifyAbc([(100m, "A"), (null, "Unknown"), (10m, "B"), (0m, "Zero")]);
        Assert.Equal("B", classes[0]);
        Assert.Equal("N/A", classes[1]);
        Assert.Equal("C", classes[2]);
        Assert.Equal("C", classes[3]);
    }

    [Fact]
    public void Abc_AllUnknown_RemainsUnclassified()
    {
        Assert.Equal(new[] { "N/A", "N/A" }, InventoryValueCoverage.ClassifyAbc([(null, "A"), (null, "B")]));
    }
}
