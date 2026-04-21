using Api.Config;
using Api.Services.Access;
using Api.Services.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

public sealed class WorkerRuntimeConfigTests
{
    [Fact]
    public void ResolveProcessType_ReturnsWorker_WhenProcessTypeIsWorker()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PROCESS_TYPE"] = "worker"
        });

        var processType = WorkerRuntimeConfig.ResolveProcessType(configuration, out var source);

        Assert.Equal(ProcessType.Worker, processType);
        Assert.Equal("PROCESS_TYPE", source);
    }

    [Fact]
    public void ResolveProcessType_UsesAlias_WhenProcessTypeMissing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["WORKER_PROCESS"] = "true"
        });

        var processType = WorkerRuntimeConfig.ResolveProcessType(configuration, out var source);

        Assert.Equal(ProcessType.Worker, processType);
        Assert.Equal("WORKER_PROCESS", source);
    }

    [Fact]
    public void ResolveProcessType_DefaultsToWeb_WhenNothingIsConfigured()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        var processType = WorkerRuntimeConfig.ResolveProcessType(configuration, out var source);

        Assert.Equal(ProcessType.Web, processType);
        Assert.Equal("DEFAULT_WEB", source);
    }

    [Fact]
    public void RegisterWorkerHostedServices_DoesNotRegisterWorkers_InWebProcess()
    {
        var services = new ServiceCollection();

        WorkerRuntimeConfig.RegisterWorkerHostedServices(services, isWorkerProcess: false);

        var hostedServiceDescriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        Assert.Empty(hostedServiceDescriptors);
    }

    [Fact]
    public void RegisterWorkerHostedServices_RegistersCriticalWorkers_InWorkerProcess()
    {
        var services = new ServiceCollection();

        WorkerRuntimeConfig.RegisterWorkerHostedServices(services, isWorkerProcess: true);

        var hostedServiceTypes = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .Where(t => t is not null)
            .Cast<Type>()
            .ToList();

        Assert.Contains(typeof(Workers.SyncWorker), hostedServiceTypes);
        Assert.Contains(typeof(AccessImportBackgroundWorker), hostedServiceTypes);
        Assert.Contains(typeof(DeferredStartupTasksHostedService), hostedServiceTypes);
    }

    [Fact]
    public void ResolveWorkersEnabled_DefaultsToTrue_ForWorkerProcess()
    {
        var enabled = WorkerRuntimeConfig.ResolveWorkersEnabled(
            configuredWorkersEnabled: null,
            processType: ProcessType.Worker,
            isDevelopment: false);

        Assert.True(enabled);
    }

    [Fact]
    public void ResolveWorkersEnabled_RespectsExplicitFalseSafetyToggle()
    {
        var enabled = WorkerRuntimeConfig.ResolveWorkersEnabled(
            configuredWorkersEnabled: false,
            processType: ProcessType.Worker,
            isDevelopment: false);

        Assert.False(enabled);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
