using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Application.Config;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public sealed class RequestPerformanceLoggingMiddleware
{
    internal const string PersistenceFailureCounterName = "trendplus.http.performance.persistence.failures";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Meter Meter = new("Trendplus.Api");
    private static readonly Counter<long> PersistenceFailureCounter = Meter.CreateCounter<long>(
        PersistenceFailureCounterName,
        description: "HTTP request performance records that could not be persisted.");

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestPerformanceLoggingMiddleware> _logger;
    private readonly IOptions<PerformanceLoggingOptions> _options;
    private int _hasLoggedPersistenceFailure;

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
                RecordPersistenceFailure(persistEx, "Failed to persist HTTP request performance metric.");
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
            RequestName = Truncate(requestName, 500) ?? string.Empty,
            DurationMs = durationMs,
            RequestData = Truncate(JsonSerializer.Serialize(requestData, JsonOptions), 3900),
            ResponseData = null,
            ExceptionMessage = Truncate(exception?.GetBaseException().Message, 2000),
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
            RecordPersistenceFailure(
                exception: null,
                message: "AnalyticsDbContext is unavailable; HTTP request performance metric was not persisted.");
            return;
        }

        db.PerformanceLogs.Add(record);
        await db.SaveChangesAsync(CancellationToken.None);
        Interlocked.Exchange(ref _hasLoggedPersistenceFailure, 0);
    }

    private void RecordPersistenceFailure(Exception? exception, string message)
    {
        PersistenceFailureCounter.Add(1);

        if (Interlocked.CompareExchange(ref _hasLoggedPersistenceFailure, 1, 0) != 0)
            return;

        _logger.LogWarning(
            exception,
            "{PersistenceFailureMessage} Failures are counted by metric {MetricName}.",
            message,
            PersistenceFailureCounterName);
    }

    private static bool IsSampled(double sampleRate)
    {
        if (sampleRate <= 0d) return false;
        if (sampleRate >= 1d) return true;
        return Random.Shared.NextDouble() < sampleRate;
    }

    internal static string? Truncate(string? value, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        return maxLength <= 3
            ? value[..maxLength]
            : value[..(maxLength - 3)] + "...";
    }
}
