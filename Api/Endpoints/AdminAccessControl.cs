using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Trendplus2.Endpoints;

internal enum AdminAccessDecision
{
    MissingCredential,
    Forbidden,
    Authorized
}

internal static class AdminAccessControl
{
    public static AdminAccessDecision GetDecision(HttpContext context, IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return context.User.IsInRole("Admin")
                ? AdminAccessDecision.Authorized
                : AdminAccessDecision.Forbidden;
        }

        var providedKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return AdminAccessDecision.MissingCredential;
        }

        var configuredKey = configuration["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            configuredKey = Environment.GetEnvironmentVariable("ADMIN_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return AdminAccessDecision.Forbidden;
        }

        return string.Equals(providedKey, configuredKey, StringComparison.Ordinal)
            ? AdminAccessDecision.Authorized
            : AdminAccessDecision.Forbidden;
    }
}
