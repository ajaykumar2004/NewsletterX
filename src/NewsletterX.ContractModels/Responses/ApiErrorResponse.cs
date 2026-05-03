namespace NewsletterX.ContractModels.Responses;

/// <summary>
/// Standard error response model.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Consistent Error Format
/// ───────────────────────────────────────────────
/// All errors return this format for consistent client handling:
/// - ErrorCode: Machine-readable code for programmatic handling
/// - Message: Human-readable description
/// - Details: Optional additional context
/// - TraceId: For support/debugging
/// 
/// TRANSFERABLE PATTERN: This error format works for any API.
/// Consider RFC 7807 (Problem Details) for more standardization.
/// </remarks>
public class ApiErrorResponse
{
    /// <summary>
    /// Machine-readable error code.
    /// </summary>
    /// <example>AUTH_1001</example>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    /// <example>Invalid email or password</example>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional error details (e.g., validation errors).
    /// </summary>
    public Dictionary<string, string[]>? Details { get; set; }

    /// <summary>
    /// Trace ID for debugging/support.
    /// </summary>
    /// <example>00-abc123-def456-00</example>
    public string? TraceId { get; set; }

    /// <summary>
    /// Creates an error response with a code and message.
    /// </summary>
    public static ApiErrorResponse Create(string errorCode, string message, string? traceId = null)
    {
        return new ApiErrorResponse
        {
            ErrorCode = errorCode,
            Message = message,
            TraceId = traceId
        };
    }

    /// <summary>
    /// Creates a validation error response with field-specific errors.
    /// </summary>
    public static ApiErrorResponse Validation(Dictionary<string, string[]> errors, string? traceId = null)
    {
        return new ApiErrorResponse
        {
            ErrorCode = "VAL_2001",
            Message = "One or more validation errors occurred.",
            Details = errors,
            TraceId = traceId
        };
    }
}

