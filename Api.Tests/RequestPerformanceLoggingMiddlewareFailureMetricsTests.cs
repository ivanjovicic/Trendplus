using System.Diagnostics.Metrics;
using Api.Middleware;
using Application.Config;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class RequestPerformanceLoggingMiddlewareFailureMetricsTests
{
    [Fact]
    public async Task PersistenceFailures_AreCountedAndWarningsAreThrottledUntilPersistenceRecovers()
    {
        long failureCount = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "Trendplus.Api"
                && instrument.Name == RequestPerformanceLoggingMiddleware.PersistenceFailureCounterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == RequestPerformanceLoggingMiddleware.PersistenceFailureCounterName)
                Interlocked.Add(ref failureCount, measurement);
        });
        listener.Start();

        var logger = new WarningCountingLogger<RequestPerformanceLoggingMiddleware>();
        var requestCount = 0;
        var middleware = new RequestPerformanceLoggingMiddleware(
            _ =>
            {
                Interlocked.Increment(ref requestCount);
                return Task.CompletedTask;
            },
            logger,
            Options.Create(new PerformanceLoggingOptions { CaptureHttpRequests = true }));
        var dbOptions = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase($"request-performance-failure-{Guid.NewGuid():N}")
            .Options;
        await using var db = new FailingAnalyticsDbContext(dbOptions);
        using var services = new ServiceCollection()
            .AddSingleton<AnalyticsDbContext>(db)
            .BuildServiceProvider();

        await middleware.Invoke(CreateRequestContext(services));
        await middleware.Invoke(CreateRequestContext(services));

        var recoveredDbOptions = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase($"request-performance-recovery-{Guid.NewGuid():N}")
            .Options;
        await using var recoveredDb = new AnalyticsDbContext(recoveredDbOptions);
        using var recoveredServices = new ServiceCollection()
            .AddSingleton<AnalyticsDbContext>(recoveredDb)
            .BuildServiceProvider();
        await middleware.Invoke(CreateRequestContext(recoveredServices));
        await middleware.Invoke(CreateRequestContext(services));

        Assert.True(Interlocked.Read(ref failureCount) >= 3);
        Assert.Equal(2, logger.WarningCount);
        Assert.Equal(4, requestCount);
    }

    private static DefaultHttpContext CreateRequestContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/metrics-test";
        return context;
    }

    private sealed class FailingAnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
        : AnalyticsDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromException<int>(new InvalidOperationException("Persistence failure fixture."));
    }

    private sealed class WarningCountingLogger<T> : ILogger<T>
    {
        private int _warningCount;

        public int WarningCount => Volatile.Read(ref _warningCount);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning
                && formatter(state, exception).Contains("persist", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _warningCount);
            }
        }
    }
}
