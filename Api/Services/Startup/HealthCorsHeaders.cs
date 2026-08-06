using Microsoft.AspNetCore.Http;

namespace Api.Services.Startup;

public static class HealthCorsHeaders
{
    public static void Apply(HttpContext context, ISet<string> allowedOrigins)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(allowedOrigins);

        if (!context.Request.Headers.TryGetValue("Origin", out var originValues))
        {
            return;
        }

        var origin = originValues.ToString();
        if (string.IsNullOrWhiteSpace(origin) || !allowedOrigins.Contains(origin))
        {
            return;
        }

        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Vary"] = "Origin";
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
    }
}
