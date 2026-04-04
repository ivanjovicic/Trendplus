using System.Diagnostics;
using Application.Common.Interfaces;
using Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Endpoints;

public static class HandledErrorLogging
{
    public static Task PersistHandledExceptionAsync(
        HttpContext context,
        Exception exception,
        string messagePrefix,
        CancellationToken ct = default)
    {
        return PersistHandledIssueAsync(
            context,
            level: "Error",
            message: $"{messagePrefix}: {exception.GetBaseException().Message}",
            exceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            stackTrace: exception.StackTrace,
            ct);
    }

    public static async Task PersistHandledIssueAsync(
        HttpContext context,
        string level,
        string message,
        string exceptionType,
        string? stackTrace,
        CancellationToken ct = default)
    {
        var errorStore = context.RequestServices.GetService<IErrorStore>();
        if (errorStore is null)
            return;

        try
        {
            var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? Activity.Current?.Id
                ?? context.TraceIdentifier;

            await errorStore.SaveAsync(new ErrorRecord
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                ExceptionType = exceptionType,
                StackTrace = stackTrace,
                Path = context.Request.Path,
                UserName = context.User?.Identity?.Name ?? "anonymous",
                ClientApp = context.Request.Headers.UserAgent.ToString(),
                CorrelationId = correlationId
            }, ct);
        }
        catch
        {
            // Never break endpoint flow because error persistence failed.
        }
    }
}
