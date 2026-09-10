using Infrastructure.Services;
using Xunit;

namespace Api.Tests;

public sealed class WorkerHealthServiceDeadLetterTests
{
    [Fact]
    public void ReportHealthy_StoresTrueZeroAndNonZeroDeadLetterCounts()
    {
        var service = new WorkerHealthService();

        service.ReportHealthy("OutboxProcessorWorker", "observed", deadLetterCount: 3);
        var first = Assert.Single(service.GetHealthSummary().Workers);
        Assert.Equal(3, first.DeadLetterCount);

        service.ReportHealthy("OutboxProcessorWorker", "observed", deadLetterCount: 0);
        var second = Assert.Single(service.GetHealthSummary().Workers);
        Assert.Equal(0, second.DeadLetterCount);
    }

    [Fact]
    public void ReportError_ClearsDeadLetterObservationInsteadOfKeepingStaleEvidence()
    {
        var service = new WorkerHealthService();
        service.ReportHealthy("OutboxProcessorWorker", "observed", deadLetterCount: 3);

        service.ReportError("OutboxProcessorWorker", new InvalidOperationException("observation failed"));

        var worker = Assert.Single(service.GetHealthSummary().Workers);
        Assert.Null(worker.DeadLetterCount);
    }
}
