using System.Diagnostics;
using System.Text.Json;
using Application.Config;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public sealed class RequestPerformanceLoggingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestPerformanceLoggingMiddleware> _logger;
    private readonly IOptions<PerformanceLoggingOptions> _options;

    public RequestPerformanceLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestPerformanceLoggingMiddleware> logger,
        IOptions<PerformanceLoggingOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path;
        var shouldCapture = path.StartsWithSegments("/api")
            && _options.Value.CaptureHttpRequests
            && IsSampled(_options.Value.HttpSampleRate);

        if (!shouldCapture)
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            try
            {
                await PersistRequestPerformanceAsync(context, stopwatch.ElapsedMilliseconds, exception);
            }
            catch (Exception persistEx)
            {
                _logger.LogWarning(persistEx, "Failed to persist HTTP request performance metric.");
            }
        }
    }

    private async Task PersistRequestPerformanceAsync(HttpContext context, long durationMs, Exception? exception)
    {
        var statusCode = exception is null
            ? context.Response.StatusCode
            : StatusCodes.Status500InternalServerError;
        var requestName = $"{context.Request.Method} {context.Request.Path.Value ?? "/"}";
        var traceId = Activity.Current?.TraceId.ToString();
        var activityId = Activity.Current?.Id;
        var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? activityId
            ?? context.TraceIdentifier;

        var requestData = new
        {
            method = context.Request.Method,
            path = context.Request.Path.Value,
            queryString = Truncate(context.Request.QueryString.Value, 1000),
            statusCode,
            traceId,
            activityId,
            correlationId,
            userName = context.User?.Identity?.Name ?? "anonymous",
            userAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 500),
            contentLength = context.Request.ContentLength,
            remoteIp = context.Connection.RemoteIpAddress?.ToString(),
            sqlExecutions = Application.Logging.RequestLogContext.Current.CapturedSqlExecutions
        };

        var record = new PerformanceLog
        {
            Timestamp = DateTime.UtcNow,
            RequestType = "HttpRequest",
            RequestName = requestName,
            DurationMs = durationMs,
            RequestData = Truncate(JsonSerializer.Serialize(requestData, JsonOptions), 3900),
            ResponseData = null,
            ExceptionMessage = exception?.GetBaseException().Message,
            IsSuccess = exception is null && statusCode < StatusCodes.Status500InternalServerError
        };

        var level = exception is not null || statusCode >= 500
            ? LogLevel.Error
            : durationMs >= Math.Max(0, _options.Value.SlowHttpRequestThresholdMs)
                ? LogLevel.Warning
                : LogLevel.Debug;

        _logger.Log(
            level,
            exception,
            "HTTP {Method} {Path} completed with {StatusCode} in {DurationMs}ms. CorrelationId={CorrelationId}",
            context.Request.Method,
            context.Request.Path.Value,
            statusCode,
            durationMs,
            correlationId);

        var db = context.RequestServices.GetService<AnalyticsDbContext>();
        if (db is null)
        {
            _logger.LogWarning("AnalyticsDbContext is unavailable; HTTP request performance metric was not persisted.");
            return;
        }

        db.PerformanceLogs.Add(record);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static bool IsSampled(double sampleRate)
    {
        if (sampleRate <= 0d) return false;
        if (sampleRate >= 1d) return true;
        return Random.Shared.NextDouble() < sampleRate;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
