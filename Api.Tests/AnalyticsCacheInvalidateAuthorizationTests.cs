using System.Net;
using System.Net.Http.Json;
using Application.Artikli.Common.Interfaces;
using Api.Endpoints;
using Api.Services;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using MediatR;
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
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class AnalyticsCacheInvalidateAuthorizationTests
{
    private const string AdminApiKey = "test-admin-key";

    [Fact]
    public async Task CacheInvalidate_RejectsRequestWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.PostAsync("/api/analytics/cached/cache/invalidate?family=dashboard", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(host.Cache.RemovedPrefixes);
    }

    [Fact]
    public async Task CacheInvalidate_AllowsRequestWithAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/cached/cache/invalidate?family=dashboard");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CacheInvalidateResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.Success);
        Assert.Contains(AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.DashboardFamily), host.Cache.RemovedPrefixes);
    }

    [Fact]
    public async Task CacheStatus_IsStillAvailableWithoutAdminKey()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.GetAsync("/api/analytics/cached/cache/status");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CacheStatusResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.CacheMode));
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, RecordingAnalyticsCacheService cache)
        {
            App = app;
            Client = app.GetTestClient();
            Cache = cache;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public RecordingAnalyticsCacheService Cache { get; }

        public static async Task<TestHost> CreateAsync(bool withAdminKey)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();

            var config = new Dictionary<string, string?>
            {
                ["PROCESS_TYPE"] = "worker"
            };
            if (withAdminKey)
            {
                config["Admin:ApiKey"] = AdminApiKey;
            }

            builder.Configuration.AddInMemoryCollection(config);
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddMemoryCache();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddDbContext<TrendplusDbContext>(options =>
                options.UseInMemoryDatabase($"cache-invalidate-{Guid.NewGuid():N}"));
            builder.Services.AddScoped<ITrendplusDbContext>(sp => sp.GetRequiredService<TrendplusDbContext>());
            builder.Services.AddDbContext<AnalyticsDbContext>(options =>
                options.UseInMemoryDatabase($"cache-invalidate-analytics-{Guid.NewGuid():N}"));
            builder.Services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AnalyticsDbContext>());
            builder.Services.AddSingleton<IMediator, NoopMediator>();
            builder.Services.AddSingleton<RecordingAnalyticsCacheService>();
            builder.Services.AddSingleton<IAnalyticsCacheService>(sp => sp.GetRequiredService<RecordingAnalyticsCacheService>());
            builder.Services.AddSingleton<AnalyticsCacheAdminService>();
            builder.Services.AddSingleton<ILogger<AnalyticsCacheAdminService>>(NullLogger<AnalyticsCacheAdminService>.Instance);
            builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy("analytics", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "analytics",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapCachedAnalyticsEndpoints();
            await app.StartAsync();

            return new TestHost(app, app.Services.GetRequiredService<RecordingAnalyticsCacheService>());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class RecordingAnalyticsCacheService : IAnalyticsCacheService
    {
        public List<string> RemovedPrefixes { get; } = [];

        public bool IsRedisAvailable { get; set; }
        public bool IsRedisEnabled { get; set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        {
            RemovedPrefixes.Add(prefix);
            return Task.CompletedTask;
        }

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => factory();

        public void SetRedisEnabled(bool enabled)
            => IsRedisEnabled = enabled;
    }

    private sealed record CacheInvalidateResponse(bool Success, string Message);

    private sealed record CacheStatusResponse(string Provider, string CacheMode);

    private sealed class NoopMediator : IMediator
    {
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Mediator stream is not used by this test host.");

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Mediator stream is not used by this test host.");

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Mediator is not used by this test host.");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Mediator is not used by this test host.");
    }
}
