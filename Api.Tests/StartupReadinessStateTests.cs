using Api.Services.Startup;
using Xunit;

namespace Api.Tests;

public sealed class StartupReadinessStateTests
{
    [Fact]
    public void ReportProbe_StoresLatestDbProbeState()
    {
        var state = new StartupReadinessState();

        state.ReportProbe(
            new StartupReadinessState.DatabaseProbeState
            {
                Ok = true,
                LatencyMs = 123,
                Error = null
            },
            new StartupReadinessState.DatabaseProbeState
            {
                Ok = false,
                LatencyMs = 456,
                Error = "timeout"
            });

        Assert.True(state.DefaultDb.Ok);
        Assert.Equal(123L, state.DefaultDb.LatencyMs);
        Assert.False(state.AnalyticsDb.Ok);
        Assert.Equal(456L, state.AnalyticsDb.LatencyMs);
        Assert.Equal("timeout", state.AnalyticsDb.Error);
        Assert.NotNull(state.LastProbeAtUtc);
    }

    [Fact]
    public void FreshState_LeavesProbeLatencyUnknown()
    {
        var state = new StartupReadinessState();

        Assert.Null(state.DefaultDb.LatencyMs);
        Assert.Null(state.AnalyticsDb.LatencyMs);
    }

    [Fact]
    public async Task MissingConnectionStringProbe_StaysUnknownAndFailsClosed()
    {
        var result = await DbConnectionHelper.TryProbeConnectionStringAsync(
            "default",
            connectionString: null,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Null(result.ElapsedMs);
        Assert.Equal(DependencyHealthPublicErrors.MissingConnectionString, result.Error);
    }

    [Fact]
    public void MarkReady_And_MarkNotReady_UpdateFlagsAndReason()
    {
        var state = new StartupReadinessState();

        state.MarkReady();
        Assert.True(state.IsReady);
        Assert.Equal("ready", state.Reason);
        Assert.NotNull(state.ReadyAtUtc);

        state.MarkNotReady("db_warmup_failed");
        Assert.False(state.IsReady);
        Assert.Equal("db_warmup_failed", state.Reason);
        Assert.Null(state.ReadyAtUtc);
    }
}
