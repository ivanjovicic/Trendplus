using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsRefreshRunRecorderTests
{
    [Fact]
    public async Task MarkFailed_TrimLongErrorFields()
    {
        await using var db = CreateAnalyticsDbContext();
        var recorder = CreateRecorder(db);

        var runId = await recorder.StartRunAsync(
            "nightly_analytics_refresh",
            "Nightly analytics refresh",
            "nightly",
            "worker",
            "NightlyAnalyticsRefreshWorker",
            new string('c', 150),
            CancellationToken.None);

        Assert.True(runId.HasValue);

        await recorder.MarkFailedAsync(
            runId,
            new string('e', 200),
            new string('m', 5000),
            ["mv_supplier_decision_score_cache_90d"],
            new string('x', 160),
            CancellationToken.None);

        var run = await db.AnalyticsRefreshRuns.AsNoTracking().SingleAsync(x => x.Id == runId!.Value);
        Assert.Equal("failed", run.Status);
        Assert.NotNull(run.ErrorCode);
        Assert.NotNull(run.ErrorMessage);
        Assert.NotNull(run.CorrelationId);
        Assert.True(run.ErrorCode!.Length <= 120);
        Assert.True(run.ErrorMessage!.Length <= 2000);
        Assert.True(run.CorrelationId!.Length <= 100);
        Assert.EndsWith("...", run.ErrorCode);
        Assert.EndsWith("...", run.ErrorMessage);
    }

    [Fact]
    public async Task StartRun_DoesNotThrow_WhenPersistenceFails()
    {
        var context = CreateAnalyticsDbContext();
        var recorder = CreateRecorder(context);
        await context.DisposeAsync();

        var runId = await recorder.StartRunAsync(
            "nightly_analytics_refresh",
            "Nightly analytics refresh",
            "nightly",
            "worker",
            "NightlyAnalyticsRefreshWorker",
            null,
            CancellationToken.None);

        Assert.Null(runId);
    }

    private static AnalyticsDbContext CreateAnalyticsDbContext()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AnalyticsDbContext(options);
    }

    private static AnalyticsRefreshRunRecorder CreateRecorder(AnalyticsDbContext dbContext)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Analytics:RefreshHistory:Retention:MaxRuns"] = "500",
                ["Analytics:RefreshHistory:Retention:MaxAgeDays"] = "30"
            })
            .Build();

        return new AnalyticsRefreshRunRecorder(
            dbContext,
            config,
            NullLogger<AnalyticsRefreshRunRecorder>.Instance);
    }
}
