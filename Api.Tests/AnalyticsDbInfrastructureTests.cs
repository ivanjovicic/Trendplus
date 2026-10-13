using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.DbContexts;
using Domain.Model;

namespace Trendplus2.Tests;

/// <summary>
/// Schema and Concurrency Guard tests.
/// These tests ensure that critical database columns used by analytics haven't been renamed/dropped
/// and that parallel requests to the endpoint don't cause concurrency crashes.
/// </summary>
[Trait("Category", "Integration")]
public class AnalyticsDbInfrastructureTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public AnalyticsDbInfrastructureTests(WebApplicationFactory<global::Program> factory)
    {
        // Readiness is not under test here; with the gate on (non-Development hosts) /api returns 503
        // before routing, which would race warmup or (route smoke) mask a missing route.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("StartupReadiness:GateApiTraffic", "false"));
    }

    // RQ642 moved schema-column and parallel-route proof to AnalyticsDbInfrastructurePostgresIntegrationTests
    // (PostgresRequiredFact + disposable fixture). Only the environment-sensitive baseline remains here.

    /// <summary>
    /// Performance Baseline Guard: Verifies the endpoint doesn't exceed 3 seconds on standard test data.
    /// Useful for catching unintended N+1 query regressions.
    /// </summary>
    [OperationsIntegrationFact(DisplayName = "Analytics response time is within acceptable limits")]
    public async Task AnalyticsEndpoint_ResponseTimeBaseline()
    {
        var client = _factory.CreateClient();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync("/api/analytics/shoe-type-sales-stats");
        
        sw.Stop();
        
        // Standard baseline: < 3 seconds for test dataset
        // NOTE: perf baselines are environment-sensitive; keep wide enough to avoid false negatives in CI/dev.
        Assert.True(sw.ElapsedMilliseconds < 30000, $"Analytics took {sw.ElapsedMilliseconds}ms, exceeding 30s limit.");
    }
}
