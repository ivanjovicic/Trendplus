using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Endpoints;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Trendplus2.Endpoints;

public static class ScraperScoringEndpoints
{
    private sealed class ItemsCacheEntry
    {
        public required string ETag { get; init; }
        public required CanonicalItemsResponse Payload { get; init; }
    }

    public static void MapScoringEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Scraper Scoring")
            .RequireRateLimiting("api-v1");

        group.MapGet("/items", async (
            HttpContext httpContext,
            IScraperScoringQueryService service,
            IMemoryCache cache,
            string? brand = null,
            string? category = null,
            string? color = null,
            string? search = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (!TryNormalizePagination(page, pageSize, 20, out var p, out var ps, out var badResult))
                return badResult!;

            var cacheKey = $"api:v1:items:brand={brand}|category={category}|color={color}|search={search}|page={p}|size={ps}";
            if (cache.TryGetValue(cacheKey, out ItemsCacheEntry? cached) && cached is not null)
            {
                httpContext.Response.Headers.ETag = cached.ETag;
                if (IsEtagMatch(httpContext, cached.ETag))
                    return Results.StatusCode(StatusCodes.Status304NotModified);

                return Results.Ok(cached.Payload);
            }

            try
            {
                var payload = await service.GetItemsAsync(brand, category, color, search, p, ps, ct);
                var etag = BuildEtag(payload);
                var entry = new ItemsCacheEntry { ETag = etag, Payload = payload };

                cache.Set(cacheKey, entry, TimeSpan.FromMinutes(30));
                httpContext.Response.Headers.ETag = etag;

                if (IsEtagMatch(httpContext, etag))
                    return Results.StatusCode(StatusCodes.Status304NotModified);

                return Results.Ok(payload);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                await HandledErrorLogging.PersistHandledIssueAsync(
                    httpContext,
                    level: "Error",
                    message: $"Scoring items schema missing: {ex.GetBaseException().Message}",
                    exceptionType: ex.GetType().FullName ?? ex.GetType().Name,
                    stackTrace: ex.StackTrace,
                    ct);
                return Results.Problem(
                    title: "Scoring schema nije podignuta",
                    detail: "Pokreni Database/Analytics/004_AddScraperScoringTables.sql migraciju.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetCanonicalItems")
        .Produces<CanonicalItemsResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/trending", async (
            HttpContext httpContext,
            IScraperScoringQueryService service,
            IMemoryCache cache,
            string? category = null,
            string? brand = null,
            string? market = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (!TryNormalizePagination(page, pageSize, 20, out var p, out var ps, out var badResult))
                return badResult!;

            var cacheKey = $"api:v1:trending:category={category}|brand={brand}|market={market}|page={p}|size={ps}";
            if (cache.TryGetValue(cacheKey, out TrendingResponse? cached) && cached is not null)
                return Results.Ok(cached);

            try
            {
                var payload = await service.GetTrendingAsync(category, brand, market, p, ps, ct);
                cache.Set(cacheKey, payload, TimeSpan.FromMinutes(5));
                return Results.Ok(payload);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                await HandledErrorLogging.PersistHandledIssueAsync(
                    httpContext,
                    level: "Error",
                    message: $"Scoring trending schema missing: {ex.GetBaseException().Message}",
                    exceptionType: ex.GetType().FullName ?? ex.GetType().Name,
                    stackTrace: ex.StackTrace,
                    ct);
                return Results.Problem(
                    title: "Scoring schema nije podignuta",
                    detail: "Pokreni Database/Analytics/004_AddScraperScoringTables.sql migraciju.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetTrendingItems")
        .Produces<TrendingResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/momentum", async (
            HttpContext httpContext,
            IScraperScoringQueryService service,
            IMemoryCache cache,
            string? category = null,
            string? market = null,
            decimal threshold = 0.2m,
            string? sortBy = "normalized",
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (!TryNormalizePagination(page, pageSize, 20, out var p, out var ps, out var badResult))
                return badResult!;

            if (threshold < 0)
                return Results.BadRequest(new { error = "threshold mora biti >= 0" });

            var cacheKey = $"api:v1:momentum:category={category}|market={market}|threshold={threshold}|sortBy={sortBy}|page={p}|size={ps}";
            if (cache.TryGetValue(cacheKey, out MomentumResponse? cached) && cached is not null)
                return Results.Ok(cached);

            try
            {
                var payload = await service.GetMomentumAsync(category, market, threshold, sortBy, p, ps, ct);
                cache.Set(cacheKey, payload, TimeSpan.FromMinutes(10));
                return Results.Ok(payload);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                await HandledErrorLogging.PersistHandledIssueAsync(
                    httpContext,
                    level: "Error",
                    message: $"Scoring momentum schema missing: {ex.GetBaseException().Message}",
                    exceptionType: ex.GetType().FullName ?? ex.GetType().Name,
                    stackTrace: ex.StackTrace,
                    ct);
                return Results.Problem(
                    title: "Scoring schema nije podignuta",
                    detail: "Pokreni Database/Analytics/004_AddScraperScoringTables.sql migraciju.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetMomentumItems")
        .Produces<MomentumResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/markets", async (
            HttpContext httpContext,
            IScraperScoringQueryService service,
            IMemoryCache cache,
            string market = "DE",
            string? category = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (!TryNormalizePagination(page, pageSize, 20, out var p, out var ps, out var badResult))
                return badResult!;

            var normalizedMarket = string.IsNullOrWhiteSpace(market) ? "DE" : market.Trim().ToUpperInvariant();
            var cacheKey = $"api:v1:markets:market={normalizedMarket}|category={category}|page={p}|size={ps}";
            if (cache.TryGetValue(cacheKey, out MarketsResponse? cached) && cached is not null)
                return Results.Ok(cached);

            try
            {
                var payload = await service.GetMarketsAsync(normalizedMarket, category, p, ps, ct);
                cache.Set(cacheKey, payload, TimeSpan.FromMinutes(10));
                return Results.Ok(payload);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                await HandledErrorLogging.PersistHandledIssueAsync(
                    httpContext,
                    level: "Error",
                    message: $"Scoring markets schema missing: {ex.GetBaseException().Message}",
                    exceptionType: ex.GetType().FullName ?? ex.GetType().Name,
                    stackTrace: ex.StackTrace,
                    ct);
                return Results.Problem(
                    title: "Scoring schema nije podignuta",
                    detail: "Pokreni Database/Analytics/004_AddScraperScoringTables.sql migraciju.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetMarketTopItems")
        .Produces<MarketsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/debug/score/{itemId:long}", async (
            long itemId,
            HttpContext httpContext,
            IScraperScoringQueryService service,
            IMemoryCache cache,
            CancellationToken ct = default) =>
        {
            if (itemId <= 0)
                return Results.BadRequest(new { error = "itemId mora biti > 0" });

            var cacheKey = $"api:v1:debug:score:{itemId}";
            if (cache.TryGetValue(cacheKey, out DebugScoreResponse? cached) && cached is not null)
                return Results.Ok(cached);

            try
            {
                var payload = await service.GetDebugScoreAsync(itemId, ct);
                if (payload is null)
                    return Results.NotFound(new { message = $"Item {itemId} nije pronađen u latest run-u." });

                cache.Set(cacheKey, payload, TimeSpan.FromMinutes(1));
                return Results.Ok(payload);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                await HandledErrorLogging.PersistHandledIssueAsync(
                    httpContext,
                    level: "Error",
                    message: $"Scoring debug schema missing: {ex.GetBaseException().Message}",
                    exceptionType: ex.GetType().FullName ?? ex.GetType().Name,
                    stackTrace: ex.StackTrace,
                    ct);
                return Results.Problem(
                    title: "Scoring schema nije podignuta",
                    detail: "Pokreni Database/Analytics/004_AddScraperScoringTables.sql migraciju.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetDebugScoreBreakdown")
        .Produces<DebugScoreResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static bool TryNormalizePagination(
        int page,
        int pageSize,
        int defaultPageSize,
        out int normalizedPage,
        out int normalizedPageSize,
        out IResult? error)
    {
        normalizedPage = page <= 0 ? 1 : page;
        normalizedPageSize = pageSize <= 0 ? defaultPageSize : pageSize;

        if (normalizedPageSize > 100)
        {
            error = Results.BadRequest(new { error = "pageSize ne sme biti veći od 100." });
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsEtagMatch(HttpContext httpContext, string etag)
    {
        var ifNoneMatch = httpContext.Request.Headers.IfNoneMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
            return false;

        var tokens = ifNoneMatch.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (token == "*" || string.Equals(token, etag, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string BuildEtag(CanonicalItemsResponse payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"\"{Convert.ToHexString(hash)}\"";
    }
}
