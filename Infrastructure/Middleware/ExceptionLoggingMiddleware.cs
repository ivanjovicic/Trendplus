using System;
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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExceptionLoggingMiddleware> _logger;

        public ExceptionLoggingMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, ILogger<ExceptionLoggingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var record = new ErrorRecord
                {
                    Timestamp = DateTime.UtcNow,
                    Message = ex.Message,
                    ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                    StackTrace = ex.StackTrace,
                    Path = context.Request?.Path,
                    ClientApp = context.Request?.Headers["User-Agent"].ToString(),
                    UserName = context.User?.Identity?.Name
                };

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var store = scope.ServiceProvider.GetRequiredService<IErrorStore>();
                    await store.LogAsync(record).ConfigureAwait(false);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to log exception to DB-backed store");
                }

                _logger.LogError(ex, "Unhandled exception for {Path}", context.Request?.Path);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/problem+json";
                var problem = new { title = "An unexpected error occurred.", detail = ex.Message };
                await context.Response.WriteAsJsonAsync(problem).ConfigureAwait(false);
            }
        }
    }
}
