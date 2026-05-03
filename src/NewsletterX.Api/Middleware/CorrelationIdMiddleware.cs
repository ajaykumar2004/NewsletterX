namespace NewsletterX.Api.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NewsletterX.Types.Constants;
using Serilog.Context;

/// <summary>
/// Middleware to extract or generate correlation IDs for request tracing.
/// </summary>
/// <remarks>
/// CORRELATION ID PATTERN:
/// ──────────────────────
/// - Unique ID that follows a request through the entire system
/// - Passed via HTTP header: X-Correlation-Id
/// - Logged with every log message (via Serilog LogContext)
/// - Passed to downstream services and message queues
/// 
/// FLOW:
/// 1. Check for incoming X-Correlation-Id header
/// 2. If missing, generate a new GUID
/// 3. Add to Serilog LogContext (all logs include it)
/// 4. Add to response headers (for client-side debugging)
/// 5. Store in HttpContext.Items (for use in controllers/services)
/// 
/// TRANSFERABLE PATTERN: This middleware works for any distributed system.
/// Essential for debugging issues across services.
/// </remarks>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next, 
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or generate correlation ID
        var correlationId = context.Request.Headers[AppConstants.CorrelationIdHeader].FirstOrDefault();
        
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N")[..12]; // Short ID for readability
        }

        // Store in HttpContext for use in controllers/services
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[AppConstants.CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Push to Serilog context - all logs in this request will include this
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}

/// <summary>
/// Extension methods for correlation ID.
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Gets the correlation ID from the current HTTP context.
    /// </summary>
    public static string GetCorrelationId(this HttpContext context)
    {
        return context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N")[..12];
    }
}

