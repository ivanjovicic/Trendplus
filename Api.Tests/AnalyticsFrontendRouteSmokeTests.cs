using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class AnalyticsFrontendRouteSmokeTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public AnalyticsFrontendRouteSmokeTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/analytics/refresh-status?dataScope=all")]
    [InlineData("/api/analytics/actions?status=deferred&sourceType=product&page=1&pageSize=200&dataScope=all")]
    [InlineData("/api/analytics/actions?status=accepted&sourceType=product&page=1&pageSize=200&dataScope=all")]
    [InlineData("/api/analytics/actions?status=new&sourceType=product&page=1&pageSize=200&dataScope=all")]
    [InlineData("/api/analytics/cached/products/decision-center?fromDate=2026-05-01&toDate=2026-05-31&top=1200&dataScope=all")]
    [InlineData("/api/analytics/inventory/list?page=1&pageSize=20&dataScope=all")]
    [InlineData("/api/analytics/supplier-sales-stats?fromDate=2026-02-15&toDate=2026-03-15&dataScope=all")]
    [InlineData("/api/analytics/shoe-type-sales-stats?fromDate=2026-02-15&toDate=2026-03-15&dataScope=all")]
    [InlineData("/api/analytics/daily-sales?fromDate=2026-02-15&toDate=2026-03-15&dataScope=all")]
    [InlineData("/api/analytics/vendor-sales-nivelacija?from=2026-02-15&to=2026-03-15&dataScope=all")]
    [InlineData("/api/analytics/color-sales-stats?fromDate=2026-02-15&toDate=2026-03-15&dataScope=all")]
    [InlineData("/api/analytics/pre-nivelacija-prioriteti?page=1&pageSize=20&dataScope=all")]
    public async Task FrontendAnalyticsRoutes_AreRegistered_AndDoNotReturn404(string url)
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.True(
            response.StatusCode != HttpStatusCode.NotFound,
            $"Expected registered analytics route for '{url}', but received 404 Not Found.");
    }
}
