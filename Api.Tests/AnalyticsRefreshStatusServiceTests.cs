using Api.Services;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsRefreshStatusServiceTests
{
    [Fact]
    public void GetStatus_ReturnsUnknown_WhenNoRefreshHistoryExists()
    {
        var service = CreateService(
            configValues: new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = "worker",
                ["Workers:Enabled"] = "true"
            },
            healthService: new WorkerHealthService(),
            runtimeControlService: new WorkerRuntimeControlService(
                initialEnabled: true,
                runtimeToggleAllowed: true,
                initialSource: "test"));

        var status = service.GetStatus();

        Assert.Equal("unknown", status.DataFreshnessStatus);
        Assert.All(status.Jobs, job => Assert.Equal("unknown", job.DataFreshnessStatus));
        Assert.Null(status.WorkerProcessWarning);
    }

    [Fact]
    public void GetStatus_ReturnsStale_WhenLastSuccessfulRefreshIsOlderThanFreshThreshold()
    {
        var health = new WorkerHealthService();
        var staleSuccessUtc = DateTime.UtcNow.AddHours(-40);
        var successLabel = staleSuccessUtc.ToString("yyyy-MM-dd HH:mm:ss'Z'");
        health.ReportHealthy(
            "NightlyAnalyticsRefreshWorker",
            $"Idle. Next run (UTC): 2099-01-01 01:00:00Z | Last success: {successLabel}");

        var service = CreateService(
            configValues: new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = "worker",
                ["Workers:Enabled"] = "true"
            },
            healthService: health,
            runtimeControlService: new WorkerRuntimeControlService(
                initialEnabled: true,
                runtimeToggleAllowed: true,
                initialSource: "test"));

        var status = service.GetStatus();

        Assert.Equal("stale", status.DataFreshnessStatus);
        Assert.Contains(status.Jobs, job => job.Key == "sales_facts_refresh" && job.DataFreshnessStatus == "stale");
    }

    [Fact]
    public void GetStatus_ReturnsProcessWarning_WhenWorkersEnabledInWebProcess()
    {
        var service = CreateService(
            configValues: new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = "web",
                ["Workers:Enabled"] = "true"
            },
            healthService: new WorkerHealthService(),
            runtimeControlService: new WorkerRuntimeControlService(
                initialEnabled: true,
                runtimeToggleAllowed: true,
                initialSource: "test"));

        var status = service.GetStatus();
        var dataQualityJob = Assert.Single(status.Jobs, job => job.Key == "data_quality_snapshot");

        Assert.Equal("web", status.ProcessType);
        Assert.False(string.IsNullOrWhiteSpace(status.WorkerProcessWarning));
        Assert.Equal("unknown", dataQualityJob.DataFreshnessStatus);
        Assert.Contains("web procesu", dataQualityJob.StatusReason ?? string.Empty);
    }

    private static AnalyticsRefreshStatusService CreateService(
        Dictionary<string, string?> configValues,
        WorkerHealthService healthService,
        WorkerRuntimeControlService runtimeControlService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new AnalyticsRefreshStatusService(
            configuration,
            new TestHostEnvironment(),
            healthService,
            runtimeControlService);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
