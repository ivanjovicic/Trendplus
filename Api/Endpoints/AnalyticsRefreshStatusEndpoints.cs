using Api.Services;

namespace Trendplus2.Endpoints;

public static class AnalyticsRefreshStatusEndpoints
{
    public static void MapAnalyticsRefreshStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/refresh-status", (AnalyticsRefreshStatusService refreshStatusService) =>
            Results.Ok(refreshStatusService.GetStatus()))
            .WithName("GetAnalyticsRefreshStatus")
            .WithTags("Analytics")
            .RequireRateLimiting("analytics");
    }
}
