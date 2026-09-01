using Microsoft.Extensions.Hosting;

namespace Infrastructure.Services;

public static class OperationalLogPersistencePolicy
{
    // Keep non-production databases small by skipping durable operational writes.
    public static bool ShouldPersist(IHostEnvironment? environment)
    {
        return environment?.IsProduction() == true;
    }
}
