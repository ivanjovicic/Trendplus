using Api.Services;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Mvc;

namespace Trendplus2.Endpoints;

public static class AnalyticsReportsEndpoints
{
    public static void MapAnalyticsReportsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/reports")
            .WithTags("Analytics Reports")
            .RequireRateLimiting("analytics");

        group.MapGet("/supplier-decision", SupplierDecisionHubEndpoints.HandleSupplierDecisionReportAsync);

        group.MapGet("/pilot-intake", DataQualityEndpoints.HandlePilotIntakeReportAsync);
    }
}
