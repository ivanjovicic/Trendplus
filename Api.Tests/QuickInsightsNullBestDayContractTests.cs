using System.Text.Json;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class QuickInsightsNullBestDayContractTests
{
    [Fact]
    public void BestDayRevenue_SerializesNull_WhenBestDayMissing()
    {
        var dto = new QuickInsightsDto
        {
            BestDay = null,
            BestDayRevenue = null,
            TopProduct = null,
            LowStockAlert = 0,
        };

        var json = JsonSerializer.Serialize(dto);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("BestDayRevenue").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("BestDay").ValueKind);
    }
}
