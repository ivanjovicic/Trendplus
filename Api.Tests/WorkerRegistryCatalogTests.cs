using Api.Services.Access;
using Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

public sealed class WorkerRegistryCatalogTests
{
    [Fact]
    public void Definitions_HaveUniqueWorkerNames()
    {
        var workerNames = WorkerRegistryCatalog.Definitions
            .Select(x => x.WorkerName)
            .ToList();

        var uniqueNames = workerNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(workerNames.Count, uniqueNames.Count);
    }

    [Fact]
    public void Definitions_CoverAllHostedServiceTypes_InApiAndWorkersAssemblies()
    {
        var assemblies = new[]
        {
            typeof(AccessImportBackgroundWorker).Assembly,
            typeof(Workers.SyncWorker).Assembly
        };

        var discoveredHostedServiceNames = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
                typeof(IHostedService).IsAssignableFrom(t) &&
                (t.Namespace?.StartsWith("Workers", StringComparison.Ordinal) == true
                 || t.Namespace?.StartsWith("Api.Services", StringComparison.Ordinal) == true))
            .Select(t => t.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var catalogNames = WorkerRegistryCatalog.Definitions
            .Select(x => x.WorkerName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingFromCatalog = discoveredHostedServiceNames
            .Where(name => !catalogNames.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Empty(missingFromCatalog);
    }
}

