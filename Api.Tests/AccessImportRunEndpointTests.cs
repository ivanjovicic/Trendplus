using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.RateLimiting;
using Api.Models;
using Api.Services;
using Api.Services.Access;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportRunEndpointTests
{
    private const string AdminApiKey = "test-admin-key";

    [Fact]
    public async Task OptionsRun_ReturnsQuickly_AndDoesNotTriggerImportService()
    {
        var service = new RecordingAccessImportService();
        await using var host = await AccessImportRunTestHost.CreateAsync(service);

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/access-import/run");
        request.Headers.Add("Origin", "https://trendplus.vercel.app");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-admin-key");

        var stopwatch = Stopwatch.StartNew();
        using var response = await host.Client.SendAsync(request);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, service.StartImportCallCount);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Expected fast preflight but took {stopwatch.ElapsedMilliseconds}ms.");
        Assert.Contains("https://trendplus.vercel.app", response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task PostRun_ReturnsAccepted_AndInvokesImportServiceOnce()
    {
        var service = new RecordingAccessImportService();
        await using var host = await AccessImportRunTestHost.CreateAsync(service, withAdminKey: true);

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "sample.accdb");
        content.Add(new StringContent("true"), "includeAnalytics");
        content.Add(new StringContent("false"), "overwriteExisting");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/run")
        {
            Content = content
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, service.StartImportCallCount);
        Assert.True(service.FileExistedWhenCalled);
        Assert.Equal(4, service.LastObservedFileSizeBytes);
        Assert.Equal("/api/access-import/batches/123", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task PostRun_RejectsRequestWithoutAdminKey()
    {
        var service = new RecordingAccessImportService();
        await using var host = await AccessImportRunTestHost.CreateAsync(service, withAdminKey: true);

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "sample.accdb");

        using var response = await host.Client.PostAsync("/api/access-import/run", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, service.StartImportCallCount);
    }

    [Fact]
    public async Task PostRun_RejectsRequestWithWrongAdminKey()
    {
        var service = new RecordingAccessImportService();
        await using var host = await AccessImportRunTestHost.CreateAsync(service, withAdminKey: true);

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "sample.accdb");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/run")
        {
            Content = content
        };
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, service.StartImportCallCount);
    }

    [Fact]
    public async Task PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout()
    {
        var service = new RecordingAccessImportService
        {
            StartImportHandler = (_, _, _, _, _) => throw new TimeoutException(
                "Access import source upload timed out after 1 seconds.",
                new OperationCanceledException("The operation was canceled."))
        };
        await using var host = await AccessImportRunTestHost.CreateAsync(service, withAdminKey: true);

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", "sample.accdb");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/access-import/run")
        {
            Content = content
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(1, service.StartImportCallCount);

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var body = await JsonDocument.ParseAsync(responseStream);
        var detail = body.RootElement.GetProperty("detail").GetString();
        Assert.Contains("Access import source upload timed out after 1 seconds.", detail);
        Assert.DoesNotContain("The operation was canceled.", detail);
    }

    private sealed class RecordingAccessImportService : IAccessImportService
    {
        public int StartImportCallCount { get; private set; }
        public bool FileExistedWhenCalled { get; private set; }
        public long? LastObservedFileSizeBytes { get; private set; }
        public Func<string, bool, bool, bool, CancellationToken, Task<AccessImportRunResponse>>? StartImportHandler { get; init; }

        public Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<AccessImportRunResponse> ImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default)
            => throw new NotSupportedException();

        public async Task<AccessImportRunResponse> StartImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default)
        {
            StartImportCallCount++;
            FileExistedWhenCalled = File.Exists(accessFilePath);
            LastObservedFileSizeBytes = FileExistedWhenCalled ? new FileInfo(accessFilePath).Length : null;

            if (StartImportHandler is not null)
                return await StartImportHandler(accessFilePath, includeAnalytics, overwriteExisting, includeTemporaryTables, ct);

            return new AccessImportRunResponse
            {
                BatchId = 123,
                Status = "pending",
                SourceFileName = Path.GetFileName(accessFilePath),
                IncludeAnalytics = includeAnalytics,
                StartedAtUtc = DateTime.UtcNow
            };
        }

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
            => Task.FromResult(false);

        public Task MarkBatchInterruptedAsync(long batchId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<DeleteBatchResult> DeleteBatchAsync(long batchId, bool includeAnalytics = true, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class AccessImportRunTestHost : IAsyncDisposable
    {
        private AccessImportRunTestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<AccessImportRunTestHost> CreateAsync(IAccessImportService service, bool withAdminKey = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();

            var config = new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = "web"
            };
            if (withAdminKey)
            {
                config["Admin:ApiKey"] = AdminApiKey;
            }

            builder.Configuration.AddInMemoryCollection(config);
            builder.Services.AddLogging();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton(service);
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy
                        .WithOrigins("https://trendplus.vercel.app")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
            builder.Services.AddRateLimiter(options =>
            {
                foreach (var policyName in new[] { "writes", "fixed", "db-heavy" })
                {
                    options.AddPolicy(policyName, _ =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: policyName,
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 1000,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));
                }
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseCors("AllowFrontend");
            app.UseRateLimiter();

            var group = app.MapGroup("/api/access-import");
            group.MapMethods("/run", new[] { HttpMethods.Options }, () => Results.NoContent());
            group.MapPost("/run", async (
                HttpRequest request,
                HttpContext httpContext,
                IConfiguration configuration,
                IHostEnvironment environment,
                IAccessImportService importService,
                ILogger<Program> logger,
                CancellationToken ct = default) =>
                await AccessImportEndpoints.StartAccessImportJobAsync(request, httpContext, configuration, environment, importService, logger, ct));
            await app.StartAsync();

            return new AccessImportRunTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}
