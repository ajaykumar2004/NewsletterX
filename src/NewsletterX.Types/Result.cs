namespace NewsletterX.Types;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Result Pattern vs Exceptions
/// ────────────────────────────────────────────────────
/// Use Result&lt;T&gt; for EXPECTED failures (validation, business rules).
/// Use Exceptions for UNEXPECTED failures (database down, null reference).
/// 
/// Why Result Pattern?
/// - Makes failure explicit in the return type
/// - Forces caller to handle failure case
/// - No performance cost of exception throwing
/// - Better for flow control (not exceptional circumstances)
/// 
/// TRANSFERABLE PATTERN: This Result type works for any project.
/// Consider using a library like FluentResults for more features.
/// </remarks>
/// <typeparam name="T">The type of the success value.</typeparam>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, null, null);

    /// <summary>
    /// Creates a failed result with an error code and message.
    /// </summary>
    public static Result<T> Failure(string errorCode, string errorMessage) 
        => new(false, default, errorCode, errorMessage);

    /// <summary>
    /// Maps the success value to a new type.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess 
            ? Result<TNew>.Success(mapper(Value!)) 
            : Result<TNew>.Failure(ErrorCode!, ErrorMessage!);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess) action(Value!);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<string, string> action)
    {
        if (IsFailure) action(ErrorCode!, ErrorMessage!);
        return this;
    }
}

/// <summary>
/// Result type for operations that don't return a value.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string errorCode, string errorMessage) 
        => new(false, errorCode, errorMessage);

    /// <summary>
    /// Converts to a Result&lt;T&gt; with the given value if successful.
    /// </summary>
    public Result<T> WithValue<T>(T value)
    {
        return IsSuccess 
            ? Result<T>.Success(value) 
            : Result<T>.Failure(ErrorCode!, ErrorMessage!);
    }
}

