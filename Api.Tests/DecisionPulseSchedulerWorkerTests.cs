using Workers;
using Xunit;

namespace Api.Tests;

public sealed class DecisionPulseSchedulerWorkerTests
{
    [Fact]
    public void ResolveTimeZone_UnknownIdFallsBackToLocalZone()
    {
        var zone = DecisionPulseSchedulerWorker.ResolveTimeZone("not-a-real-timezone");

        Assert.NotNull(zone);
        Assert.NotEqual(string.Empty, zone.Id);
    }
}
