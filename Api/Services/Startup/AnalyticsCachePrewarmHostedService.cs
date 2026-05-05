using System.Diagnostics;
using System.Globalization;

namespace Api.Services.Startup;

public sealed class AnalyticsCachePrewarmHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AnalyticsCachePrewarmHostedService> _logger;

    public AnalyticsCachePrewarmHostedService(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AnalyticsCachePrewarmHostedService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool?>("AnalyticsPrewarm:Enabled") ?? !_environment.IsDevelopment();
        if (!enabled)
        {
            _logger.LogInformation("Analytics cache prewarm is disabled.");
            return;
        }

        var initialDelaySeconds = Math.Max(0, _configuration.GetValue<int?>("AnalyticsPrewarm:InitialDelaySeconds") ?? 12);
        var requestTimeoutSeconds = Math.Max(5, _configuration.GetValue<int?>("AnalyticsPrewarm:RequestTimeoutSeconds") ?? 45);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var baseUri = ResolveBaseUri();
        if (baseUri is null)
        {
            _logger.LogWarning("Analytics cache prewarm skipped because local base URL could not be resolved.");
            return;
        }

        var todayUtc = DateTime.UtcNow.Date;
        var fromUtc = todayUtc.AddDays(-29);
        var toUtc = todayUtc.AddDays(1).AddSeconds(-1);
        var previousToUtc = fromUtc.AddSeconds(-1);
        var previousFromUtc = previousToUtc.Date.AddDays(-29);
        var from = Uri.EscapeDataString(FormatUtc(fromUtc));
        var to = Uri.EscapeDataString(FormatUtc(toUtc));
        var previousFrom = Uri.EscapeDataString(FormatUtc(previousFromUtc));
        var previousTo = Uri.EscapeDataString(FormatUtc(previousToUtc));

        var paths = new[]
        {
            "/api/analytics/cached/filters/stores?prewarm=1",
            $"/api/analytics/cached/dashboard/bootstrap?fromDate={from}&toDate={to}&dataScope=all&prewarm=1",
            $"/api/analytics/supplier-sales-stats?fromDate={from}&toDate={to}&dataScope=all&prewarm=1",
            $"/api/analytics/shoe-type-sales-stats?fromDate={from}&toDate={to}&dataScope=all&prewarm=1",
            $"/api/analytics/daily-sales?fromDate={from}&toDate={to}&dataScope=all&prewarm=1",
            $"/api/analytics/daily-sales?fromDate={previousFrom}&toDate={previousTo}&dataScope=all&prewarm=1"
        };

        _logger.LogInformation(
            "Analytics cache prewarm starting. BaseUrl={BaseUrl} From={FromDate} To={ToDate} PathCount={PathCount}",
            baseUri,
            fromUtc,
            toUtc,
            paths.Length);

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds)
        };

        foreach (var path in paths)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await WarmPathAsync(httpClient, baseUri, path, stoppingToken);
        }
    }

    private async Task WarmPathAsync(HttpClient httpClient, Uri baseUri, string path, CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestUri = new Uri(baseUri, path.TrimStart('/'));

        try
        {
            using var response = await httpClient.GetAsync(requestUri, stoppingToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Analytics cache prewarm succeeded for {Path} in {ElapsedMs}ms. StatusCode={StatusCode}",
                    requestUri.PathAndQuery,
                    stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }
            else
            {
                _logger.LogWarning(
                    "Analytics cache prewarm returned non-success for {Path} in {ElapsedMs}ms. StatusCode={StatusCode}",
                    requestUri.PathAndQuery,
                    stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Analytics cache prewarm failed for {Path} after {ElapsedMs}ms.",
                requestUri.PathAndQuery,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private Uri? ResolveBaseUri()
    {
        var configuredBaseUrl = _configuration["AnalyticsPrewarm:BaseUrl"];
        if (TryCreateBaseUri(configuredBaseUrl, out var configuredUri))
        {
            return configuredUri;
        }

        var urls = _configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        if (!string.IsNullOrWhiteSpace(urls))
        {
            foreach (var rawUrl in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var localUrl = rawUrl
                    .Replace("0.0.0.0", "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    .Replace("[::]", "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    .Replace("*", "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    .Replace("+", "127.0.0.1", StringComparison.OrdinalIgnoreCase);

                if (TryCreateBaseUri(localUrl, out var uri))
                {
                    return uri;
                }
            }
        }

        var port = Environment.GetEnvironmentVariable("PORT");
        return TryCreateBaseUri($"http://127.0.0.1:{(string.IsNullOrWhiteSpace(port) ? "8080" : port)}", out var fallbackUri)
            ? fallbackUri
            : null;
    }

    private static bool TryCreateBaseUri(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            string.IsNullOrWhiteSpace(parsed.Scheme) ||
            string.IsNullOrWhiteSpace(parsed.Host))
        {
            return false;
        }

        uri = parsed.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? parsed
            : new Uri(parsed.AbsoluteUri + "/");
        return true;
    }

    private static string FormatUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
