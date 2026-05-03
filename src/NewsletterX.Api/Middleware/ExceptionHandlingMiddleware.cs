namespace NewsletterX.Api.Middleware;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewsletterX.ContractModels.Responses;
using NewsletterX.Types.Constants;

/// <summary>
/// Global exception handling middleware.
/// </summary>
/// <remarks>
/// EXCEPTION HANDLING STRATEGY:
/// ───────────────────────────
/// - Catches ALL unhandled exceptions
/// - Logs the full exception (for debugging)
/// - Returns a sanitized error response (no stack traces to client)
/// - Uses standard ApiErrorResponse format
/// 
/// WHY MIDDLEWARE vs EXCEPTION FILTERS?
/// - Middleware catches exceptions from other middleware (not just controllers)
/// - Single place for all exception handling
/// - Can handle exceptions before authentication runs
/// 
/// SECURITY: Never expose internal details (stack trace, connection strings)
/// in production error responses.
/// </remarks>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly bool _includeDetails;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _includeDetails = environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString();
        
        _logger.LogError(
            exception,
            "Unhandled exception. CorrelationId: {CorrelationId}, Path: {Path}",
            correlationId,
            context.Request.Path);

        var (statusCode, errorCode, message) = MapException(exception);

        var response = new ApiErrorResponse
        {
            ErrorCode = errorCode,
            Message = _includeDetails ? $"{message}: {exception.Message}" : message,
            TraceId = correlationId
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static (int StatusCode, string ErrorCode, string Message) MapException(Exception exception)
    {
        return exception switch
        {
            ArgumentException => (
                (int)HttpStatusCode.BadRequest, 
                ErrorCodes.ValidationFailed, 
                "Invalid request parameters."),
            
            UnauthorizedAccessException => (
                (int)HttpStatusCode.Unauthorized, 
                ErrorCodes.TokenInvalid, 
                "Authentication required."),
            
            KeyNotFoundException => (
                (int)HttpStatusCode.NotFound, 
                ErrorCodes.UserNotFound, 
                "Resource not found."),
            
            InvalidOperationException => (
                (int)HttpStatusCode.Conflict, 
                ErrorCodes.InternalError, 
                "Operation cannot be performed."),
            
            TimeoutException => (
                (int)HttpStatusCode.GatewayTimeout, 
                ErrorCodes.ExternalServiceError, 
                "Request timed out."),
            
            _ => (
                (int)HttpStatusCode.InternalServerError, 
                ErrorCodes.InternalError, 
                "An unexpected error occurred.")
        };
    }
}

