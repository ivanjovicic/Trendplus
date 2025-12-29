using System;
using System.Net;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware
{
    public class ExceptionLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionLoggingMiddleware> _logger;

        public ExceptionLoggingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionLoggingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = GetOrCreateCorrelationId(context);

            try
            {
                // Propaguj CorrelationId u response header
                context.Response.Headers["X-Correlation-ID"] = correlationId;

                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, correlationId);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex, string correlationId)
        {
            var request = context.Request;

            // 1) Log preko ILogger/Serilog
            _logger.LogError(
                ex,
                "Unhandled exception. CorrelationId={CorrelationId}, Path={Path}, Method={Method}",
                correlationId,
                request.Path,
                request.Method);

            // 2) Upis u ErrorRecords preko IErrorStore (uzmi iz request scope-a)
            var errorStore = context.RequestServices.GetService<IErrorStore>();
            if (errorStore != null)
            {
                try
                {
                    var error = new ErrorRecord
                    {
                        Timestamp = DateTime.UtcNow,
                        Message = ex.Message,
                        ExceptionType = ex.GetType().FullName ?? string.Empty,
                        StackTrace = ex.StackTrace ?? string.Empty,
                        Path = request.Path,
                        UserName = context.User?.Identity?.Name ?? "anonymous",
                        ClientApp = request.Headers["User-Agent"].ToString(),
                        CorrelationId = correlationId
                    };

                    await errorStore.SaveAsync(error);
                }
                catch (Exception storeEx)
                {
                    // Log but swallow the error - we don't want the error store to break the error handling
                    _logger.LogWarning(
                        storeEx,
                        "Failed to persist error record for CorrelationId={CorrelationId}. This might indicate missing database migrations.",
                        correlationId);
                }
            }

            // 3) JSON response prema klijentu
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var problem = new
                {
                    title = "Dogodila se greška prilikom obrade zahteva.",
                    status = context.Response.StatusCode,
                    correlationId
                };

                await context.Response.WriteAsJsonAsync(problem);
            }
        }

        private static string GetOrCreateCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValue)
                && !string.IsNullOrWhiteSpace(headerValue.ToString()))
            {
                return headerValue.ToString();
            }

            var newId = Guid.NewGuid().ToString("N");
            context.Items["CorrelationId"] = newId;
            return newId;
        }
    }
}
