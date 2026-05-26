using System.Net;
using System.Net.Http.Json;
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

public sealed class AnalyticsActionsEndpointsTests
{
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

    private sealed class AnalyticsActionsTestHost : IAsyncDisposable
    {
        private readonly string _databaseName = $"analytics-actions-endpoints-{Guid.NewGuid():N}";

        private AnalyticsActionsTestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<AnalyticsActionsTestHost> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();

            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddDbContext<AnalyticsDbContext>(options =>
                options.UseInMemoryDatabase($"analytics-actions-endpoints-{Guid.NewGuid():N}"));
            builder.Services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AnalyticsDbContext>());
            builder.Services.AddScoped<AnalyticsActionItemService>();

            var app = builder.Build();
            app.MapAnalyticsActionsEndpoints();
            await app.StartAsync();

            return new AnalyticsActionsTestHost(app);
        }

        public async Task<long> SeedActionAsync(
            string? outcomeStatus = null,
            decimal? measuredImpactRsd = null,
            DateTime? outcomeMeasuredAtUtc = null,
            string? outcomeNotes = null)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            var item = new AnalyticsActionItem
            {
                SourceType = AnalyticsActionConstants.SourceTypes.Inventory,
                SourceKey = $"inventory-{Guid.NewGuid():N}",
                SourceId = 101,
                Title = "Proveri dopunu",
                Description = "Proveriti efekat akcije",
                RecommendationStatus = "dopuna",
                Priority = AnalyticsActionConstants.Priorities.P1,
                Status = AnalyticsActionConstants.Statuses.Accepted,
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
}