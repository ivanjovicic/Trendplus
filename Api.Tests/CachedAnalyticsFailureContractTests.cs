using System.Net;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class CachedAnalyticsFailureContractTests
{
    [Fact]
    public async Task InventoryBalance_UnexpectedFailureReturnsStableErrorPayload()
    {
        await using var factory = new FailureFactory(new InvalidOperationException("cache factory failed"));
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/balance?storeId=4", "failure-balance");

        Assert.Equal(0, root.GetProperty("totalSku").GetInt32());
        Assert.Equal(0, root.GetProperty("totalOnHand").GetInt32());
        Assert.Equal(0, root.GetProperty("lowStockCount").GetInt32());
        Assert.Equal(0, root.GetProperty("outOfStockCount").GetInt32());
        Assert.Equal(0m, root.GetProperty("estimatedInventoryValue").GetDecimal());
        AssertErrorMeta(root.GetProperty("meta"), "inventory_cached_balance_error", "failure-balance");
    }

    [Fact]
    public async Task InventoryList_UnexpectedFailurePreservesNormalizedPaginationAndEmptyShape()
    {
        await using var factory = new FailureFactory(new InvalidOperationException("inventory list failed"));
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/inventory/list?page=0&pageSize=5000&search=model",
            "failure-list");

        Assert.Equal(1, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(1000, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
        Assert.Empty(root.GetProperty("items").EnumerateArray());
        AssertErrorMeta(root.GetProperty("meta"), "inventory_cached_list_error", "failure-list");
    }

    [Fact]
    public async Task InventoryInsights_UnexpectedFailureReturnsAllCollectionsEmptyWithErrorMeta()
    {
        await using var factory = new FailureFactory(new InvalidOperationException("inventory insights failed"));
        var root = await GetJsonAsync(factory, "/api/analytics/cached/inventory/insights", "failure-insights");

        Assert.Equal(0, root.GetProperty("totalItems").GetInt32());
        Assert.Equal(0m, root.GetProperty("totalEstimatedValue").GetDecimal());
        Assert.Empty(root.GetProperty("aging").EnumerateArray());
        Assert.Empty(root.GetProperty("abc").EnumerateArray());
        Assert.Empty(root.GetProperty("topAgedItems").EnumerateArray());
        Assert.Empty(root.GetProperty("topCapitalLockedItems").EnumerateArray());
        AssertErrorMeta(root.GetProperty("meta"), "inventory_cached_insights_error", "failure-insights");
    }

    [Fact]
    public async Task TopProducts_TimeoutReturnsExplicitTimeoutCodeInsteadOfGenericFailure()
    {
        await using var factory = new FailureFactory(new TimeoutException("analytics query timeout"));
        var root = await GetJsonAsync(
            factory,
            "/api/analytics/cached/sales/top-products?fromDate=2026-06-01&toDate=2026-06-30&top=5",
            "failure-timeout");

        Assert.Empty(root.GetProperty("byRevenue").EnumerateArray());
        Assert.Empty(root.GetProperty("byUnits").EnumerateArray());
        AssertErrorMeta(root.GetProperty("meta"), "sql_timeout", "failure-timeout");
    }

    [Fact]
    public async Task FailureResponsesGenerateCorrelationIdWhenCallerDoesNotProvideOne()
    {
        await using var factory = new FailureFactory(new InvalidOperationException("failure without request id"));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/analytics/cached/inventory/balance");
        var root = await ReadJsonAsync(response);

        var meta = root.GetProperty("meta");
        Assert.False(meta.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(meta.GetProperty("correlationId").GetString()));
    }

    private static void AssertErrorMeta(JsonElement meta, string expectedErrorCode, string expectedCorrelationId)
    {
        Assert.False(meta.GetProperty("success").GetBoolean());
        Assert.False(meta.GetProperty("isPartial").GetBoolean());
        Assert.Equal(expectedErrorCode, meta.GetProperty("errorCode").GetString());
        Assert.Equal(expectedCorrelationId, meta.GetProperty("correlationId").GetString());
        Assert.Equal("insufficient_data", meta.GetProperty("dataQualityStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(meta.GetProperty("errorMessage").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(meta.GetProperty("message").GetString()));
    }

    private static async Task<JsonElement> GetJsonAsync(
        WebApplicationFactory<global::Program> factory,
        string url,
        string correlationId)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Correlation-ID", correlationId);
        using var response = await client.SendAsync(request);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body));
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed class FailureFactory : WebApplicationFactory<global::Program>
    {
        private readonly Exception _exception;
        private readonly string _databaseName = $"cached-analytics-failure-{Guid.NewGuid():N}";

        public FailureFactory(Exception exception)
        {
            _exception = exception;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnalyticsCacheService>();
                services.AddSingleton<IAnalyticsCacheService>(new ThrowingAnalyticsCacheService(_exception));

                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();

                services.AddDbContextFactory<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.AddDbContext<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.AddScoped<ITrendplusDbContext>(sp => sp.GetRequiredService<TrendplusDbContext>());
            });
        }
    }

    private sealed class ThrowingAnalyticsCacheService : IAnalyticsCacheService
    {
        private readonly Exception _exception;

        public ThrowingAnalyticsCacheService(Exception exception)
        {
            _exception = exception;
        }

        public bool IsRedisAvailable => false;
        public bool IsRedisEnabled => false;

        public CacheFootprintSnapshot GetFootprintSnapshot()
            => new("disabled", false, false, 0);

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class =>
            Task.FromException<T?>(_exception);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class =>
            Task.FromException(_exception);

        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;

        public Task<T> GetOrSetAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? expiration = null,
            CancellationToken ct = default) where T : class =>
            Task.FromException<T>(_exception);

        public void SetRedisEnabled(bool enabled)
        {
        }
    }
}
