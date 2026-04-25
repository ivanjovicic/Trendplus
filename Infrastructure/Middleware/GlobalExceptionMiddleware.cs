using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Exceptions;
using Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace Infrastructure.Middleware;

/// <summary>
/// Global exception handling middleware with structured logging
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
            ?? Activity.Current?.Id 
            ?? Guid.NewGuid().ToString();
        
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request aborted by client for {Method} {Path}. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);
            return;
        }

        var response = context.Response;
        response.ContentType = "application/json";
        await PersistErrorRecordAsync(context, exception, correlationId);

        var problemDetails = new ProblemDetails
        {
            Instance = context.Request.Path,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case Domain.Exceptions.ValidationException validationEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                problemDetails.Status = 400;
                problemDetails.Title = "Validation Error";
                problemDetails.Detail = validationEx.Message;
                problemDetails.ErrorCode = validationEx.ErrorCode;
                problemDetails.Errors = validationEx.Errors;
                
                _logger.LogWarning(
                    "Validation failed for request {Path}. CorrelationId: {CorrelationId}. Errors: {@Errors}",
                    context.Request.Path,
                    correlationId,
                    validationEx.Errors);
                break;

            case EntityNotFoundException notFoundEx:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                problemDetails.Status = 404;
                problemDetails.Title = "Entity Not Found";
                problemDetails.Detail = notFoundEx.Message;
                problemDetails.ErrorCode = notFoundEx.ErrorCode;
                problemDetails.Metadata = notFoundEx.Metadata;
                
                _logger.LogWarning(
                    "Entity not found: {EntityType} with ID {EntityId}. CorrelationId: {CorrelationId}",
                    notFoundEx.EntityType,
                    notFoundEx.EntityId,
                    correlationId);
                break;

            case BusinessRuleException businessEx:
                response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                problemDetails.Status = 422;
                problemDetails.Title = "Business Rule Violation";
                problemDetails.Detail = businessEx.Message;
                problemDetails.ErrorCode = businessEx.ErrorCode;
                problemDetails.Metadata = businessEx.Metadata;
                
                _logger.LogWarning(
                    "Business rule violation: {RuleName}. Message: {Message}. CorrelationId: {CorrelationId}",
                    businessEx.RuleName,
                    businessEx.Message,
                    correlationId);
                break;

            case InsufficientStockException stockEx:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                problemDetails.Status = 409;
                problemDetails.Title = "Insufficient Stock";
                problemDetails.Detail = stockEx.Message;
                problemDetails.ErrorCode = stockEx.ErrorCode;
                problemDetails.Metadata = stockEx.Metadata;
                
                _logger.LogWarning(
                    "Insufficient stock for artikal {ArtikalId}. Requested: {Requested}, Available: {Available}. CorrelationId: {CorrelationId}",
                    stockEx.ArtikalId,
                    stockEx.RequestedQuantity,
                    stockEx.AvailableQuantity,
                    correlationId);
                break;

            case DuplicateEntityException duplicateEx:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                problemDetails.Status = 409;
                problemDetails.Title = "Duplicate Entity";
                problemDetails.Detail = duplicateEx.Message;
                problemDetails.ErrorCode = duplicateEx.ErrorCode;
                problemDetails.Metadata = duplicateEx.Metadata;
                
                _logger.LogWarning(
                    "Duplicate entity: {EntityType}.{Field} = {Value}. CorrelationId: {CorrelationId}",
                    duplicateEx.EntityType,
                    duplicateEx.DuplicateField,
                    duplicateEx.DuplicateValue,
                    correlationId);
                break;

            case ExternalServiceException serviceEx:
                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                problemDetails.Status = 503;
                problemDetails.Title = "External Service Unavailable";
                problemDetails.Detail = serviceEx.Message;
                problemDetails.ErrorCode = serviceEx.ErrorCode;
                problemDetails.Metadata = serviceEx.Metadata;
                
                _logger.LogError(
                    serviceEx,
                    "External service error: {ServiceName}. CorrelationId: {CorrelationId}",
                    serviceEx.ServiceName,
                    correlationId);
                break;

            case BrokenCircuitException circuitEx:
                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                problemDetails.Status = 503;
                problemDetails.Title = "Service Temporarily Unavailable";
                problemDetails.Detail = "Servis je privremeno nedostupan. Molimo pokušajte ponovo za nekoliko sekundi.";
                problemDetails.ErrorCode = "CIRCUIT_BREAKER_OPEN";
                
                _logger.LogWarning(
                    "Circuit breaker is open. CorrelationId: {CorrelationId}",
                    correlationId);
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails.Status = 401;
                problemDetails.Title = "Unauthorized";
                problemDetails.Detail = "Niste autorizovani za pristup ovom resursu.";
                problemDetails.ErrorCode = "UNAUTHORIZED";
                
                _logger.LogWarning(
                    "Unauthorized access attempt to {Path}. CorrelationId: {CorrelationId}",
                    context.Request.Path,
                    correlationId);
                break;

            case OperationCanceledException:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                problemDetails.Status = 408;
                problemDetails.Title = "Request Timeout";
                problemDetails.Detail = "Zahtev je istekao. Molimo pokušajte ponovo.";
                problemDetails.ErrorCode = "REQUEST_TIMEOUT";
                
                _logger.LogWarning(
                    "Request cancelled/timeout for {Path}. CorrelationId: {CorrelationId}",
                    context.Request.Path,
                    correlationId);
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                problemDetails.Status = 500;
                problemDetails.Title = "Internal Server Error";
                problemDetails.Detail = "Došlo je do neočekivane greške. Molimo kontaktirajte podršku.";
                problemDetails.ErrorCode = "INTERNAL_ERROR";

                _logger.LogError(
                    exception,
                    "Unhandled exception for request {Method} {Path}. CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    correlationId);
                break;
        }

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await response.WriteAsync(json, context.RequestAborted);
    }

    private async Task PersistErrorRecordAsync(HttpContext context, Exception exception, string correlationId)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        var errorStore = context.RequestServices.GetService<IErrorStore>();
        if (errorStore is null)
        {
            return;
        }

        try
        {
            var record = new ErrorRecord
            {
                Timestamp = DateTime.UtcNow,
                Level = ResolveErrorLevel(exception),
                Message = exception.Message,
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                StackTrace = exception.StackTrace,
                Path = context.Request.Path,
                UserName = context.User?.Identity?.Name ?? "anonymous",
                ClientApp = context.Request.Headers.UserAgent.ToString(),
                CorrelationId = correlationId
            };

            await errorStore.SaveAsync(record);
        }
        catch (Exception storeEx)
        {
            _logger.LogWarning(storeEx, "Failed to persist ErrorRecord. CorrelationId: {CorrelationId}", correlationId);
        }
    }

    private static string ResolveErrorLevel(Exception exception)
    {
        return exception switch
        {
            ValidationException => "Warning",
            EntityNotFoundException => "Warning",
            BusinessRuleException => "Warning",
            InsufficientStockException => "Warning",
            DuplicateEntityException => "Warning",
            UnauthorizedAccessException => "Warning",
            OperationCanceledException => "Warning",
            BrokenCircuitException => "Warning",
            ExternalServiceException => "Error",
            _ => "Error"
        };
    }
}

/// <summary>
/// Standard problem details response
/// </summary>
public class ProblemDetails
{
    public int Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
    public IDictionary<string, object>? Metadata { get; set; }
}
