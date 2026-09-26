using Infrastructure.Services;
using Xunit;

namespace Api.Tests;

public sealed class OperationsAnalyticsIntegrityProbeWindowTests
{
    [Fact]
    public void Resolve_UsesInclusiveUtcDayBounds()
    {
        var checkedAt = new DateTime(2026, 7, 7, 15, 30, 0, DateTimeKind.Utc);
        var (fromUtc, toUtc) = OperationsAnalyticsIntegrityProbeWindow.Resolve(checkedAt, probeLookbackDays: 7);

        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), fromUtc);
        Assert.Equal(
            new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1),
            toUtc);
    }
}
