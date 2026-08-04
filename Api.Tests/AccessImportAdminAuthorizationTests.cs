using System.Net;
using System.Net.Http.Json;
using Api.Endpoints;
using Api.Models;
using Api.Services;
using Api.Services.Access;
using Trendplus2.Endpoints;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.RateLimiting;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class AccessImportAdminAuthorizationTests
{
    private const string AdminApiKey = "test-admin-key";

    [Fact]
    public async Task CancelBatch_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.PostAsync("/api/access-import/batches/42/cancel", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Service.RequestCancellationCallCount);
    }

    [Fact]
    public async Task CancelBatch_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/batches/42/cancel");
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Service.RequestCancellationCallCount);
    }

    [Fact]
    public async Task CancelBatch_AllowsRequestWithAdminKey_AndInvokesService()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/batches/42/cancel");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, host.Service.RequestCancellationCallCount);
    }

    [Fact]
    public async Task DeleteBatch_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.DeleteAsync("/api/access-import/batches/42?includeAnalytics=true");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Service.DeleteBatchCallCount);
    }

    [Fact]
    public async Task DeleteBatch_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/access-import/batches/42?includeAnalytics=true");
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Service.DeleteBatchCallCount);
    }

    [Fact]
    public async Task DeleteBatch_AllowsRequestWithAdminKey_AndInvokesService()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/access-import/batches/42?includeAnalytics=true");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, host.Service.DeleteBatchCallCount);
    }

    [Fact]
    public async Task EnqueueJob_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.PostAsync("/api/access-import/jobs/42/enqueue", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Queue.EnqueueCallCount);
    }

    [Fact]
    public async Task EnqueueJob_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/jobs/42/enqueue");
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Queue.EnqueueCallCount);
    }

    [Fact]
    public async Task EnqueueJob_AllowsRequestWithAdminKey_AndInvokesQueue()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/jobs/42/enqueue");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, host.Queue.EnqueueCallCount);
    }

    [Fact]
    public async Task RestoreScript_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.PostAsJsonAsync("/api/access-import/cleanup/archive/restore-script", new { ids = new[] { 1, 2, 3 } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RestoreScript_RejectsRequestWithWrongAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/cleanup/archive/restore-script")
        {
            Content = JsonContent.Create(new { ids = new[] { 1, 2, 3 } })
        };
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RestoreScript_AllowsRequestWithAdminKey_AndValidatesBody()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/cleanup/archive/restore-script")
        {
            Content = JsonContent.Create(new { ids = Array.Empty<int>() })
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, RecordingAccessImportService service, RecordingAccessImportJobQueue queue)
        {
            App = app;
            Client = app.GetTestClient();
            Service = service;
            Queue = queue;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public RecordingAccessImportService Service { get; }
        public RecordingAccessImportJobQueue Queue { get; }

        public static async Task<TestHost> CreateAsync(bool withAdminKey = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddMemoryCache();
            builder.Services.AddDbContext<TrendplusDbContext>(options =>
                options.UseInMemoryDatabase($"access-import-admin-auth-{Guid.NewGuid():N}"));
            builder.Services.AddRateLimiter(options =>
            {
                foreach (var policyName in new[] { "writes", "fixed", "db-heavy" })
                {
                    options.AddPolicy(policyName, _ =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: policyName,
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 100,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));
                }
            });

            if (withAdminKey)
            {
                builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            }

            var service = new RecordingAccessImportService();
            var queue = new RecordingAccessImportJobQueue();
            builder.Services.AddSingleton<IAccessImportService>(service);
            builder.Services.AddSingleton<IAccessImportJobQueue>(queue);
            builder.Services.AddSingleton<ILogger<Program>>(NullLogger<Program>.Instance);

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapAccessImportEndpoints();
            app.MapAccessImportRestoreEndpoints();
            await app.StartAsync();

            return new TestHost(app, service, queue);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class RecordingAccessImportService : IAccessImportService
    {
        public int RequestCancellationCallCount { get; private set; }
        public int DeleteBatchCallCount { get; private set; }

        public Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<AccessImportRunResponse> ImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<AccessImportRunResponse> StartImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<AccessImportRunResponse> RunExistingBatchAsync(long batchId, string accessFilePath, string sourceFileName, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, bool deleteWorkingFileAfterCompletion = false, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task RefreshBatchStatusesAsync(long? batchId = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<List<AccessImportBatchDto>> GetRecentBatchStatusesAsync(int take = 20, CancellationToken ct = default)
            => Task.FromResult(new List<AccessImportBatchDto>());

        public Task<List<AccessImportBatchDto>> GetRecentBatchesAsync(int take = 20, CancellationToken ct = default)
            => Task.FromResult(new List<AccessImportBatchDto>());

        public Task<AccessImportBatchDto?> GetBatchAsync(long batchId, CancellationToken ct = default)
            => Task.FromResult<AccessImportBatchDto?>(null);

        public Task<bool> RequestCancellationAsync(long batchId, CancellationToken ct = default)
        {
            RequestCancellationCallCount++;
            return Task.FromResult(true);
        }

        public Task MarkBatchInterruptedAsync(long batchId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<DeleteBatchResult> DeleteBatchAsync(long batchId, bool includeAnalytics = true, CancellationToken ct = default)
        {
            DeleteBatchCallCount++;
            return Task.FromResult(new DeleteBatchResult
            {
                Found = true,
                BatchId = batchId,
                IncludeAnalytics = includeAnalytics
            });
        }
    }

    private sealed class RecordingAccessImportJobQueue : IAccessImportJobQueue
    {
        public int EnqueueCallCount { get; private set; }

        public Task EnqueueAsync(long batchId, CancellationToken ct = default)
        {
            EnqueueCallCount++;
            return Task.CompletedTask;
        }

        public Task<AccessImportQueuedJob?> ClaimNextAsync(CancellationToken ct = default)
            => Task.FromResult<AccessImportQueuedJob?>(null);

        public Task<AccessImportEnqueueDiagnostics> GetEnqueueDiagnosticsAsync(long batchId, CancellationToken ct = default)
            => Task.FromResult(new AccessImportEnqueueDiagnostics(
                batchId,
                Exists: true,
                CurrentStatus: "pending",
                HasSourceFilePath: true,
                HasSourceStorageKey: false,
                CancellationRequested: false,
                CompletedAtUtc: null,
                Enqueueable: true,
                Reason: "enqueueable"));

        public Task<AccessImportPendingRecoveryResult> RecoverStalePendingAsync(TimeSpan staleAfter, CancellationToken ct = default)
            => Task.FromResult(new AccessImportPendingRecoveryResult(0, 0, 0));
    }
}
