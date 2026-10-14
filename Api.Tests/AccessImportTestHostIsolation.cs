using Api.Services.Access;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Tests;

internal static class AccessImportTestHostIsolation
{
    /// <summary>
    /// Drops the web-process Access import worker from a non-import test host. appsettings.json sets
    /// AccessImport:RegisterWorkerInWebProcess=true and outranks builder.UseSetting, so the registered
    /// descriptor has to be removed after Program has added it.
    /// </summary>
    public static void RemoveAccessImportWorker(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(IsAccessImportWorker).ToList())
        {
            services.Remove(descriptor);
        }
    }

    public static bool IsRegistered(IServiceProvider services) =>
        services.GetServices<IHostedService>().Any(service => service is AccessImportBackgroundWorker);

    private static bool IsAccessImportWorker(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && descriptor.ImplementationType == typeof(AccessImportBackgroundWorker);
}
