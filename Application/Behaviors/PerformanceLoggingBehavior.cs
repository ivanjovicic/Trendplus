using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using Application.Config;
using Application.Logging;
using Domain.Model;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Behaviors
{
    /// <summary>
    /// Performance logging behavior that tracks execution time of all MediatR requests
    /// and logs slow operations (>1000ms) to the database.
    /// </summary>
    public class PerformanceLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> _logger;
        private readonly IAnalyticsDbContext _analyticsDb;
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
        private readonly IOptions<PerformanceLoggingOptions> _options;
        private const int SlowRequestThresholdMs = 1000;

        public PerformanceLoggingBehavior(
            ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> logger,
            IAnalyticsDbContext analyticsDb,
            Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
            IOptions<PerformanceLoggingOptions> options)
        {
            _logger = logger;
            _analyticsDb = analyticsDb;
            _httpContextAccessor = httpContextAccessor;
            _options = options;
        }

        public async Task<TResponse> Handle(
    TRequest request,
    CancellationToken cancellationToken,
    RequestHandlerDelegate<TResponse> next)
        {
            var requestName = typeof(TRequest).Name;
            var stopwatch = Stopwatch.StartNew();
            TResponse? response = default;
            Exception? exception = null;
            bool isSuccess = true;
            var createdLocalContext = false;

            try
            {
                // Fallback context initialization when request middleware is not active.
                var ctx = RequestLogContext.Current;
                if (string.IsNullOrWhiteSpace(ctx.RequestId) && string.IsNullOrWhiteSpace(ctx.TraceId))
                {
                    createdLocalContext = true;

                    var httpCtx = _httpContextAccessor.HttpContext;
                    ctx.RequestId = httpCtx?.TraceIdentifier ?? Activity.Current?.Id;
                    ctx.TraceId = Activity.Current?.Id;

                    var cfg = _options.Value;
                    if (cfg?.CaptureSql == true)
                    {
                        var sample = cfg.SampleRate;
                        if (sample <= 0d) ctx.ShouldCaptureSql = false;
                        else if (sample >= 1d) ctx.ShouldCaptureSql = true;
                        else ctx.ShouldCaptureSql = Random.Shared.NextDouble() < sample;
                    }
                    else
                    {
                        ctx.ShouldCaptureSql = false;
                    }

                    if (cfg is not null)
                    {
                        ctx.MaxQueryLength = cfg.MaxQueryLength;
                    }
                }

                response = await next();
                return response;
            }
            catch (Exception ex)
            {
                exception = ex;
                isSuccess = false;
                throw;
            }
            finally
            {
                var capturedSqlExecutions = RequestLogContext.Current.CapturedSqlExecutions;

                if (createdLocalContext)
                {
                    RequestLogContext.Current = new RequestLogContext();
                }
                stopwatch.Stop();
                var durationMs = stopwatch.ElapsedMilliseconds;

                // Log to console/file (always)
                if (durationMs > SlowRequestThresholdMs)
                {
                    _logger.LogWarning(
                        "SLOW REQUEST: {RequestName} took {Duration}ms",
                        requestName,
                        durationMs
                    );
                }
                else
                {
                    _logger.LogInformation(
                        "{RequestName} completed in {Duration}ms",
                        requestName,
                        durationMs
                    );
                }

                // Log to database if slow or failed
                if (durationMs > SlowRequestThresholdMs || !isSuccess)
                {
                    try
                    {
                        var performanceLog = new PerformanceLog
                        {
                            Timestamp = DateTime.UtcNow,
                            RequestType = typeof(TRequest).FullName ?? requestName,
                            RequestName = requestName,
                            DurationMs = durationMs,
                            RequestData = SerializeObject(new
                            {
                                request,
                                sqlExecutions = capturedSqlExecutions
                            }),
                            ResponseData = response != null ? SerializeObject(response) : null,
                            ExceptionMessage = exception?.Message,
                            IsSuccess = isSuccess
                        };

                        _analyticsDb.PerformanceLogs.Add(performanceLog);
                        await _analyticsDb.SaveChangesAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save performance log for {RequestName}", requestName);
                    }
                }
            }
        }

        private string? SerializeObject(object? obj)
        {
            if (obj == null) return null;

            try
            {
                var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Truncate if too long
                return json.Length > 3900 ? json.Substring(0, 3900) + "..." : json;
            }
            catch
            {
                return obj.ToString();
            }
        }
    }
}
