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
        private readonly IErrorStore _errorStore;

        public ExceptionLoggingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionLoggingMiddleware> logger,
            IErrorStore errorStore)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorStore = errorStore ?? throw new ArgumentNullException(nameof(errorStore));
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = GetOrCreateCorrelationId(context);

            try
            {
                // Propaguj CorrelationId u response header da ga front vidi
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

            // Log preko Serilog/ILogger sa strukturiranim podacima
            _logger.LogError(
                ex,
                "Unhandled exception. Path: {Path}, Method: {Method}, CorrelationId: {CorrelationId}",
                request.Path,
                request.Method,
                correlationId);

            // Upis u DB (ErrorRecords)
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
                    // ako imaš kolonu CorrelationId u ErrorRecord:
                    CorrelationId = correlationId
                };

                await _errorStore.SaveAsync(error);
            }
            catch (Exception storeEx)
            {
                // fallback log ako DB logging fail‑uje
                _logger.LogError(storeEx, "Failed to persist error record for CorrelationId {CorrelationId}", correlationId);
            }

            // Standardizovan error response
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

        private static string GetOrCreateCorrelationId(HttpContext context)
        {
            // Pokušaj da pročitaš iz request headera, inače generiši novi
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
