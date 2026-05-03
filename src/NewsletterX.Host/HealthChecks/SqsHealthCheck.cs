namespace NewsletterX.Host.HealthChecks;

using Amazon.SQS;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NewsletterX.Providers.Options;

/// <summary>
/// Health check for SQS queue connectivity.
/// </summary>
public class SqsHealthCheck : IHealthCheck
{
    private readonly IAmazonSQS _sqsClient;
    private readonly SqsOptions _options;

    public SqsHealthCheck(IAmazonSQS sqsClient, IOptions<SqsOptions> options)
    {
        _sqsClient = sqsClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can get queue attributes (lightweight operation)
            var response = await _sqsClient.GetQueueAttributesAsync(
                _options.EmailSendQueueUrl, 
                new List<string> { "ApproximateNumberOfMessages" },
                cancellationToken);

            var messageCount = int.Parse(response.Attributes["ApproximateNumberOfMessages"]);

            return HealthCheckResult.Healthy($"SQS reachable. Pending messages: {messageCount}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQS connection failed.", ex);
        }
    }
}

