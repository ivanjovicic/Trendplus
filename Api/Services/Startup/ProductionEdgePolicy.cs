using Microsoft.Extensions.Hosting;

namespace Api.Services.Startup;

/// <summary>
/// Environment rules for reverse-proxy / HTTPS edge middleware.
/// </summary>
public static class ProductionEdgePolicy
{
    /// <summary>
    /// HSTS belongs on non-Development hosts (typically behind TLS-terminating proxy).
    /// Development stays HTTP-friendly without Strict-Transport-Security.
    /// </summary>
    public static bool ShouldUseHsts(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return !environment.IsDevelopment();
    }
}
