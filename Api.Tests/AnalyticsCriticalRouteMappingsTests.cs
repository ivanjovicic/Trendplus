using System.Net;
using System.Text.Json;
using Api.Endpoints;
using Api.Services;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Analytics;
using Infrastructure.Services.Caching;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Threading.RateLimiting;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class AnalyticsCriticalRouteMappingsTests
{
    private static readonly HttpStatusCode[] AllowedStatuses =
    [
        HttpStatusCode.OK,
        HttpStatusCode.BadRequest,
        HttpStatusCode.ServiceUnavailable,
    ];

    [Fact]
    public async Task CriticalAnalyticsRoutes_AreRegisteredInEndpointTable()
    {
        await using var host = await AnalyticsRouteSmokeTestHost.CreateAsync();

        using var scope = host.App.Services.CreateScope();
        var routes = scope.ServiceProvider
            .GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => NormalizeRoute(endpoint.RoutePattern.RawText))
            .Where(static route => !string.IsNullOrWhiteSpace(route))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Contains("/api/analytics/refresh-status", routes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("/api/analytics/actions", routes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("/api/analytics/cached/products/decision-center", routes, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CriticalAnalyticsRoutes_AreRegisteredByProgramBootstrap()
    {
        // Keep the minimal-host smoke test above for fast handler-level wiring checks.
        // This factory-based test boots Program.cs registration with local in-memory overrides
        // so route drift in the real app composition is caught without external dependencies.
        await using var factory = await AnalyticsProgramRouteTestFactory.CreateAsync();

        using var scope = factory.Services.CreateScope();
        var routes = scope.ServiceProvider
            .GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => NormalizeRoute(endpoint.RoutePattern.RawText))
            .Where(static route => !string.IsNullOrWhiteSpace(route))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Contains("/api/analytics/refresh-status", routes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("/api/analytics/actions", routes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("/api/analytics/cached/products/decision-center", routes, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/analytics/refresh-status?dataScope=all")]
    [InlineData("/api/analytics/actions?status=new&sourceType=product&page=1&pageSize=1&dataScope=all")]
    [InlineData("/api/analytics/cached/products/decision-center?fromDate=2026-05-19&toDate=2026-06-17&top=10&dataScope=all")]
    public async Task CriticalAnalyticsFrontendUrls_DoNotReturn404(string url)
    {
        await using var host = await AnalyticsRouteSmokeTestHost.CreateAsync();
        using var response = await host.Client.GetAsync(url);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(response.StatusCode, AllowedStatuses);

        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));

        using var json = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
    }

    private static string NormalizeRoute(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        return rawText.Length > 1
            ? rawText.TrimEnd('/')
            : rawText;
    }

    private sealed class AnalyticsRouteSmokeTestHost : IAsyncDisposable
    {
        private AnalyticsRouteSmokeTestHost(WebApplication app)
        {
            App = app;
            Client = app.GetTestClient();
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<AnalyticsRouteSmokeTestHost> CreateAsync()
        {
            var trendDbName = $"trendplus-route-smoke-{Guid.NewGuid():N}";
            var analyticsDbName = $"trendplus-analytics-route-smoke-{Guid.NewGuid():N}";

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();

            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Services.AddMemoryCache();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSingleton<IMediator, NoopMediator>();
            builder.Services.AddSingleton<WorkerHealthService>();
            builder.Services.AddSingleton(new WorkerRuntimeControlService(
                initialEnabled: false,
                runtimeToggleAllowed: true,
                initialSource: "tests"));
            builder.Services.AddSingleton<IAnalyticsCacheService, InMemoryCacheService>();
            builder.Services.AddSingleton<AnalyticsCacheAdminService>();
            builder.Services.AddScoped<AnalyticsRefreshStatusService>();
            builder.Services.AddScoped<AnalyticsActionItemService>();
            builder.Services.AddDbContext<TrendplusDbContext>(options => options.UseInMemoryDatabase(trendDbName));
            builder.Services.AddScoped<ITrendplusDbContext>(sp => sp.GetRequiredService<TrendplusDbContext>());
            builder.Services.AddDbContext<AnalyticsDbContext>(options => options.UseInMemoryDatabase(analyticsDbName));
            builder.Services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AnalyticsDbContext>());
            builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy("analytics", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "analytics",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 1000,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapAnalyticsRefreshStatusEndpoints();
            app.MapAnalyticsActionsEndpoints();
            app.MapCachedAnalyticsEndpoints();

            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<TrendplusDbContext>().Database.EnsureCreated();
                scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().Database.EnsureCreated();
            }

            await app.StartAsync();
            return new AnalyticsRouteSmokeTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class AnalyticsProgramRouteTestFactory : WebApplicationFactory<global::Program>, IAsyncDisposable
    {
        private readonly string _trendDbName = $"trendplus-route-program-{Guid.NewGuid():N}";
        private readonly string _analyticsDbName = $"trendplus-analytics-route-program-{Guid.NewGuid():N}";
        private readonly string _trainingDbName = $"trendplus-training-route-program-{Guid.NewGuid():N}";

        public static async Task<AnalyticsProgramRouteTestFactory> CreateAsync()
        {
            var factory = new AnalyticsProgramRouteTestFactory();
            _ = factory.CreateClient();
            await Task.CompletedTask;
            return factory;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PROCESS_TYPE"] = "web",
                    ["Database:AutoMigrate"] = "false",
                    ["StartupReadiness:GateApiTraffic"] = "false",
                    ["ConnectionStrings:DefaultConnection"] = BuildConnectionString(_trendDbName),
                    ["ConnectionStrings:AnalyticsConnection"] = BuildConnectionString(_analyticsDbName),
                    ["ConnectionStrings:OpenProductTrainingConnection"] = BuildConnectionString(_trainingDbName)
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();
                services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
                services.RemoveAll<AnalyticsDbContext>();
                services.RemoveAll<IDbContextFactory<AnalyticsDbContext>>();
                services.RemoveAll<IAnalyticsDbContext>();
                services.RemoveAll<DbContextOptions<OpenProductTrainingDbContext>>();
                services.RemoveAll<OpenProductTrainingDbContext>();
                services.RemoveAll<IDbContextFactory<OpenProductTrainingDbContext>>();

                services.AddMemoryCache();
                services.AddDistributedMemoryCache();
                services.AddDbContextFactory<TrendplusDbContext>(options => options.UseInMemoryDatabase(_trendDbName));
                services.AddDbContext<TrendplusDbContext>(options => options.UseInMemoryDatabase(_trendDbName));
                services.AddScoped<ITrendplusDbContext>(sp => sp.GetRequiredService<TrendplusDbContext>());
                services.AddDbContextFactory<AnalyticsDbContext>(options => options.UseInMemoryDatabase(_analyticsDbName));
                services.AddDbContext<AnalyticsDbContext>(options => options.UseInMemoryDatabase(_analyticsDbName));
                services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AnalyticsDbContext>());
                services.AddDbContextFactory<OpenProductTrainingDbContext>(options => options.UseInMemoryDatabase(_trainingDbName));
                services.AddDbContext<OpenProductTrainingDbContext>(options => options.UseInMemoryDatabase(_trainingDbName));
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await Task.CompletedTask;
        }

        private static string BuildConnectionString(string databaseName)
            => $"Host=localhost;Database={databaseName};Username=test;Password=test";
    }

    private sealed class NoopMediator : IMediator
    {
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Mediator stream is not used by this smoke host.");

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Mediator stream is not used by this smoke host.");

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Mediator is not used by this smoke host.");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Mediator is not used by this smoke host.");
    }
}
