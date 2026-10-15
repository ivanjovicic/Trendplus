using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

/// <summary>
/// Invalidates analytics cache families after an operational write has committed (POS sale, return,
/// article master data, cost snapshot activation). Best effort: a cache failure never fails the write,
/// and a client abort after the commit does not skip the invalidation.
/// </summary>
public static class AnalyticsCacheMutationInvalidation
{
    public static async Task InvalidateAfterCommitAsync(
        IServiceProvider services,
        string[] families,
        string trigger,
        ILogger logger)
    {
        var cacheAdmin = services.GetService<AnalyticsCacheAdminService>();
        if (cacheAdmin is null)
        {
            return;
        }

        try
        {
            // No integrity probe per write: probes stay owned by import/refresh workers.
            await cacheAdmin.ClearFamiliesAsync(families, CancellationToken.None, scheduleIntegrityProbe: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics cache invalidation after {Trigger} failed; cached screens expire by TTL.", trigger);
        }
    }
}
