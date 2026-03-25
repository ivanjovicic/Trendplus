using Infrastructure.Analytics;
using Xunit;

namespace Api.Tests;

public class ReturnFactStatusMapperTests
{
    [Theory]
    [InlineData(null, ReturnFactStatusMapper.Pending)]
    [InlineData("", ReturnFactStatusMapper.Pending)]
    [InlineData("Kreiran", ReturnFactStatusMapper.Pending)]
    [InlineData("Poslat", ReturnFactStatusMapper.Pending)]
    [InlineData("pending", ReturnFactStatusMapper.Pending)]
    [InlineData("Prihvacen", ReturnFactStatusMapper.Approved)]
    [InlineData("Prihvaćen", ReturnFactStatusMapper.Approved)]
    [InlineData("Odobren", ReturnFactStatusMapper.Approved)]
    [InlineData("Approved", ReturnFactStatusMapper.Approved)]
    [InlineData("Odbijen", ReturnFactStatusMapper.Rejected)]
    [InlineData("Rejected", ReturnFactStatusMapper.Rejected)]
    [InlineData("Storniran", ReturnFactStatusMapper.Rejected)]
    [InlineData("nepoznato-stanje", ReturnFactStatusMapper.Pending)]
    public void Normalize_MapsOperationalStatusesToConstraintValues(string? source, string expected)
    {
        var actual = ReturnFactStatusMapper.Normalize(source);

        Assert.Equal(expected, actual);
    }
}
