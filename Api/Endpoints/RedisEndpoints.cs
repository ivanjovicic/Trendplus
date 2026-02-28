using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.RateLimiting;

namespace Trendplus2.Endpoints;

public static class RedisEndpoints
{
    public sealed record RedisStatusDto(bool Enabled, bool Available);

    public static void MapRedisEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/redis")
            .WithTags("Infra")
            .RequireRateLimiting("fixed");

        group.MapGet("/status", (IAnalyticsCacheService cache) =>
        {
            return Results.Ok(new RedisStatusDto(
                Enabled: cache.IsRedisEnabled,
                Available: cache.IsRedisAvailable));
        })
        .WithName("GetRedisStatus");

        group.MapPost("/toggle", (IAnalyticsCacheService cache) =>
        {
            cache.SetRedisEnabled(!cache.IsRedisEnabled);
            return Results.Ok(new RedisStatusDto(
                Enabled: cache.IsRedisEnabled,
                Available: cache.IsRedisAvailable));
        })
        .WithName("ToggleRedis");
    }
}

