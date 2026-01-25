using Microsoft.AspNetCore.RateLimiting;

namespace Trendplus2.Extensions;

/// <summary>
/// Extension methods for applying rate limiting policies to endpoints.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Apply rate limiting for DB-heavy operations (max 5 concurrent).
    /// Use for: complex JOINs, aggregations, large data fetches.
    /// </summary>
    public static RouteHandlerBuilder RequireDbHeavyRateLimit(this RouteHandlerBuilder builder)
        => builder.RequireRateLimiting("db-heavy");

    /// <summary>
    /// Apply rate limiting for analytics endpoints (200/min sliding window).
    /// Use for: dashboards, reports, charts.
    /// </summary>
    public static RouteHandlerBuilder RequireAnalyticsRateLimit(this RouteHandlerBuilder builder)
        => builder.RequireRateLimiting("analytics");

    /// <summary>
    /// Apply rate limiting for write operations (token bucket).
    /// Use for: POST, PUT, DELETE operations.
    /// </summary>
    public static RouteHandlerBuilder RequireWriteRateLimit(this RouteHandlerBuilder builder)
        => builder.RequireRateLimiting("writes");

    /// <summary>
    /// Apply strict rate limiting for admin operations (5 per 5 min).
    /// Use for: seed data, bulk operations, migrations.
    /// </summary>
    public static RouteHandlerBuilder RequireStrictRateLimit(this RouteHandlerBuilder builder)
        => builder.RequireRateLimiting("strict");

    /// <summary>
    /// Apply rate limiting for external API calls (Pexels, Unsplash).
    /// Respects external API rate limits.
    /// </summary>
    public static RouteHandlerBuilder RequireExternalApiRateLimit(this RouteHandlerBuilder builder)
        => builder.RequireRateLimiting("external-api");

    /// <summary>
    /// Apply general rate limiting (100/min fixed window).
    /// Default for most endpoints.
    /// </summary>
    public static RouteHandlerBuilder RequireGeneralRateLimit(this RouteHandlerBuilder builder)
        => builder.RequireRateLimiting("fixed");
}
