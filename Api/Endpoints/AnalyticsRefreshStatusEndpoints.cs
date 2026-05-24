using Api.Services;

namespace Trendplus2.Endpoints;

public static class AnalyticsRefreshStatusEndpoints
{
    public static void MapAnalyticsRefreshStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/refresh-status", async (
            AnalyticsRefreshStatusService refreshStatusService,
            CancellationToken ct) =>
            Results.Ok(await refreshStatusService.GetStatusAsync(ct)))
            .WithName("GetAnalyticsRefreshStatus")
            .WithTags("Analytics")
            .RequireRateLimiting("analytics");
    }
}
