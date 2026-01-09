using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Trendplus.POS.Middleware
{
    public class PosRequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PosRequestLoggingMiddleware> _logger;

        public PosRequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<PosRequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestPath = context.Request.Path.Value ?? "";

            // Only log POS sale endpoints
            if (!requestPath.StartsWith("/pos/", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Log request
            var terminalId = context.Request.Headers["X-Terminal-Id"].ToString();
            var correlationId = Guid.NewGuid().ToString();

            _logger.LogInformation(
                "POS Request started: {Method} {Path} - Terminal: {TerminalId}, CorrelationId: {CorrelationId}",
                context.Request.Method,
                requestPath,
                terminalId,
                correlationId);

            // Read request body
            context.Request.EnableBuffering();
            var requestBody = await ReadRequestBodyAsync(context.Request);

            try
            {
                await _next(context);

                stopwatch.Stop();

                _logger.LogInformation(
                    "POS Request completed: {Method} {Path} - Status: {StatusCode}, Duration: {Duration}ms, Terminal: {TerminalId}, CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    requestPath,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    terminalId,
                    correlationId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "POS Request failed: {Method} {Path} - Duration: {Duration}ms, Terminal: {TerminalId}, CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    requestPath,
                    stopwatch.ElapsedMilliseconds,
                    terminalId,
                    correlationId);

                throw;
            }
        }

        private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }
    }
}
