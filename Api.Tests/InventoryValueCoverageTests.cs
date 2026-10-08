using Application.Analytics;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public sealed class InventoryValueCoverageTests
{
    [Fact]
    public void Aggregate_MixedKnownAndUnknown_ReportsKnownSubtotalAndCoverage()
    {
        var result = InventoryValueCoverage.Aggregate([(4, 100m), (2, null), (3, 0m)]);
        Assert.Equal(100m, result.Value);
        Assert.Equal(2, result.KnownRows);
        Assert.Equal(1, result.UnknownRows);
        Assert.Equal(66.7m, result.CoveragePct);
    }

    [Fact]
    public void Aggregate_AllUnknown_DoesNotManufactureZero()
    {
        var result = InventoryValueCoverage.Aggregate([(1, null), (5, null)]);
        Assert.Null(result.Value);
        Assert.Equal(0, result.KnownRows);
        Assert.Equal(2, result.UnknownRows);
        Assert.Equal(0m, result.CoveragePct);
    }

    [Fact]
    public void Aggregate_PreservesMeasuredZero()
    {
        var result = InventoryValueCoverage.Aggregate([(2, 0m), (1, null)]);
        Assert.Equal(0m, result.Value);
        Assert.Equal(1, result.KnownRows);
        Assert.Equal(50m, result.CoveragePct);
    }

    [Fact]
    public void Aggregate_ZeroAndNegativeStockRows_DoNotInflateCoverageOrSubtractValue()
    {
        // 3 sold-out rows (measured zero) must not make 1 known of 4 stocked rows look like 57% coverage.
        var result = InventoryValueCoverage.Aggregate([
            (0, 0m), (0, 0m), (0, 0m),
            (-2, -50m),
            (5, 250m), (3, null), (2, null), (1, null),
        ]);
        Assert.Equal(250m, result.Value);
        Assert.Equal(1, result.KnownRows);
        Assert.Equal(3, result.UnknownRows);
        Assert.Equal(25m, result.CoveragePct);
    }

    [Fact]
    public void Aggregate_NoStockedRows_IsMeasuredZeroCapital()
    {
        var result = InventoryValueCoverage.Aggregate([(0, 0m), (-1, null)]);
        Assert.Equal(0m, result.Value);
        Assert.Equal(0, result.UnknownRows);
        Assert.Equal(100m, result.CoveragePct);
    }

    [Fact]
    public void Abc_ExcludesUnknownValuesFromMonetaryClasses()
    {
        var classes = InventoryValueCoverage.ClassifyAbc([(100m, "A"), (null, "Unknown"), (10m, "B"), (0m, "Zero")]);
        Assert.Equal("A", classes[0]);
        Assert.Equal("N/A", classes[1]);
        Assert.Equal("B", classes[2]);
        Assert.Equal("C", classes[3]);
    }

    [Fact]
    public void Abc_LargestValueIsAlwaysClassA_EvenForSingleSku()
    {
        Assert.Equal(new[] { "A" }, InventoryValueCoverage.ClassifyAbc([(500m, "Only")]));
        var classes = InventoryValueCoverage.ClassifyAbc([(70m, "a"), (20m, "b"), (6m, "c"), (4m, "d"), (-5m, "negative")]);
        Assert.Equal(new[] { "A", "A", "B", "C", "C" }, classes);
    }

    [Fact]
    public void Abc_AllUnknown_RemainsUnclassified()
    {
        Assert.Equal(new[] { "N/A", "N/A" }, InventoryValueCoverage.ClassifyAbc([(null, "A"), (null, "B")]));
    }
}
