namespace Domain.Exceptions;

/// <summary>
/// Base exception for all domain-specific exceptions
/// </summary>
public abstract class DomainException : Exception
{
    public string ErrorCode { get; }
    public IDictionary<string, object> Metadata { get; }

    protected DomainException(string message, string errorCode) 
        : base(message)
    {
        ErrorCode = errorCode;
        Metadata = new Dictionary<string, object>();
    }

    protected DomainException(string message, string errorCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Metadata = new Dictionary<string, object>();
    }

    public DomainException WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }
}

/// <summary>
/// Thrown when an entity is not found
/// </summary>
public class EntityNotFoundException : DomainException
{
    public string EntityType { get; }
    public object EntityId { get; }

    public EntityNotFoundException(string entityType, object entityId)
        : base($"{entityType} sa ID '{entityId}' nije prona?en.", "ENTITY_NOT_FOUND")
    {
        EntityType = entityType;
        EntityId = entityId;
        WithMetadata("EntityType", entityType);
        WithMetadata("EntityId", entityId);
    }
}

/// <summary>
/// Thrown when there's a business rule violation
/// </summary>
public class BusinessRuleException : DomainException
{
    public string RuleName { get; }

    public BusinessRuleException(string message, string ruleName = "BUSINESS_RULE_VIOLATION")
        : base(message, ruleName)
    {
        RuleName = ruleName;
    }
}

/// <summary>
/// Thrown when validation fails
/// </summary>
public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("Validacija nije uspela.", "VALIDATION_ERROR")
    {
        Errors = errors;
        WithMetadata("ValidationErrors", errors);
    }

    public ValidationException(string propertyName, string errorMessage)
        : base(errorMessage, "VALIDATION_ERROR")
    {
        Errors = new Dictionary<string, string[]>
        {
            { propertyName, new[] { errorMessage } }
        };
        WithMetadata("ValidationErrors", Errors);
    }
}

/// <summary>
/// Thrown when there's insufficient stock
/// </summary>
public class InsufficientStockException : DomainException
{
    public int ArtikalId { get; }
    public int RequestedQuantity { get; }
    public int AvailableQuantity { get; }

    public InsufficientStockException(int artikalId, int requested, int available)
        : base($"Nedovoljna koli?ina artikla {artikalId}. Traženo: {requested}, Dostupno: {available}", 
               "INSUFFICIENT_STOCK")
    {
        ArtikalId = artikalId;
        RequestedQuantity = requested;
        AvailableQuantity = available;
        WithMetadata("ArtikalId", artikalId);
        WithMetadata("RequestedQuantity", requested);
        WithMetadata("AvailableQuantity", available);
    }
}

/// <summary>
/// Thrown when a duplicate entity is detected
/// </summary>
public class DuplicateEntityException : DomainException
{
    public string EntityType { get; }
    public string DuplicateField { get; }
    public object DuplicateValue { get; }

    public DuplicateEntityException(string entityType, string field, object value)
        : base($"{entityType} sa {field} '{value}' ve? postoji.", "DUPLICATE_ENTITY")
    {
        EntityType = entityType;
        DuplicateField = field;
        DuplicateValue = value;
        WithMetadata("EntityType", entityType);
        WithMetadata("DuplicateField", field);
        WithMetadata("DuplicateValue", value);
    }
}

/// <summary>
/// Thrown when an operation is not allowed in current state
/// </summary>
public class InvalidOperationException : DomainException
{
    public string OperationName { get; }
    public string CurrentState { get; }

    public InvalidOperationException(string operation, string currentState, string message)
        : base(message, "INVALID_OPERATION")
    {
        OperationName = operation;
        CurrentState = currentState;
        WithMetadata("Operation", operation);
        WithMetadata("CurrentState", currentState);
    }
}

/// <summary>
/// Thrown when external service is unavailable
/// </summary>
public class ExternalServiceException : DomainException
{
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string message, Exception? innerException = null)
        : base(message, "EXTERNAL_SERVICE_ERROR", innerException!)
    {
        ServiceName = serviceName;
        WithMetadata("ServiceName", serviceName);
    }
}
