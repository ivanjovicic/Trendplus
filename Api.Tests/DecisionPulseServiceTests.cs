using Application.Analytics.DecisionPulse;
using Api.Services.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class DecisionPulseServiceTests
{
    [Fact]
    public void BuildResponseMeta_MarksEmptyProjectionAsPartialWhenSomeSourcesFailed()
    {
        var projection = new DecisionPulseProjection(
            true,
            null,
            null,
            Array.Empty<DecisionPulseItem>(),
            0,
            DecisionPulseProjector.DedicatedTenantScope);

        var meta = DecisionPulseService.BuildResponseMeta(
            projection,
            generatedAtUtc: DateTime.UtcNow,
            sourceFailures: ["supplier_source_unavailable"],
            sourceFailureMessages: ["Supplier decision hub nije dostupan."]);

        Assert.True(meta.Success);
        Assert.True(meta.IsPartial);
        Assert.Equal("no_pulse_items", meta.EmptyReason);
        Assert.Equal("PULSE_PARTIAL", meta.WarningCode);
        Assert.Equal("Supplier decision hub nije dostupan.", meta.WarningMessage);
        Assert.Contains("Nema Decision Pulse izuzetaka", meta.Message);
        Assert.Contains("Supplier decision hub nije dostupan.", meta.Message);
    }
}
