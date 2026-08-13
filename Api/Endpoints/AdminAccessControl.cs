using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Application.Documents.Interfaces;
using Application.Documents.Models;

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

    public static IResult? RejectIfUnauthorized(HttpContext context, IConfiguration configuration)
    {
        return GetDecision(context, configuration) switch
        {
            AdminAccessDecision.MissingCredential => Results.Unauthorized(),
            AdminAccessDecision.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            _ => null
        };
    }

    public static bool TryAuthorizeDocumentPrivilege(
        HttpContext httpContext,
        IConfiguration configuration,
        IDocumentUserContextAccessor accessor,
        out DocumentExecutionContext context,
        out IResult? rejected)
    {
        rejected = RejectIfUnauthorized(httpContext, configuration);
        if (rejected is not null)
        {
            context = null!;
            return false;
        }

        context = accessor.GetCurrent();
        if (!context.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            context.Roles = [.. context.Roles, "Admin"];
        }

        return true;
    }
}
