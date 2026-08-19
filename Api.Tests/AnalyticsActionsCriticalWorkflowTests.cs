using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Endpoints;
using Application.Artikli.Common.Interfaces;
using Application.Analytics;
using Domain.Model.Analytics;
using Infrastructure.DbContexts;
using Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class AnalyticsActionsCriticalWorkflowTests
{
    private const string AdminApiKey = "analytics-actions-critical-key";

    [Fact]
    public async Task Upsert_SameOpenSourceTupleIsIdempotent()
    {
        await using var host = await ActionsHost.CreateAsync();
        var body = new
        {
            sourceType = AnalyticsActionConstants.SourceTypes.Product,
            sourceKey = "product:critical:101",
            sourceId = 101,
            title = "Dopuni Model 101",
            description = "Niska pokrivenost zalihe.",
            recommendationStatus = "REPLENISH",
            priority = AnalyticsActionConstants.Priorities.P1,
            expectedImpactRsd = 25_000m,
            confidencePct = 88,
            reliabilityPct = 82,
            dataQualityStatus = "good"
        };

        var first = await PostActionAsync(host.Client, body);
        var second = await PostActionAsync(host.Client, body);

        Assert.True(first.GetProperty("created").GetBoolean());
        Assert.False(first.GetProperty("existing").GetBoolean());
        Assert.False(second.GetProperty("created").GetBoolean());
        Assert.True(second.GetProperty("existing").GetBoolean());

        var firstId = first.GetProperty("item").GetProperty("id").GetInt64();
        var secondId = second.GetProperty("item").GetProperty("id").GetInt64();
        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await host.CountActionsAsync());
    }

    [Fact]
    public async Task Upsert_ClosedActionAllowsFreshActionForSameSourceTuple()
    {
        await using var host = await ActionsHost.CreateAsync();
        var closedId = await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey: "inventory:sku:closed-1",
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success);

        var result = await PostActionAsync(host.Client, new
        {
            sourceType = AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey = "inventory:sku:closed-1",
            title = "Ponovo proveri zalihu",
            priority = AnalyticsActionConstants.Priorities.P2,
            dataQualityStatus = "warning"
        });

        Assert.True(result.GetProperty("created").GetBoolean());
        Assert.False(result.GetProperty("existing").GetBoolean());
        Assert.NotEqual(closedId, result.GetProperty("item").GetProperty("id").GetInt64());
        Assert.Equal(2, await host.CountActionsAsync());
    }

    [Fact]
    public async Task Upsert_NormalizesLegacyDataQualityWithoutLosingDecisionFields()
    {
        await using var host = await ActionsHost.CreateAsync();

        var result = await PostActionAsync(host.Client, new
        {
            sourceType = AnalyticsActionConstants.SourceTypes.Supplier,
            sourceKey = "supplier:77:review",
            sourceId = 77,
            title = "Proveri dobavljača 77",
            priority = AnalyticsActionConstants.Priorities.P2,
            dataQualityStatus = "fair",
            sourceRecommendationId = "supplier:77:expand",
            recommendationType = "EXPAND_SELECTIVELY",
            expectedImpactBasis = "revenue + margin + reliability",
            confidenceLevel = "medium",
            warningCodes = new[] { "PARTIAL_COVERAGE" },
            primaryDrivers = new[] { "margin", "supplier_reliability" },
            decisionReason = "Dobar signal uz delimičnu pokrivenost.",
            recommendedAction = "Proširi selektivno",
            generatedAtUtc = "2026-07-02T08:00:00Z",
            inputFreshnessStatus = "fresh"
        });

        var item = result.GetProperty("item");
        Assert.Equal("warning", item.GetProperty("dataQualityStatus").GetString());

        var creation = item
            .GetProperty("ledgerSnapshot")
            .GetProperty("creationSnapshot");
        Assert.Equal("supplier:77:expand", creation.GetProperty("sourceRecommendationId").GetString());
        Assert.Equal("EXPAND_SELECTIVELY", creation.GetProperty("recommendationType").GetString());
        Assert.Equal("Proširi selektivno", creation.GetProperty("recommendedAction").GetString());

        var lifecycle = item.GetProperty("recommendationLifecycle");
        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Issued, lifecycle.GetProperty("lifecycleState").GetString());
        Assert.False(lifecycle.GetProperty("learningEligible").GetBoolean());
        Assert.Contains(
            "acceptance_is_not_success",
            lifecycle.GetProperty("learningEligibilityReasonCodes").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task List_AppliesCanonicalFiltersSearchPagingAndPriorityOrdering()
    {
        await using var host = await ActionsHost.CreateAsync();
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Product,
            sourceKey: "product:list:1",
            status: AnalyticsActionConstants.Statuses.Accepted,
            priority: AnalyticsActionConstants.Priorities.P1,
            dataQualityStatus: "fair",
            title: "Urgentna dopuna Model A");
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Product,
            sourceKey: "product:list:2",
            status: AnalyticsActionConstants.Statuses.Accepted,
            priority: AnalyticsActionConstants.Priorities.P2,
            dataQualityStatus: "warning",
            title: "Urgentna dopuna Model B");
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Supplier,
            sourceKey: "supplier:list:3",
            status: AnalyticsActionConstants.Statuses.New,
            priority: AnalyticsActionConstants.Priorities.P1,
            dataQualityStatus: "good",
            title: "Pregled dobavljača");

        using var response = await host.Client.GetAsync(
            "/api/analytics/actions?status=accepted&sourceType=product&dataQualityStatus=warning&search=urgentna&page=1&pageSize=1");
        var root = await ReadJsonAsync(response, HttpStatusCode.OK);

        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(1, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, root.GetProperty("totalPages").GetInt32());

        var item = Assert.Single(root.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal("product:list:1", item.GetProperty("sourceKey").GetString());
        Assert.Equal(AnalyticsActionConstants.Priorities.P1, item.GetProperty("priority").GetString());
    }

    [Theory]
    [InlineData("status=not-a-status", "status must be one of")]
    [InlineData("priority=P9", "priority must be one of")]
    [InlineData("sourceType=unknown", "sourceType must be one of")]
    [InlineData("dataQualityStatus=impossible", "dataQualityStatus must be one of")]
    public async Task List_InvalidFiltersReturnBadRequest(string query, string expectedMessage)
    {
        await using var host = await ActionsHost.CreateAsync();

        using var response = await host.Client.GetAsync($"/api/analytics/actions?{query}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedMessage, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SourceStatusProbe_DeduplicatesInputsAndPrefersOpenActionOverClosedHistory()
    {
        await using var host = await ActionsHost.CreateAsync();
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Product,
            sourceKey: "product:probe:1",
            status: AnalyticsActionConstants.Statuses.Done,
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            updatedAtUtc: new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc));
        var openId = await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Product,
            sourceKey: "product:probe:1",
            status: AnalyticsActionConstants.Statuses.Accepted,
            updatedAtUtc: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));

        using var response = await host.Client.PostAsJsonAsync("/api/analytics/actions/status", new
        {
            items = new[]
            {
                new { sourceType = "product", sourceKey = "product:probe:1" },
                new { sourceType = "product", sourceKey = "product:probe:1" }
            }
        });
        var root = await ReadJsonAsync(response, HttpStatusCode.OK);

        var item = Assert.Single(root.GetProperty("items").EnumerateArray().ToArray());
        Assert.True(item.GetProperty("exists").GetBoolean());
        Assert.False(item.GetProperty("canCreateNew").GetBoolean());
        Assert.Equal(openId, item.GetProperty("actionId").GetInt64());
        Assert.Equal(AnalyticsActionConstants.Statuses.Accepted, item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SourceStatusProbe_RejectsOversizedBatchBeforeDatabaseWork()
    {
        await using var host = await ActionsHost.CreateAsync();
        var items = Enumerable.Range(1, 1001)
            .Select(index => new { sourceType = "product", sourceKey = $"product:{index}" })
            .ToArray();

        using var response = await host.Client.PostAsJsonAsync("/api/analytics/actions/status", new { items });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at most 1000", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Counts_ReturnsStatusBucketsAndOnlyOpenP1Actions()
    {
        await using var host = await ActionsHost.CreateAsync();
        await host.SeedActionAsync(status: AnalyticsActionConstants.Statuses.New, priority: AnalyticsActionConstants.Priorities.P1);
        await host.SeedActionAsync(status: AnalyticsActionConstants.Statuses.Accepted, priority: AnalyticsActionConstants.Priorities.P1);
        await host.SeedActionAsync(status: AnalyticsActionConstants.Statuses.Deferred, priority: AnalyticsActionConstants.Priorities.P2);
        await host.SeedActionAsync(status: AnalyticsActionConstants.Statuses.Done, priority: AnalyticsActionConstants.Priorities.P1);
        await host.SeedActionAsync(status: AnalyticsActionConstants.Statuses.Rejected, priority: AnalyticsActionConstants.Priorities.P3);

        using var response = await host.Client.GetAsync("/api/analytics/actions/counts");
        var root = await ReadJsonAsync(response, HttpStatusCode.OK);

        Assert.Equal(1, root.GetProperty("new").GetInt32());
        Assert.Equal(1, root.GetProperty("accepted").GetInt32());
        Assert.Equal(1, root.GetProperty("deferred").GetInt32());
        Assert.Equal(1, root.GetProperty("done").GetInt32());
        Assert.Equal(1, root.GetProperty("rejected").GetInt32());
        Assert.Equal(2, root.GetProperty("p1Open").GetInt32());
    }

    [Fact]
    public async Task DetailAndMutations_ReturnNotFoundForUnknownAction()
    {
        await using var host = await ActionsHost.CreateAsync();

        using var getResponse = await host.Client.GetAsync("/api/analytics/actions/999999");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        using var statusRequest = CreateAdminJsonRequest(
            HttpMethod.Patch,
            "/api/analytics/actions/999999/status",
            new { status = AnalyticsActionConstants.Statuses.Done });
        using var statusResponse = await host.Client.SendAsync(statusRequest);
        Assert.Equal(HttpStatusCode.NotFound, statusResponse.StatusCode);

        using var outcomeRequest = CreateAdminJsonRequest(
            HttpMethod.Patch,
            "/api/analytics/actions/999999/outcome",
            new { outcomeStatus = AnalyticsActionConstants.OutcomeStatuses.Success, measuredImpactRsd = 100m });
        using var outcomeResponse = await host.Client.SendAsync(outcomeRequest);
        Assert.Equal(HttpStatusCode.NotFound, outcomeResponse.StatusCode);
    }

    [Fact]
    public async Task Outcome_RejectsNotesLongerThanContractLimit()
    {
        await using var host = await ActionsHost.CreateAsync();
        var actionId = await host.SeedActionAsync();

        using var request = CreateAdminJsonRequest(
            HttpMethod.Patch,
            $"/api/analytics/actions/{actionId}/outcome",
            new
            {
                outcomeStatus = AnalyticsActionConstants.OutcomeStatuses.Success,
                outcomeNotes = new string('x', 4001)
            });
        using var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("4000 characters or fewer", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OutcomeLifecycle_AcceptanceIsNotSuccess_AndNotMeasuredDoesNotStampTimestamp()
    {
        await using var host = await ActionsHost.CreateAsync();
        var created = await PostActionAsync(host.Client, new
        {
            sourceType = AnalyticsActionConstants.SourceTypes.Product,
            sourceKey = "product:learning:1",
            sourceId = 101,
            title = "Dopuni Model 101",
            description = "Niska pokrivenost zalihe.",
            recommendationStatus = "REPLENISH",
            priority = AnalyticsActionConstants.Priorities.P1,
            expectedImpactRsd = 25_000m,
            dataQualityStatus = "good"
        });

        var item = created.GetProperty("item");
        var actionId = item.GetProperty("id").GetInt64();
        Assert.False(item.GetProperty("recommendationLifecycle").GetProperty("learningEligible").GetBoolean());
        Assert.True(
            !item.TryGetProperty("outcomeMeasuredAtUtc", out var createdMeasuredAt)
            || createdMeasuredAt.ValueKind == JsonValueKind.Null);

        using var acceptRequest = CreateAdminJsonRequest(
            HttpMethod.Patch,
            $"/api/analytics/actions/{actionId}/status",
            new { status = AnalyticsActionConstants.Statuses.Accepted, note = "Prihvaceno" });
        using var acceptResponse = await host.Client.SendAsync(acceptRequest);
        var accepted = await ReadJsonAsync(acceptResponse, HttpStatusCode.OK);
        Assert.False(accepted.GetProperty("recommendationLifecycle").GetProperty("learningEligible").GetBoolean());
        Assert.Contains(
            "acceptance_is_not_success",
            accepted.GetProperty("recommendationLifecycle").GetProperty("learningEligibilityReasonCodes")
                .EnumerateArray()
                .Select(x => x.GetString()));

        using var doneRequest = CreateAdminJsonRequest(
            HttpMethod.Patch,
            $"/api/analytics/actions/{actionId}/status",
            new { status = AnalyticsActionConstants.Statuses.Done, note = "Izvrseno" });
        using var doneResponse = await host.Client.SendAsync(doneRequest);
        var executed = await ReadJsonAsync(doneResponse, HttpStatusCode.OK);
        Assert.False(executed.GetProperty("recommendationLifecycle").GetProperty("learningEligible").GetBoolean());
        Assert.True(
            !executed.TryGetProperty("outcomeMeasuredAtUtc", out var executedMeasuredAt)
            || executedMeasuredAt.ValueKind == JsonValueKind.Null);

        using var notMeasuredRequest = CreateAdminJsonRequest(
            HttpMethod.Patch,
            $"/api/analytics/actions/{actionId}/outcome",
            new
            {
                outcomeStatus = AnalyticsActionConstants.OutcomeStatuses.NotMeasured,
                measuredImpactRsd = 999m,
                outcomeMeasuredAtUtc = DateTime.UtcNow
            });
        using var notMeasuredResponse = await host.Client.SendAsync(notMeasuredRequest);
        var notMeasured = await ReadJsonAsync(notMeasuredResponse, HttpStatusCode.OK);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.NotMeasured, notMeasured.GetProperty("outcomeStatus").GetString());
        Assert.Equal(JsonValueKind.Null, notMeasured.GetProperty("outcomeMeasuredAtUtc").ValueKind);
        Assert.False(notMeasured.GetProperty("recommendationLifecycle").GetProperty("learningEligible").GetBoolean());

        using var measuredRequest = CreateAdminJsonRequest(
            HttpMethod.Patch,
            $"/api/analytics/actions/{actionId}/outcome",
            new
            {
                outcomeStatus = AnalyticsActionConstants.OutcomeStatuses.Success,
                measuredImpactRsd = 180m,
                outcomeMeasuredAtUtc = "2026-08-10T09:00:00Z",
                evidenceSource = "action_outcome_summary",
                evidenceReference = "summary:product:learning:1"
            });
        using var measuredResponse = await host.Client.SendAsync(measuredRequest);
        var measured = await ReadJsonAsync(measuredResponse, HttpStatusCode.OK);
        Assert.True(measured.GetProperty("recommendationLifecycle").GetProperty("learningEligible").GetBoolean());
        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Executed, measured.GetProperty("recommendationLifecycle").GetProperty("lifecycleState").GetString());
        Assert.NotEqual(JsonValueKind.Null, measured.GetProperty("outcomeMeasuredAtUtc").ValueKind);
    }

    private static async Task<JsonElement> PostActionAsync(HttpClient client, object body)
    {
        using var request = CreateAdminJsonRequest(HttpMethod.Post, "/api/analytics/actions", body);
        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static HttpRequestMessage CreateAdminJsonRequest(HttpMethod method, string url, object body)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        return request;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body));
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed class ActionsHost : IAsyncDisposable
    {
        private ActionsHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<ActionsHost> CreateAsync()
        {
            var databaseName = $"analytics-actions-critical-{Guid.NewGuid():N}";
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();
            builder.Configuration["Admin:ApiKey"] = AdminApiKey;

            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddDbContext<AnalyticsDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            builder.Services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AnalyticsDbContext>());
            builder.Services.AddScoped<AnalyticsActionItemService>();

            var app = builder.Build();
            app.MapAnalyticsActionsEndpoints();
            await app.StartAsync();
            return new ActionsHost(app);
        }

        public async Task<long> SeedActionAsync(
            string? sourceType = null,
            string? sourceKey = null,
            string? status = null,
            string? priority = null,
            string? dataQualityStatus = null,
            string? outcomeStatus = null,
            string? title = null,
            DateTime? updatedAtUtc = null)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            var now = updatedAtUtc ?? DateTime.UtcNow;
            var item = new AnalyticsActionItem
            {
                SourceType = sourceType ?? AnalyticsActionConstants.SourceTypes.Inventory,
                SourceKey = sourceKey ?? $"inventory:{Guid.NewGuid():N}",
                SourceId = 101,
                Title = title ?? "Proveri analytics signal",
                Description = "Kritičan workflow test",
                RecommendationStatus = "REVIEW",
                Priority = priority ?? AnalyticsActionConstants.Priorities.P2,
                Status = status ?? AnalyticsActionConstants.Statuses.New,
                DataQualityStatus = dataQualityStatus,
                OutcomeStatus = outcomeStatus,
                CreatedAtUtc = now.AddMinutes(-1),
                UpdatedAtUtc = now,
                DueAtUtc = now.AddDays(7)
            };
            db.AnalyticsActionItems.Add(item);
            await db.SaveChangesAsync();
            return item.Id;
        }

        public async Task<int> CountActionsAsync()
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            return await db.AnalyticsActionItems.CountAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}
