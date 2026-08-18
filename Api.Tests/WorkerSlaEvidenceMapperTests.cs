using Infrastructure.Services;
using Xunit;

namespace Api.Tests;

public sealed class WorkerSlaEvidenceMapperTests
{
    [Fact]
    public void Capture_EmptyInventory_IsUnknown_NotHealthyZeroQueue()
    {
        var snapshot = WorkerSlaEvidenceMapper.Capture(
            new WorkerHealthSummary(),
            workersGloballyEnabled: true,
            utcNow: new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal("OBS08", snapshot.ContractId);
        Assert.Equal("unknown", snapshot.InventoryStatus);
        Assert.Equal("unknown", snapshot.DataQualityStatus);
        Assert.Empty(snapshot.Workers);
        Assert.Contains(WorkerSlaEvidenceMapper.InventoryMissingCode, snapshot.WarningCodes);
        Assert.Contains(WorkerSlaEvidenceMapper.LastSuccessUnknownCode, snapshot.WarningCodes);
    }

    [Fact]
    public void Capture_HeartbeatWithoutQueueOrLastSuccess_KeepsUnknownNotZero()
    {
        var now = new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);
        var summary = new WorkerHealthSummary
        {
            TotalWorkers = 1,
            HealthyWorkers = 1,
            Workers =
            [
                new WorkerStatusDto
                {
                    WorkerName = "AnalyticsAggregationWorker",
                    Status = nameof(WorkerStatusType.Healthy),
                    LastHeartbeat = now.AddMinutes(-1),
                    LastError = null,
                    ErrorCount = 0,
                    IsStale = false
                }
            ]
        };

        var snapshot = WorkerSlaEvidenceMapper.Capture(summary, workersGloballyEnabled: true, utcNow: now);
        var worker = Assert.Single(snapshot.Workers);

        Assert.Equal("partial", snapshot.DataQualityStatus);
        Assert.Equal("enabled", worker.ExecutionState);
        Assert.Null(worker.QueueDepth);
        Assert.Null(worker.OldestWorkAgeSeconds);
        Assert.Null(worker.LastSuccessfulRunAtUtc);
        Assert.Null(worker.LastSuccessfulRunAgeSeconds);
        Assert.Null(worker.SuccessCount);
        Assert.Null(worker.FailureCount);
        Assert.Null(worker.RetryCount);
        Assert.Null(worker.DeadLetterCount);
        Assert.False(worker.LastErrorPresent);
        Assert.Equal(60, worker.HeartbeatAgeSeconds);
        Assert.Contains(WorkerSlaEvidenceMapper.QueueDepthUnknownCode, worker.WarningCodes);
        Assert.Contains(WorkerSlaEvidenceMapper.LastSuccessUnknownCode, worker.WarningCodes);
        Assert.Equal("partial", worker.DataQualityStatus);
    }

    [Fact]
    public void Capture_GlobalSwitchOff_IsPaused_NotHealthySilence()
    {
        var now = new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);
        var summary = new WorkerHealthSummary
        {
            Workers =
            [
                new WorkerStatusDto
                {
                    WorkerName = "OutboxProcessorWorker",
                    Status = nameof(WorkerStatusType.Healthy),
                    LastHeartbeat = now,
                    IsStale = false
                }
            ]
        };

        var snapshot = WorkerSlaEvidenceMapper.Capture(summary, workersGloballyEnabled: false, utcNow: now);
        var worker = Assert.Single(snapshot.Workers);

        Assert.False(snapshot.WorkersGloballyEnabled);
        Assert.Equal("unknown", snapshot.DataQualityStatus);
        Assert.Equal("paused", worker.ExecutionState);
        Assert.Equal("Global workers switch is off.", worker.PauseReason);
        Assert.Contains(WorkerSlaEvidenceMapper.GlobalPausedCode, snapshot.WarningCodes);
        Assert.Null(worker.LastSuccessfulRunAgeSeconds);
        Assert.Null(worker.QueueDepth);
    }

    [Fact]
    public void Capture_StaleHeartbeat_IsUnknown_EmptyErrorIsNotHealthy()
    {
        var now = new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);
        var summary = new WorkerHealthSummary
        {
            Workers =
            [
                new WorkerStatusDto
                {
                    WorkerName = "SyncWorker",
                    Status = nameof(WorkerStatusType.Healthy),
                    LastHeartbeat = now.AddHours(-2),
                    LastError = null,
                    ErrorCount = 0,
                    IsStale = true
                }
            ]
        };

        var worker = Assert.Single(WorkerSlaEvidenceMapper.Capture(summary, true, now).Workers);

        Assert.Equal("unknown", worker.ExecutionState);
        Assert.False(worker.LastErrorPresent);
        Assert.Null(worker.LastSuccessfulRunAgeSeconds);
        Assert.NotEqual("enabled", worker.DataQualityStatus);
    }
}
