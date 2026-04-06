using System.Diagnostics;
using Application.Config;
using Application.Logging;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public sealed class SqlLoggingRequestContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<PerformanceLoggingOptions> _options;

    public SqlLoggingRequestContextMiddleware(
        RequestDelegate next,
        IOptions<PerformanceLoggingOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task Invoke(HttpContext context)
    {
        var cfg = _options.Value ?? new PerformanceLoggingOptions();
        RequestLogContext.Current = new RequestLogContext
        {
            RequestId = context.TraceIdentifier ?? Activity.Current?.Id,
            TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
            ShouldCaptureSql = cfg.CaptureSql && ShouldSample(cfg.SampleRate),
            MaxQueryLength = cfg.MaxQueryLength
        };

        try
        {
            await _next(context);
        }
        finally
        {
            RequestLogContext.Current = new RequestLogContext();
        }
    }

    private static bool ShouldSample(double sampleRate)
    {
        if (sampleRate <= 0d) return false;
        if (sampleRate >= 1d) return true;
        return Random.Shared.NextDouble() < sampleRate;
    }
}
