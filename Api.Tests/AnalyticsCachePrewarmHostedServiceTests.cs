using System.Net;
using Api.Services.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsCachePrewarmHostedServiceTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public async Task RunPrewarmOnceAsync_WhenLocalApiNeverBecomesReady_LogsSingleSkipWarningAndDoesNotWarmPaths()
    {
        var requests = new List<string>();
        var handler = new DelegateHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!.PathAndQuery);
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused"));
        });

        var logs = new TestLogCollector();
        var service = CreateService(
            new Dictionary<string, string?>
            {
                ["AnalyticsPrewarm:Enabled"] = "true",
                ["AnalyticsPrewarm:BaseUrl"] = "http://127.0.0.1:8080/",
                ["AnalyticsPrewarm:InitialDelaySeconds"] = "0",
                ["AnalyticsPrewarm:WaitForApplicationStartedSeconds"] = "0",
                ["AnalyticsPrewarm:MaxStartupProbeAttempts"] = "2",
                ["AnalyticsPrewarm:StartupProbeDelaySeconds"] = "1",
                ["AnalyticsPrewarm:RequestTimeoutSeconds"] = "5",
            },
            logs);

        await service.RunPrewarmOnceAsync(
            CancellationToken.None,
            handler,
            static (_, _) => Task.CompletedTask);

        Assert.Equal(2, requests.Count);
        Assert.All(requests, path => Assert.Equal("/ready", path));
        Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(
            logs.Entries,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains("Analytics cache prewarm skipped because local API was not ready after 2 attempts.", StringComparison.Ordinal));
        Assert.DoesNotContain(
            logs.Entries,
            entry => entry.Message.Contains("Analytics cache prewarm failed for /api/analytics/", StringComparison.Ordinal));
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task RunPrewarmOnceAsync_WhenReadyProbeEventuallySucceeds_WarmsAnalyticsPaths()
    {
        var requests = new List<string>();
        var readyAttempts = 0;
        var handler = new DelegateHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            requests.Add(path);

            if (path == "/ready")
            {
                readyAttempts++;
                return new HttpResponseMessage(readyAttempts == 1
                    ? HttpStatusCode.ServiceUnavailable
                    : HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var logs = new TestLogCollector();
        var service = CreateService(
            new Dictionary<string, string?>
            {
                ["AnalyticsPrewarm:Enabled"] = "true",
                ["AnalyticsPrewarm:BaseUrl"] = "http://127.0.0.1:8080/",
                ["AnalyticsPrewarm:InitialDelaySeconds"] = "0",
                ["AnalyticsPrewarm:WaitForApplicationStartedSeconds"] = "0",
                ["AnalyticsPrewarm:MaxStartupProbeAttempts"] = "3",
                ["AnalyticsPrewarm:StartupProbeDelaySeconds"] = "1",
                ["AnalyticsPrewarm:RequestTimeoutSeconds"] = "5",
            },
            logs);

        await service.RunPrewarmOnceAsync(
            CancellationToken.None,
            handler,
            static (_, _) => Task.CompletedTask);

        Assert.True(requests.Count > 2);
        Assert.Equal("/ready", requests[0]);
        Assert.Equal("/ready", requests[1]);
        Assert.Contains(requests, path => path.StartsWith("/api/analytics/cached/dashboard/bootstrap?", StringComparison.Ordinal));
        Assert.Contains(
            logs.Entries,
            entry => entry.Level == LogLevel.Information
                && entry.Message.Contains("Analytics cache prewarm starting.", StringComparison.Ordinal));
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task WaitForLocalApiReadyAsync_FallsBackToHealth_WhenReadyEndpointIsUnavailable()
    {
        var requests = new List<string>();
        var handler = new DelegateHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            requests.Add(path);

            return Task.FromResult(new HttpResponseMessage(path == "/ready"
                ? HttpStatusCode.NotFound
                : HttpStatusCode.OK));
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8080/")
        };

        var result = await AnalyticsCachePrewarmHostedService.WaitForLocalApiReadyAsync(
            client,
            client.BaseAddress!,
            maxAttempts: 2,
            attemptDelay: TimeSpan.FromMilliseconds(1),
            delayAsync: static (_, _) => Task.CompletedTask,
            stoppingToken: CancellationToken.None);

        Assert.True(result.Ready);
        Assert.Equal("/health", result.ProbePath);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal(new[] { "/ready", "/health" }, requests);
    }

    private static AnalyticsCachePrewarmHostedService CreateService(
        IDictionary<string, string?> settings,
        TestLogCollector logs)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new AnalyticsCachePrewarmHostedService(
            configuration,
            new FakeHostEnvironment(),
            logs.CreateLogger<AnalyticsCachePrewarmHostedService>(),
            new FakeHostApplicationLifetime());
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = request => Task.FromResult(handler(request));
        }

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Trendplus";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopped.Token;
        public void StopApplication() => _stopping.Cancel();
    }

    private sealed class TestLogCollector
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public ILogger<T> CreateLogger<T>() => new CollectorLogger<T>(Entries);
    }

    private sealed class CollectorLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel Level, string Message)> _entries;

        public CollectorLogger(List<(LogLevel Level, string Message)> entries)
        {
            _entries = entries;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}
