namespace NewsletterX.Host.Configuration;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NewsletterX.Host.HealthChecks;
using System.Text.Json;

/// <summary>
/// Health check configuration.
/// </summary>
public static class HealthCheckConfiguration
{
    public static IServiceCollection AddHealthChecksConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString!, 
                name: "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "postgres" })
            .AddCheck<SqsHealthCheck>(
                "sqs",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "aws", "sqs" })
            .AddCheck<DynamoDbHealthCheck>(
                "dynamodb",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "aws", "dynamodb" });

        return services;
    }

    public static IEndpointRouteBuilder MapHealthCheckEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Basic health check (liveness)
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false, // Don't run any checks
            ResponseWriter = WriteResponse
        });

        // Detailed health check (readiness)
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true, // Run all checks
            ResponseWriter = WriteResponse
        });

        // Database only
        endpoints.MapHealthChecks("/health/db", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("db"),
            ResponseWriter = WriteResponse
        });

        return endpoints;
    }

    private static async Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
    }
}

