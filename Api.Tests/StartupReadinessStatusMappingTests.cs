using Api.Services.Startup;
using Xunit;

namespace Api.Tests;

public sealed class StartupReadinessStatusMappingTests
{
    [Theory]
    [InlineData("failed", "schema_contract_incompatible", "schema_incompatible", "failed")]
    [InlineData("pending", "schema_contract_incompatible", "schema_incompatible", "failed")]
    [InlineData("not_required", null, "schema_incompatible", "failed")]
    [InlineData("failed", "schema_verification_failed", "schema_verification_failed", "failed")]
    [InlineData("pending", null, "db_warmup", "warming_up")]
    [InlineData("pending", null, "starting", "warming_up")]
    [InlineData("pending", null, "database_initialization", "degraded")]
    public void ResolvePublicStatus_MapsSchemaFailuresToFailed(
        string state,
        string? failureCategory,
        string reason,
        string expectedStatus)
    {
        var snapshot = new StartupReadinessState.DatabaseInitializationSnapshot(
            State: state,
            Required: true,
            FailureCategory: failureCategory,
            FailureStage: failureCategory is null ? null : "purchase_cost_provenance",
            Attempts: 1,
            LastAttemptAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: null);

        var status = StartupReadinessEndpoints.ResolvePublicStatus(
            isReady: false,
            isDegradedSuccess: false,
            initialization: snapshot,
            reason: reason);

        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public void ResolvePublicStatus_ReadyHealthyByDefault()
    {
        var snapshot = new StartupReadinessState.DatabaseInitializationSnapshot(
            "succeeded", false, null, null, 0, null, DateTimeOffset.UtcNow);

        Assert.Equal(
            "healthy",
            StartupReadinessEndpoints.ResolvePublicStatus(true, false, snapshot, "ready"));
    }
}
