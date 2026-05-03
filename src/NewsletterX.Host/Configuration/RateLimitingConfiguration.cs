namespace NewsletterX.Host.Configuration;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Rate limiting configuration.
/// </summary>
/// <remarks>
/// RATE LIMITING STRATEGY:
/// ──────────────────────
/// - Fixed window: Simple, counts requests per time window
/// - Sliding window: More accurate, but slightly more complex
/// - Token bucket: Good for bursting
/// - Concurrency: Limits concurrent requests
/// 
/// We use Fixed Window for auth endpoints:
/// - Simple to understand
/// - Adequate for brute-force protection
/// - Partitioned by IP address
/// 
/// PRODUCTION CONSIDERATIONS:
/// - In-memory limiter doesn't work across multiple instances
/// - Use Redis-backed limiter for distributed scenarios
/// </remarks>
public static class RateLimitingConfiguration
{
    public static IServiceCollection AddRateLimitingPolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authConfig = configuration.GetSection("RateLimiting:Auth");
        var windowSeconds = authConfig.GetValue<int>("WindowSeconds", 60);
        var permitLimit = authConfig.GetValue<int>("PermitLimit", 10);
        var queueLimit = authConfig.GetValue<int>("QueueLimit", 0);

        services.AddRateLimiter(options =>
        {
            // Auth endpoints rate limiter
            options.AddPolicy("auth", context =>
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = queueLimit
                    });
            });

            // Global rate limiter (optional, more lenient)
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromSeconds(60),
                        QueueLimit = 0
                    });
            });

            // Custom response for rate limit exceeded
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString("0");
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    ErrorCode = "SYS_5004",
                    Message = "Too many requests. Please try again later."
                }, cancellationToken);
            };
        });

        return services;
    }
}

