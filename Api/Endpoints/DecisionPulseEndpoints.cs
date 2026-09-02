using Api.Services.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace Trendplus2.Endpoints;

public static class DecisionPulseEndpoints
{
    public static void MapDecisionPulseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/decision-pulse")
            .WithTags("Analytics")
            .RequireRateLimiting("analytics");

        group.MapGet("/", GetFeedAsync)
            .WithName("GetDecisionPulse");

        group.MapPost("/email", SendEmailAsync)
            .WithName("SendDecisionPulseEmail");
    }

    private static async Task<IResult> GetFeedAsync(
        [FromServices] DecisionPulseService pulse,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? storeId = null,
        int? supplierId = null,
        string? dataScope = null,
        CancellationToken ct = default)
    {
        var response = await pulse.GetFeedAsync(fromDate, toDate, storeId, supplierId, dataScope, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> SendEmailAsync(
        [FromServices] DecisionPulseService pulse,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? storeId = null,
        int? supplierId = null,
        string? dataScope = null,
        CancellationToken ct = default)
    {
        var feed = await pulse.GetFeedAsync(fromDate, toDate, storeId, supplierId, dataScope, ct);
        var result = await pulse.SendEmailAsync(feed, ct);
        return Results.Ok(new { feed, email = result });
    }
}
