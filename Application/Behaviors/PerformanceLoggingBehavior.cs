using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using MediatR;
using Microsoft.Extensions.Logging;

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
        private const int SlowRequestThresholdMs = 1000;

        public PerformanceLoggingBehavior(
            ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> logger,
            IAnalyticsDbContext analyticsDb)
        {
            _logger = logger;
            _analyticsDb = analyticsDb;
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

            try
            {
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
                stopwatch.Stop();
                var durationMs = stopwatch.ElapsedMilliseconds;

                // Log to console/file (always)
                if (durationMs > SlowRequestThresholdMs)
                {
                    _logger.LogWarning(
                        "?? SLOW REQUEST: {RequestName} took {Duration}ms",
                        requestName,
                        durationMs
                    );
                }
                else
                {
                    _logger.LogInformation(
                        "? {RequestName} completed in {Duration}ms",
                        requestName,
                        durationMs
                    );
                }

                // Log to database if slow or failed
                if (durationMs > SlowRequestThresholdMs || !isSuccess)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var performanceLog = new PerformanceLog
                            {
                                Timestamp = DateTime.UtcNow,
                                RequestType = typeof(TRequest).FullName ?? requestName,
                                RequestName = requestName,
                                DurationMs = durationMs,
                                RequestData = SerializeObject(request),
                                ResponseData = response != null ? SerializeObject(response) : null,
                                ExceptionMessage = exception?.Message,
                                IsSuccess = isSuccess
                            };

                            _analyticsDb.PerformanceLogs.Add(performanceLog);
                            await _analyticsDb.SaveChangesAsync(CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            // Use a separate logger since _logger is generic to the behavior
                            Console.WriteLine($"Failed to save performance log to database: {ex.Message}");
                        }
                    });
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
