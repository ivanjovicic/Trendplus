using Api.Services.Startup;
using Infrastructure.Seed;
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

    [Fact]
    public void DatabaseInitialization_NotRequiredIsDistinctFromSucceeded()
    {
        var state = new StartupReadinessState();

        state.MarkDatabaseInitializationNotRequired();

        Assert.Equal("not_required", state.DatabaseInitialization.State);
        Assert.False(state.DatabaseInitialization.Required);
        Assert.Equal(0, state.DatabaseInitialization.Attempts);
        Assert.Null(state.DatabaseInitialization.LastAttemptAtUtc);
        Assert.NotNull(state.DatabaseInitialization.CompletedAtUtc);
    }

    [Fact]
    public void DatabaseInitialization_TracksPendingAttemptsAndCleanSuccess()
    {
        var state = new StartupReadinessState();
        state.RequireDatabaseInitialization();
        state.BeginDatabaseInitializationAttempt();

        Assert.Equal("pending", state.DatabaseInitialization.State);
        Assert.True(state.DatabaseInitialization.Required);
        Assert.Equal(1, state.DatabaseInitialization.Attempts);
        Assert.NotNull(state.DatabaseInitialization.LastAttemptAtUtc);
        Assert.Null(state.DatabaseInitialization.CompletedAtUtc);

        state.ReportProbe(new StartupReadinessState.DatabaseProbeState { Ok = true }, new StartupReadinessState.DatabaseProbeState { Ok = true });
        state.MarkDatabaseInitializationCompleted(DatabaseInitializationOutcome.Succeeded);

        Assert.Equal("succeeded", state.DatabaseInitialization.State);
        Assert.True(state.IsReady);
        Assert.Equal("ready", state.Reason);
        Assert.NotNull(state.DatabaseInitialization.CompletedAtUtc);
    }

    [Fact]
    public void DatabaseInitialization_NonStrictErrorsKeepAvailabilityButMarkDegraded()
    {
        var state = new StartupReadinessState();
        state.RequireDatabaseInitialization();
        state.BeginDatabaseInitializationAttempt();
        state.ReportProbe(new StartupReadinessState.DatabaseProbeState { Ok = true }, new StartupReadinessState.DatabaseProbeState { Ok = true });

        state.MarkDatabaseInitializationCompleted(
            DatabaseInitializationOutcome.WithErrors("schema_verification_failed", "analytics_database"));

        Assert.Equal("completed_with_errors", state.DatabaseInitialization.State);
        Assert.Equal("schema_verification_failed", state.DatabaseInitialization.FailureCategory);
        Assert.Equal("analytics_database", state.DatabaseInitialization.FailureStage);
        Assert.True(state.IsReady);
        Assert.Equal("ready_degraded_schema", state.Reason);
    }

    [Fact]
    public void DatabaseInitialization_FailureRemainsNotReady()
    {
        var state = new StartupReadinessState();
        state.RequireDatabaseInitialization();
        state.BeginDatabaseInitializationAttempt();
        state.RecordDatabaseInitializationFailure("database_unavailable", "default_connection");
        state.MarkDatabaseInitializationFailed();

        Assert.Equal("failed", state.DatabaseInitialization.State);
        Assert.Equal("database_unavailable", state.DatabaseInitialization.FailureCategory);
        Assert.Equal("default_connection", state.DatabaseInitialization.FailureStage);
        Assert.False(state.IsReady);
        Assert.Equal("database_initialization_failed", state.Reason);
    }
}
