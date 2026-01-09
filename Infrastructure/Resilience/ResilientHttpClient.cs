using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;

namespace Infrastructure.Resilience;

/// <summary>
/// HTTP client with built-in circuit breaker and retry logic
/// </summary>
public class ResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<ResilientHttpClient> _logger;

    public ResilientHttpClient(
        HttpClient httpClient,
        ILogger<ResilientHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _pipeline = CircuitBreakerPolicies.CreateHttpPipeline(
            logger: _logger,
            failureThreshold: 5,
            breakDuration: TimeSpan.FromSeconds(30));
    }

    public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await _httpClient.GetAsync(requestUri, token),
            ct);
    }

    public async Task<HttpResponseMessage> PostAsync(
        string requestUri,
        HttpContent content,
        CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await _httpClient.PostAsync(requestUri, content, token),
            ct);
    }

    public async Task<HttpResponseMessage> PutAsync(
        string requestUri,
        HttpContent content,
        CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await _httpClient.PutAsync(requestUri, content, token),
            ct);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await _httpClient.DeleteAsync(requestUri, token),
            ct);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await _httpClient.SendAsync(request, token),
            ct);
    }
}
