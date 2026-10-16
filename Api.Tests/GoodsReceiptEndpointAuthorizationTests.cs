using System.Net;
using System.Text;
using Application.Artikli.Common.Interfaces;
using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class GoodsReceiptEndpointAuthorizationTests
{
    private const string AdminKey = "goods-receipt-test-key";
    private const string OperatorKey = "named-operator-only-test-key-over-32-characters";

    [Fact]
    public async Task ReceiptReadAndPostRoutes_RejectMissingOrWrongAdminCredential()
    {
        await using var host = await CreateHostAsync();

        using var missingRead = await host.Client.GetAsync("/api/goods-receipts?page=1&pageSize=20");
        using var wrongReadRequest = new HttpRequestMessage(HttpMethod.Get, "/api/goods-receipts/articles?storeId=1");
        wrongReadRequest.Headers.Add("X-Admin-Key", "wrong-key");
        using var wrongRead = await host.Client.SendAsync(wrongReadRequest);
        using var missingPost = await host.Client.PostAsync("/api/goods-receipts", new StringContent("{}", Encoding.UTF8, "application/json"));
        using var wrongPostRequest = new HttpRequestMessage(HttpMethod.Post, "/api/goods-receipts")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        wrongPostRequest.Headers.Add("X-Admin-Key", "wrong-key");
        using var wrongPost = await host.Client.SendAsync(wrongPostRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, missingRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRead.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missingPost.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongPost.StatusCode);
    }

    [Fact]
    public async Task ReceiptPostRoute_AcceptsAdminCredentialAndReturnsServerValidation()
    {
        await using var host = await CreateHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/goods-receipts")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Admin-Key", AdminKey);
        request.Headers.Add("X-Receipt-Operator-Id", "stock-clerk-1");
        request.Headers.Add("X-Receipt-Operator-Key", OperatorKey);

        using var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("missing_request_key", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReceiptPostRoute_SharedAdminKeyAloneNeverAuthorizesWrite()
    {
        await using var host = await CreateHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/goods-receipts")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Admin-Key", AdminKey);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiptPostRoute_DisabledFlagReturnsNotFoundEvenForNamedOperator()
    {
        await using var host = await CreateHostAsync(writesEnabled: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/goods-receipts")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Admin-Key", AdminKey);
        request.Headers.Add("X-Receipt-Operator-Id", "stock-clerk-1");
        request.Headers.Add("X-Receipt-Operator-Key", OperatorKey);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReceiptPostRoute_UnknownOrIncorrectOperatorIsForbidden()
    {
        await using var host = await CreateHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/goods-receipts")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Admin-Key", AdminKey);
        request.Headers.Add("X-Receipt-Operator-Id", "stock-clerk-1");
        request.Headers.Add("X-Receipt-Operator-Key", "wrong-operator-key-12345678901234567890");
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task StornoRoute_RequiresBothAdminAndNamedOperatorAndDoesNotReachDatabaseWhenDenied()
    {
        await using var host = await CreateHostAsync();

        using var withoutAdmin = new HttpRequestMessage(HttpMethod.Post, "/api/goods-receipts/81/reverse")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        withoutAdmin.Headers.Add("X-Receipt-Operator-Id", "stock-clerk-1");
        withoutAdmin.Headers.Add("X-Receipt-Operator-Key", OperatorKey);
        using var first = await host.Client.SendAsync(withoutAdmin);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);

        using var sharedOnly = new HttpRequestMessage(HttpMethod.Post, "/api/goods-receipts/81/reverse")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        sharedOnly.Headers.Add("X-Admin-Key", AdminKey);
        using var second = await host.Client.SendAsync(sharedOnly);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        using var authorized = new HttpRequestMessage(HttpMethod.Post, "/api/goods-receipts/81/reverse")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        authorized.Headers.Add("X-Admin-Key", AdminKey);
        authorized.Headers.Add("X-Receipt-Operator-Id", "stock-clerk-1");
        authorized.Headers.Add("X-Receipt-Operator-Key", OperatorKey);
        using var third = await host.Client.SendAsync(authorized);
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Contains("invalid_reversal_request", await third.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static async Task<TestHost> CreateHostAsync(bool writesEnabled = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:ApiKey"] = AdminKey,
            ["GoodsReceipts:WritesEnabled"] = writesEnabled.ToString(),
            ["GoodsReceipts:Operators:stock-clerk-1:Enabled"] = "true",
            ["GoodsReceipts:Operators:stock-clerk-1:Key"] = OperatorKey
        });
        builder.Services.AddLogging();
        builder.Services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(
            "Host=127.0.0.1;Database=not-used;Username=not-used;Password=not-used;Timeout=1"));
        builder.Services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(
            "Host=127.0.0.1;Database=not-used;Username=not-used;Password=not-used;Timeout=1"));
        builder.Services.AddScoped<ITrendplusDbContext>(services => services.GetRequiredService<TrendplusDbContext>());
        builder.Services.AddScoped<IAnalyticsDbContext>(services => services.GetRequiredService<AnalyticsDbContext>());
        builder.Services.AddScoped<GoodsReceiptService>();
        builder.Services.AddRateLimiter(options => options.AddPolicy("strict", _ =>
            RateLimitPartition.GetFixedWindowLimiter("goods-receipt-auth", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            })));

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapGoodsReceiptEndpoints();
        await app.StartAsync();
        return new TestHost(app, app.GetTestClient());
    }

    private sealed class TestHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
