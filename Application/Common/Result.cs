namespace Application.Common;

/// <summary>
/// Represents the result of an operation that can either succeed or fail
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }
    public IDictionary<string, string[]>? ValidationErrors { get; }

    protected Result(bool isSuccess, string? errorMessage = null, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    protected Result(IDictionary<string, string[]> validationErrors)
    {
        IsSuccess = false;
        ErrorMessage = "Validation failed";
        ErrorCode = "VALIDATION_ERROR";
        ValidationErrors = validationErrors;
    }

    public static Result Success() => new(true);
    
    public static Result Failure(string errorMessage, string? errorCode = null) 
        => new(false, errorMessage, errorCode);
    
    public static Result ValidationFailure(IDictionary<string, string[]> errors) 
        => new(errors);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    
    public static Result<T> Failure<T>(string errorMessage, string? errorCode = null) 
        => Result<T>.Failure(errorMessage, errorCode);
}

/// <summary>
/// Represents the result of an operation that returns a value
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(T value) : base(true)
    {
        Value = value;
    }

    private Result(string errorMessage, string? errorCode) : base(false, errorMessage, errorCode)
    {
        Value = default;
    }

    private Result(IDictionary<string, string[]> validationErrors) : base(validationErrors)
    {
        Value = default;
    }

    public static Result<T> Success(T value) => new(value);
    
    public static new Result<T> Failure(string errorMessage, string? errorCode = null) 
        => new(errorMessage, errorCode);
    
    public static new Result<T> ValidationFailure(IDictionary<string, string[]> errors) 
        => new(errors);

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess && Value != null
            ? Result<TNew>.Success(mapper(Value))
            : Result<TNew>.Failure(ErrorMessage!, ErrorCode);
    }

    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        return IsSuccess && Value != null
            ? Result<TNew>.Success(await mapper(Value))
            : Result<TNew>.Failure(ErrorMessage!, ErrorCode);
    }
}
