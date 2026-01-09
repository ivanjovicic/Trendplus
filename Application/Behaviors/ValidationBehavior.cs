using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates requests using FluentValidation
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        var requestName = typeof(TRequest).Name;

        if (!_validators.Any())
        {
            _logger.LogDebug("No validators found for {RequestName}", requestName);
            return await next();
        }

        _logger.LogDebug(
            "Validating {RequestName} with {ValidatorCount} validator(s)",
            requestName,
            _validators.Count());

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
        {
            var errors = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => f.ErrorMessage).ToArray());

            _logger.LogWarning(
                "Validation failed for {RequestName}. Errors: {@Errors}",
                requestName,
                errors);

            throw new Domain.Exceptions.ValidationException(errors);
        }

        _logger.LogDebug("Validation passed for {RequestName}", requestName);
        return await next();
    }
}
