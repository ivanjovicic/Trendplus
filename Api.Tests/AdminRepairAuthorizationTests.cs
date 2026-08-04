using System.Net;
using System.Net.Http.Json;
using Api.Endpoints;
using Api.Models;
using Api.Services;
using Trendplus2.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.RateLimiting;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class AdminRepairAuthorizationTests
{
    private const string AdminApiKey = "test-admin-key";

    [Fact]
    public async Task RepairNivelacije_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.PostAsJsonAsync("/admin/repair/nivelacije", new
        {
            dryRun = false,
            confirm = true,
            sourceFilePath = "repair.accdb",
            maxRowsToModify = 10
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Service.ExecuteRepairCallCount);
    }

    [Fact]
    public async Task RepairNivelacije_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/repair/nivelacije")
        {
            Content = JsonContent.Create(new
            {
                dryRun = false,
                confirm = true,
                sourceFilePath = "repair.accdb",
                maxRowsToModify = 10
            })
        };
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Service.ExecuteRepairCallCount);
    }

    [Fact]
    public async Task RepairNivelacije_AllowsRequestWithAdminKey_AndInvokesService()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/repair/nivelacije")
        {
            Content = JsonContent.Create(new
            {
                dryRun = false,
                confirm = true,
                sourceFilePath = "repair.accdb",
                maxRowsToModify = 10
            })
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, host.Service.ExecuteRepairCallCount);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, RecordingNivelacijaRepairService service)
        {
            App = app;
            Client = app.GetTestClient();
            Service = service;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public RecordingNivelacijaRepairService Service { get; }

        public static async Task<TestHost> CreateAsync(bool withAdminKey = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy("strict", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "strict",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            if (withAdminKey)
            {
                builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            }

            var service = new RecordingNivelacijaRepairService();
            builder.Services.AddSingleton<INivelacijaRepairService>(service);
            builder.Services.AddSingleton<ILogger<Program>>(NullLogger<Program>.Instance);

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapAdminRepairEndpoints();
            await app.StartAsync();

            return new TestHost(app, service);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class RecordingNivelacijaRepairService : INivelacijaRepairService
    {
        public int ExecuteRepairCallCount { get; private set; }

        public Task<NivelacijaRepairPreflightDto> RunPreflightAsync(string? explicitSourceFilePath = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<NivelacijaRepairIssueDto>> ScanIssuesAsync(string? explicitSourceFilePath = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<NivelacijaRepairPlanDto> GenerateRepairPlanAsync(string? explicitSourceFilePath = null, int maxRowsToModify = 10_000, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<long> WriteDryRunAuditAsync(string requestedBy, NivelacijaRepairPlanDto plan, CancellationToken ct = default)
            => Task.FromResult(0L);

        public Task<NivelacijaRepairExecutionResultDto> ExecuteRepairAsync(string? explicitSourceFilePath, string requestedBy, int maxRowsToModify = 10_000, CancellationToken ct = default)
        {
            ExecuteRepairCallCount++;
            return Task.FromResult(new NivelacijaRepairExecutionResultDto
            {
                SourceFilePath = explicitSourceFilePath ?? string.Empty,
                AuditId = 123,
                FixedRows = 1,
                SkippedRows = 0,
                RemainingIssuesAfterRepair = 0
            });
        }
    }
}
