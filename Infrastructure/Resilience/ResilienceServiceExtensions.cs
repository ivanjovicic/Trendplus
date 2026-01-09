using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

namespace Infrastructure.Resilience;

public static class ResilienceServiceExtensions
{
    /// <summary>
    /// Add resilient HTTP client with circuit breaker
    /// </summary>
    public static IServiceCollection AddResilientHttpClient<TClient>(
        this IServiceCollection services,
        string name,
        Action<HttpClient>? configureClient = null,
        int failureThreshold = 5,
        TimeSpan? breakDuration = null)
        where TClient : class
    {
        breakDuration ??= TimeSpan.FromSeconds(30);

        services.AddHttpClient<TClient>(name, client =>
        {
            configureClient?.Invoke(client);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = breakDuration.Value;
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = failureThreshold;
            
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
