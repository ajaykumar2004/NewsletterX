namespace NewsletterX.Api.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Health check controller for monitoring and container orchestration.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Why a dedicated Health Controller?
/// ─────────────────────────────────────────────────────────
/// - Container orchestrators (ECS, K8s) need health endpoints
/// - Load balancers need to know if instance is healthy
/// - Monitoring systems need a lightweight endpoint to poll
/// 
/// BEST PRACTICE: Health checks should be FAST and NOT hit external dependencies
/// in the basic check. Use /health/ready for dependency checks.
/// </remarks>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Basic liveness check - confirms the application is running.
    /// </summary>
    /// <returns>200 OK if the application is responsive.</returns>
    /// <remarks>
    /// This endpoint:
    /// - Does NOT check database connectivity
    /// - Does NOT check external services
    /// - Only confirms the application is alive and can handle requests
    /// 
    /// Use case: Container liveness probe, load balancer health check
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });
    }
}

/// <summary>
/// Response model for health check endpoints.
/// </summary>
public class HealthResponse
{
    public string Status { get; init; } = "Healthy";
    public DateTime Timestamp { get; init; }
    public string Version { get; init; } = "1.0.0";
}

