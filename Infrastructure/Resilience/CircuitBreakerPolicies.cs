using System;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Infrastructure.Resilience;

/// <summary>
/// Circuit Breaker policies for resilient operations
/// </summary>
public static class CircuitBreakerPolicies
{
    /// <summary>
    /// Create a circuit breaker pipeline for HTTP requests
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpPipeline(
        ILogger? logger = null,
        int failureThreshold = 5,
        TimeSpan? breakDuration = null,
        TimeSpan? timeout = null)
    {
        breakDuration ??= TimeSpan.FromSeconds(30);
        timeout ??= TimeSpan.FromSeconds(15);

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            // Timeout
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout.Value,
                OnTimeout = args =>
                {
                    logger?.LogWarning(
                        "?? Request timeout after {Timeout}s",
                        timeout.Value.TotalSeconds);
                    return default;
                }
            })
            // Retry
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => (int)r.StatusCode >= 500),
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        "?? Retry attempt {Attempt} after {Delay}ms - {Exception}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? $"Status: {args.Outcome.Result?.StatusCode}");
                    return default;
                }
            })
            // Circuit Breaker
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = failureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = breakDuration.Value,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => (int)r.StatusCode >= 500),
                OnOpened = args =>
                {
                    logger?.LogError(
                        "?? Circuit OPENED - Breaking for {Duration}s",
                        breakDuration.Value.TotalSeconds);
                    return default;
                },
                OnClosed = _ =>
                {
                    logger?.LogInformation("?? Circuit CLOSED - Recovered");
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    logger?.LogWarning("?? Circuit HALF-OPEN - Testing");
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Create a circuit breaker pipeline for general async operations
    /// </summary>
    public static ResiliencePipeline CreateAsyncPipeline(
        ILogger? logger = null,
        string name = "default",
        int failureThreshold = 5,
        TimeSpan? breakDuration = null)
    {
        breakDuration ??= TimeSpan.FromSeconds(30);

        return new ResiliencePipelineBuilder()
            // Retry
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        "?? [{Name}] Retry attempt {Attempt} - {Exception}",
                        name,
                        args.AttemptNumber,
                        args.Outcome.Exception?.Message);
                    return default;
                }
            })
            // Circuit Breaker
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = failureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = breakDuration.Value,
                OnOpened = args =>
                {
                    logger?.LogError(
                        "?? [{Name}] Circuit OPENED - Breaking for {Duration}s",
                        name,
                        breakDuration.Value.TotalSeconds);
                    return default;
                },
                OnClosed = _ =>
                {
                    logger?.LogInformation("?? [{Name}] Circuit CLOSED", name);
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    logger?.LogWarning("?? [{Name}] Circuit HALF-OPEN", name);
                    return default;
                }
            })
            .Build();
    }
}
