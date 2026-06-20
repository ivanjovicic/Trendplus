using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Endpoints;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
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
public sealed class AnalyticsActionsEndpointsTests
{
    private const string AdminApiKey = "test-admin-key";

    [Fact]
    public async Task PostUpsert_RejectsWithoutAdminKey_ReturnsUnauthorized()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync(withAdminKey: true);

        using var request = CreateJsonRequest(HttpMethod.Post, "/api/analytics/actions", new
        {
            sourceType = AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey = "inventory:sku:auth-1",
            title = "Zaštićena akcija",
            priority = AnalyticsActionConstants.Priorities.P1
        });

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostUpsert_RejectsWithWrongAdminKey_ReturnsForbidden()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync(withAdminKey: true);

        using var request = CreateJsonRequest(HttpMethod.Post, "/api/analytics/actions", new
        {
            sourceType = AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey = "inventory:sku:auth-2",
            title = "Zaštićena akcija",
            priority = AnalyticsActionConstants.Priorities.P1
        }, adminKey: "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostUpsert_AllowsWithAdminKey_AndCreatesAction()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync(withAdminKey: true);

        using var request = CreateJsonRequest(HttpMethod.Post, "/api/analytics/actions", new
        {
            sourceType = AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey = "inventory:sku:auth-3",
            title = "Zaštićena akcija",
            priority = AnalyticsActionConstants.Priorities.P1
        }, adminKey: AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionItem>();
        Assert.NotNull(payload);
        Assert.Equal("inventory:sku:auth-3", payload!.SourceKey);
        Assert.NotNull(payload.ImpactLedger);
        Assert.Equal("pending", payload.ImpactLedger!.Resolution.OutcomeStatus);
    }

    [Fact]
    public async Task PatchStatus_RejectsWithoutAdminKey_ReturnsUnauthorized()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync(withAdminKey: true);
        var actionId = await host.SeedActionAsync();

        using var request = CreateJsonRequest(HttpMethod.Patch, $"/api/analytics/actions/{actionId}/status", new
        {
            status = AnalyticsActionConstants.Statuses.Done,
            note = "status protected"
        });

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_AllowsWithAdminKey_AndUpdatesAction()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync(withAdminKey: true);
        var actionId = await host.SeedActionAsync();

        using var request = CreateJsonRequest(HttpMethod.Patch, $"/api/analytics/actions/{actionId}/status", new
        {
            status = AnalyticsActionConstants.Statuses.Done,
            note = "status protected"
        }, adminKey: AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionItem>();
        Assert.NotNull(payload);
        Assert.Equal(AnalyticsActionConstants.Statuses.Done, payload!.Status);
    }

    [Fact]
    public async Task PatchOutcome_RejectsWithWrongAdminKey_ReturnsForbidden()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync(withAdminKey: true);
        var actionId = await host.SeedActionAsync();

        using var request = CreateJsonRequest(HttpMethod.Patch, $"/api/analytics/actions/{actionId}/outcome", new
        {
            outcomeStatus = AnalyticsActionConstants.OutcomeStatuses.Success,
            measuredImpactRsd = 42m
        }, adminKey: "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchOutcome_AllowsWithAdminKey_AndUpdatesAction()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync(withAdminKey: true);
        var actionId = await host.SeedActionAsync();

        using var request = CreateJsonRequest(HttpMethod.Patch, $"/api/analytics/actions/{actionId}/outcome", new
        {
            outcomeStatus = AnalyticsActionConstants.OutcomeStatuses.Success,
            measuredImpactRsd = 42m
        }, adminKey: AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionItem>();
        Assert.NotNull(payload);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Success, payload!.OutcomeStatus);
    }

    [Fact]
    public async Task PostStatus_ExistingKey_ReturnsExistsWithOutcomeStatus()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();
        var actionId = await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey: "inventory:sku:123",
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Pending,
            status: AnalyticsActionConstants.Statuses.Accepted);

        using var response = await host.Client.PostAsJsonAsync("/api/analytics/actions/status", new
        {
            items = new[]
            {
                new { sourceType = "inventory", sourceKey = "inventory:sku:123" }
            }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionSourceStatusResponse>();
        Assert.NotNull(payload);
        var item = Assert.Single(payload!.Items);
        Assert.Equal("inventory", item.SourceType);
        Assert.Equal("inventory:sku:123", item.SourceKey);
        Assert.True(item.Exists);
        Assert.Equal(actionId, item.ActionId);
        Assert.Equal(AnalyticsActionConstants.Statuses.Accepted, item.Status);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Pending, item.OutcomeStatus);
        Assert.False(item.CanCreateNew);
    }

    [Fact]
    public async Task PostStatus_MissingKey_ReturnsExistsFalse()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();

        using var response = await host.Client.PostAsJsonAsync("/api/analytics/actions/status", new
        {
            items = new[]
            {
                new { sourceType = "inventory", sourceKey = "inventory:sku:404" }
            }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionSourceStatusResponse>();
        Assert.NotNull(payload);
        var item = Assert.Single(payload!.Items);
        Assert.Equal("inventory", item.SourceType);
        Assert.Equal("inventory:sku:404", item.SourceKey);
        Assert.False(item.Exists);
        Assert.Null(item.ActionId);
        Assert.True(item.CanCreateNew);
    }

    [Fact]
    public async Task PostStatus_DifferentSourceTypeSameSourceKey_NoCollision()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey: "shared:key:1",
            status: AnalyticsActionConstants.Statuses.Accepted);

        using var response = await host.Client.PostAsJsonAsync("/api/analytics/actions/status", new
        {
            items = new[]
            {
                new { sourceType = "supplier", sourceKey = "shared:key:1" }
            }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionSourceStatusResponse>();
        Assert.NotNull(payload);
        var item = Assert.Single(payload!.Items);
        Assert.Equal("supplier", item.SourceType);
        Assert.Equal("shared:key:1", item.SourceKey);
        Assert.False(item.Exists);
        Assert.Null(item.ActionId);
        Assert.True(item.CanCreateNew);
    }

    [Fact]
    public async Task PostStatus_ClosedOnlyAction_ReturnsExistsFalseAndAllowsNew()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey: "inventory:sku:closed",
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            status: AnalyticsActionConstants.Statuses.Done);

        using var response = await host.Client.PostAsJsonAsync("/api/analytics/actions/status", new
        {
            items = new[]
            {
                new { sourceType = "inventory", sourceKey = "inventory:sku:closed" }
            }
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionSourceStatusResponse>();
        Assert.NotNull(payload);
        var item = Assert.Single(payload!.Items);
        Assert.False(item.Exists);
        Assert.Equal(AnalyticsActionConstants.Statuses.Done, item.Status);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Success, item.OutcomeStatus);
        Assert.True(item.CanCreateNew);
    }

    [Fact]
    public async Task GetOutcomeSummary_ReturnsAggregatedPayload()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey: "summary-http-1",
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            measuredImpactRsd: 100m,
            outcomeMeasuredAtUtc: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            status: AnalyticsActionConstants.Statuses.Done);
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Supplier,
            sourceKey: "summary-http-2",
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Pending,
            status: AnalyticsActionConstants.Statuses.Done);
        await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Product,
            sourceKey: "summary-http-3",
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Negative,
            measuredImpactRsd: 50m,
            outcomeMeasuredAtUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            status: AnalyticsActionConstants.Statuses.Accepted);

        using var response = await host.Client.GetAsync("/api/analytics/actions/outcomes/summary");

        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.Equal("created", root.GetProperty("meta").GetProperty("periodMode").GetString());
        Assert.Equal(3, root.GetProperty("totals").GetProperty("createdCount").GetInt32());
        Assert.Equal(2, root.GetProperty("totals").GetProperty("closedCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totals").GetProperty("openCount").GetInt32());
        Assert.Equal(2, root.GetProperty("totals").GetProperty("measuredCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totals").GetProperty("pendingOutcomeCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totals").GetProperty("successCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totals").GetProperty("negativeCount").GetInt32());
        Assert.Equal(0.5000m, root.GetProperty("totals").GetProperty("outcomeCoverageRate").GetDecimal());
        Assert.Equal(0.5000m, root.GetProperty("totals").GetProperty("positiveOutcomeRate").GetDecimal());
        Assert.Equal(0.5000m, root.GetProperty("totals").GetProperty("negativeOutcomeRate").GetDecimal());
        Assert.Equal(150m, root.GetProperty("impact").GetProperty("measuredImpactRsd").GetDecimal());
    }

    [Fact]
    public async Task GetOutcomeSummary_InvalidResolvedRange_ReturnsBadRequest()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();

        using var response = await host.Client.GetAsync(
            "/api/analytics/actions/outcomes/summary?resolvedFrom=2026-06-20T00:00:00Z&resolvedTo=2026-06-10T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await response.Content.ReadAsStringAsync();
        Assert.Contains("resolvedFrom must be earlier than or equal to resolvedTo", message);
    }

    [Fact]
    public async Task PatchOutcome_ValidStatusUpdatesFields_AndReturnsDetailedAction()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();
        var actionId = await host.SeedActionAsync();

        using var response = await host.Client.PatchAsJsonAsync($"/api/analytics/actions/{actionId}/outcome", new
        {
            outcomeStatus = AnalyticsActionConstants.OutcomeStatuses.Success,
            measuredImpactRsd = 12345.67m,
            outcomeNotes = "  Uticaj je potvrđen  "
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionItem>();
        Assert.NotNull(payload);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Success, payload!.OutcomeStatus);
        Assert.Equal(12345.67m, payload.MeasuredImpactRsd);
        Assert.Equal("Uticaj je potvrđen", payload.OutcomeNotes);
        Assert.NotNull(payload.OutcomeMeasuredAtUtc);
        Assert.NotNull(payload.Notes);
        Assert.Single(payload.Notes!);
        var auditNote = payload.Notes.Single();
        Assert.Equal(payload.Status, auditNote.StatusFrom);
        Assert.Equal(payload.Status, auditNote.StatusTo);
        Assert.Contains("Outcome: success", auditNote.Note);
    }

    [Fact]
    public async Task PatchOutcome_InvalidStatusReturnsBadRequest()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();
        var actionId = await host.SeedActionAsync();

        using var response = await host.Client.PatchAsJsonAsync($"/api/analytics/actions/{actionId}/outcome", new
        {
            outcomeStatus = "unknown"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await response.Content.ReadAsStringAsync();
        Assert.Contains("outcomeStatus must be one of", message);
    }

    [Fact]
    public async Task PatchOutcome_PendingClearsMeasuredFields_AndKeepsAuditTrail()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();
        var actionId = await host.SeedActionAsync(
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            measuredImpactRsd: 900m,
            outcomeMeasuredAtUtc: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            outcomeNotes: "Prethodno merenje");

        using var response = await host.Client.PatchAsJsonAsync($"/api/analytics/actions/{actionId}/outcome", new
        {
            outcomeStatus = AnalyticsActionConstants.OutcomeStatuses.Pending,
            measuredImpactRsd = 42m,
            outcomeMeasuredAtUtc = "2026-06-12T00:00:00Z",
            outcomeNotes = "Čeka se potvrda"
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnalyticsActionItem>();
        Assert.NotNull(payload);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Pending, payload!.OutcomeStatus);
        Assert.Null(payload.MeasuredImpactRsd);
        Assert.Null(payload.OutcomeMeasuredAtUtc);
        Assert.Equal("Čeka se potvrda", payload.OutcomeNotes);
        Assert.NotNull(payload.Notes);
        Assert.Single(payload.Notes!);
        var auditNote = payload.Notes.Single();
        Assert.Contains("Outcome: pending", auditNote.Note);
        Assert.DoesNotContain("MeasuredImpactRsd=42", auditNote.Note);
    }

    [Fact]
    public async Task GetById_ReturnsImpactLedgerProjection()
    {
        await using var host = await AnalyticsActionsTestHost.CreateAsync();
        var actionId = await host.SeedActionAsync(
            sourceType: AnalyticsActionConstants.SourceTypes.Inventory,
            sourceKey: "inventory:ledger-get-1",
            outcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
            measuredImpactRsd: 77m,
            outcomeMeasuredAtUtc: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            status: AnalyticsActionConstants.Statuses.Done);

        using var response = await host.Client.GetAsync($"/api/analytics/actions/{actionId}");

        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ledger = payload.RootElement.GetProperty("impactLedger");

        Assert.Equal("inventory:ledger-get-1", ledger.GetProperty("sourceRecommendationId").GetString());
        Assert.Equal("done", ledger.GetProperty("resolution").GetProperty("outcomeStatus").GetString());
        Assert.Equal(77m, ledger.GetProperty("resolution").GetProperty("measuredImpactRsd").GetDecimal());
        Assert.Equal("stock_risk + sales_velocity", ledger.GetProperty("snapshot").GetProperty("expectedImpactBasis").GetString());
    }

    private sealed class AnalyticsActionsTestHost : IAsyncDisposable
    {
        private AnalyticsActionsTestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<AnalyticsActionsTestHost> CreateAsync(bool withAdminKey = false)
        {
            var databaseName = $"analytics-actions-endpoints-{Guid.NewGuid():N}";

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();

            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddDbContext<AnalyticsDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            builder.Services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AnalyticsDbContext>());
            builder.Services.AddScoped<AnalyticsActionItemService>();

            if (withAdminKey)
            {
                builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            }

            var app = builder.Build();
            app.MapAnalyticsActionsEndpoints();
            await app.StartAsync();

            return new AnalyticsActionsTestHost(app);
        }

        public async Task<long> SeedActionAsync(
            string? outcomeStatus = null,
            decimal? measuredImpactRsd = null,
            DateTime? outcomeMeasuredAtUtc = null,
            string? outcomeNotes = null,
            string? sourceType = null,
            string? sourceKey = null,
            string? status = null)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            var item = new AnalyticsActionItem
            {
                SourceType = sourceType ?? AnalyticsActionConstants.SourceTypes.Inventory,
                SourceKey = sourceKey ?? $"inventory-{Guid.NewGuid():N}",
                SourceId = 101,
                Title = "Proveri dopunu",
                Description = "Proveriti efekat akcije",
                RecommendationStatus = "dopuna",
                Priority = AnalyticsActionConstants.Priorities.P1,
                Status = status ?? AnalyticsActionConstants.Statuses.Accepted,
                CreatedAtUtc = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc),
                DueAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                ExpectedImpactRsd = 5000m,
                OutcomeStatus = outcomeStatus,
                MeasuredImpactRsd = measuredImpactRsd,
                OutcomeMeasuredAtUtc = outcomeMeasuredAtUtc,
                OutcomeNotes = outcomeNotes,
            };

            db.AnalyticsActionItems.Add(item);
            await db.SaveChangesAsync();
            return item.Id;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string uri, object body, string? adminKey = null)
    {
        var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(body)
        };

        if (!string.IsNullOrWhiteSpace(adminKey))
        {
            request.Headers.Add("X-Admin-Key", adminKey);
        }

        return request;
    }

    private sealed record AnalyticsActionSourceStatusResponse(
        List<AnalyticsActionSourceStatusItem> Items);

    private sealed record AnalyticsActionSourceStatusItem(
        string SourceType,
        string SourceKey,
        bool Exists,
        long? ActionId,
        string? Status,
        string? OutcomeStatus,
        bool CanCreateNew);
}
