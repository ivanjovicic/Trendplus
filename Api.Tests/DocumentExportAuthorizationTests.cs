using System.Net;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Application.Documents.Interfaces;
using Application.Documents.Models;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class DocumentExportAuthorizationTests
{
    private const string AdminApiKey = "test-admin-key";

    public static TheoryData<string> GenerateRoutes => new()
    {
        "/api/documents/generate",
        "/api/documents/print-preview",
    };

    [Theory]
    [MemberData(nameof(GenerateRoutes))]
    public async Task Generate_RejectsRequestWithoutAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.PostAsJsonAsync(route, NewGenerateBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Documents.GenerateCallCount);
    }

    [Theory]
    [MemberData(nameof(GenerateRoutes))]
    public async Task Generate_RejectsRequestWithWrongAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(NewGenerateBody())
        };
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Documents.GenerateCallCount);
    }

    [Theory]
    [MemberData(nameof(GenerateRoutes))]
    public async Task Generate_RejectsSpoofedUserHeadersWithoutAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(NewGenerateBody())
        };
        request.Headers.Add("X-User-Id", "attacker");
        request.Headers.Add("X-User-Name", "Attacker");
        request.Headers.Add("X-User-Roles", "Admin,AnalyticsExport");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Documents.GenerateCallCount);
    }

    [Theory]
    [MemberData(nameof(GenerateRoutes))]
    public async Task Generate_AllowsRequestWithAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(NewGenerateBody())
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success for authorized '{route}', got {(int)response.StatusCode}.");
        Assert.Equal(1, host.Documents.GenerateCallCount);
        Assert.Contains(host.Documents.LastRoles, role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExportList_RejectsSpoofedAdminRoleHeader()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/exports");
        request.Headers.Add("X-User-Roles", "Admin");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static object NewGenerateBody() => new
    {
        tableKey = "sales",
        tableTitle = "Sales",
        columns = Array.Empty<object>(),
        rows = Array.Empty<object>()
    };

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, RecordingDocumentService documents)
        {
            App = app;
            Client = app.GetTestClient();
            Documents = documents;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public RecordingDocumentService Documents { get; }

        public static async Task<TestHost> CreateAsync(bool withAdminKey)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<TrendplusDbContext>(options =>
                options.UseInMemoryDatabase($"document-export-auth-{Guid.NewGuid():N}"));
            builder.Services.Configure<DocumentExportOptions>(options =>
            {
                options.SigningKey = "unit-test-key";
                options.SignedUrlTtlMinutes = 5;
            });
            builder.Services.AddRateLimiter(options =>
            {
                foreach (var policyName in new[] { "writes", "fixed" })
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

            var documents = new RecordingDocumentService();
            builder.Services.AddSingleton<IDocumentService>(documents);
            builder.Services.AddSingleton<IDocumentUserContextAccessor, DocumentUserContextAccessor>();
            builder.Services.AddSingleton<IDocumentAccessControlService, DocumentAccessControlService>();
            builder.Services.AddSingleton<IDocumentDownloadTokenService>(sp =>
                new DocumentDownloadTokenService(
                    sp.GetRequiredService<IOptions<DocumentExportOptions>>(),
                    sp.GetRequiredService<IHostEnvironment>(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentDownloadTokenService>.Instance));

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapDocumentEndpoints();
            await app.StartAsync();
            return new TestHost(app, documents);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class RecordingDocumentService : IDocumentService
    {
        public int GenerateCallCount { get; private set; }
        public string[] LastRoles { get; private set; } = [];

        public Task<DocumentGenerateResult> GenerateAsync(
            DocumentGenerationRequest request,
            DocumentExecutionContext executionContext,
            CancellationToken ct = default)
        {
            GenerateCallCount++;
            LastRoles = executionContext.Roles;
            var documentId = Guid.NewGuid();
            return Task.FromResult(new DocumentGenerateResult
            {
                DocumentId = documentId,
                Status = "completed",
                IsAsync = false,
                CreatedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = DateTime.UtcNow
            });
        }

        public Task<DocumentBatchResult> EnqueueBatchAsync(
            DocumentBatchRequest request,
            DocumentExecutionContext executionContext,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<DocumentStatusResult?> GetStatusAsync(
            Guid documentId,
            DocumentExecutionContext executionContext,
            CancellationToken ct = default)
            => Task.FromResult<DocumentStatusResult?>(null);

        public Task<DocumentStreamResult?> OpenDownloadAsync(
            Guid documentId,
            DocumentExecutionContext executionContext,
            string? signedToken,
            CancellationToken ct = default)
            => Task.FromResult<DocumentStreamResult?>(null);

        public Task<string?> GetPrintHtmlAsync(
            Guid documentId,
            DocumentExecutionContext executionContext,
            string? signedToken,
            CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task ProcessQueuedDocumentAsync(Guid documentId, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
