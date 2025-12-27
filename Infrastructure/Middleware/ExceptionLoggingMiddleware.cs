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
        private readonly IServiceScopeFactory _scopeFactory;

        public ExceptionLoggingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionLoggingMiddleware> logger,
            IServiceScopeFactory scopeFactory)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = GetOrCreateCorrelationId(context);

            try
            {
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

            _logger.LogError(
                ex,
                "Unhandled exception. Path: {Path}, Method: {Method}, CorrelationId: {CorrelationId}",
                request.Path,
                request.Method,
                correlationId);

            // Create a scope to resolve IErrorStore
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var errorStore = scope.ServiceProvider.GetRequiredService<IErrorStore>();
                
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
                _logger.LogError(storeEx, "Failed to persist error record for CorrelationId {CorrelationId}", correlationId);
            }

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
