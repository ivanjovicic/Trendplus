using System.Net;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Application.Common.Interfaces;
using Domain.Model;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class LogsOperationalReadsAuthorizationTests
{
    private const string AdminApiKey = "test-admin-key";

    public static TheoryData<string> OperationalReadRoutes => new()
    {
        "/errors",
        "/api/logs",
        "/api/logs/1",
    };

    [Theory]
    [MemberData(nameof(OperationalReadRoutes))]
    public async Task OperationalRead_RejectsRequestWithoutAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var response = await host.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Store.GetAllCallCount);
        Assert.Equal(0, host.Store.GetPagedCallCount);
        Assert.Equal(0, host.Store.GetByIdCallCount);
    }

    [Theory]
    [MemberData(nameof(OperationalReadRoutes))]
    public async Task OperationalRead_RejectsRequestWithWrongAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add("X-Admin-Key", "wrong-admin-key");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Store.GetAllCallCount);
        Assert.Equal(0, host.Store.GetPagedCallCount);
        Assert.Equal(0, host.Store.GetByIdCallCount);
    }

    [Theory]
    [MemberData(nameof(OperationalReadRoutes))]
    public async Task OperationalRead_AllowsRequestWithAdminKey(string route)
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected 200/404 for authorized '{route}', got {(int)response.StatusCode}.");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Errors_WithAdminKey_ReturnsStorePayload()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);
        host.Store.Seed(new ErrorRecord
        {
            Id = 7,
            Timestamp = DateTime.UtcNow,
            Level = "Error",
            Message = "seeded",
            ExceptionType = "InvalidOperationException",
            CorrelationId = "corr-7"
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/errors");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<ErrorRecord>>();
        Assert.NotNull(payload);
        Assert.Single(payload!);
        Assert.Equal(7, payload[0].Id);
        Assert.Equal(1, host.Store.GetAllCallCount);
    }

    [Fact]
    public async Task LogsList_WithAdminKey_ReturnsEmptySuccessPayload()
    {
        await using var host = await TestHost.CreateAsync(withAdminKey: true);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs?pageNumber=1&pageSize=50");
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LogsPageResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload!.TotalCount);
        Assert.NotNull(payload.Logs);
        Assert.Empty(payload.Logs);
        Assert.Equal(1, host.Store.GetPagedCallCount);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, RecordingErrorStore store)
        {
            App = app;
            Client = app.GetTestClient();
            Store = store;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public RecordingErrorStore Store { get; }

        public static async Task<TestHost> CreateAsync(bool withAdminKey)
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
                options.AddPolicy("db-heavy", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "db-heavy",
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

            var store = new RecordingErrorStore();
            builder.Services.AddSingleton<IErrorStore>(store);
            builder.Services.AddSingleton<IAnalyticsCacheService, NoopAnalyticsCacheService>();
            builder.Services.AddSingleton<ILogger<Program>>(NullLogger<Program>.Instance);

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapLogsAndErrorsReadEndpoints();
            await app.StartAsync();
            return new TestHost(app, store);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class RecordingErrorStore : IErrorStore
    {
        private readonly List<ErrorRecord> _records = [];

        public int GetAllCallCount { get; private set; }
        public int GetPagedCallCount { get; private set; }
        public int GetByIdCallCount { get; private set; }

        public void Seed(ErrorRecord record) => _records.Add(record);

        public Task<IReadOnlyList<ErrorRecord>> GetAllAsync(
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default)
        {
            GetAllCallCount++;
            return Task.FromResult<IReadOnlyList<ErrorRecord>>(_records.ToList());
        }

        public Task<int> GetCountAsync(
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_records.Count);

        public Task<IReadOnlyList<ErrorRecord>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default)
        {
            GetPagedCallCount++;
            return Task.FromResult<IReadOnlyList<ErrorRecord>>(_records.ToList());
        }

        public Task<ErrorRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(_records.FirstOrDefault(record => record.Id == id));
        }

        public Task SaveAsync(ErrorRecord error, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopAnalyticsCacheService : IAnalyticsCacheService
    {
        public bool IsRedisAvailable => false;
        public bool IsRedisEnabled { get; private set; }

        public CacheFootprintSnapshot GetFootprintSnapshot()
            => new("disabled", false, false, 0);

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => factory();

        public void SetRedisEnabled(bool enabled)
            => IsRedisEnabled = enabled;
    }
}
