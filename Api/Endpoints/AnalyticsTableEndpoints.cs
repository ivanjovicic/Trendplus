using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Trendplus2.Endpoints;

public static class AnalyticsTableEndpoints
{
    public static void MapAnalyticsTableEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analitika/{table}/{id}", async (
            string table,
            string id,
            HttpRequest request,
            IAnalyticsDetailReadService detailService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("Fetching analytics detail for table {Table} and id {Id}", table, id);
            var detail = await detailService.GetDetailAsync(table, id, request.Query, ct);
            return detail is null
                ? Results.NotFound(new { message = $"Detalj nije pronađen za tabelu '{table}' i zapis '{id}'." })
                : Results.Ok(detail);
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }
}
