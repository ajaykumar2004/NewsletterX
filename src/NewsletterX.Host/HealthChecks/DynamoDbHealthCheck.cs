namespace NewsletterX.Host.HealthChecks;

using Amazon.DynamoDBv2;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NewsletterX.Providers.Options;

/// <summary>
/// Health check for DynamoDB table connectivity.
/// </summary>
public class DynamoDbHealthCheck : IHealthCheck
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly DynamoDbOptions _options;

    public DynamoDbHealthCheck(IAmazonDynamoDB dynamoDb, IOptions<DynamoDbOptions> options)
    {
        _dynamoDb = dynamoDb;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _dynamoDb.DescribeTableAsync(
                _options.EmailDispatchLogTableName, 
                cancellationToken);

            var status = response.Table.TableStatus.Value;
            
            if (status == "ACTIVE")
            {
                return HealthCheckResult.Healthy($"DynamoDB table '{_options.EmailDispatchLogTableName}' is active.");
            }

            return HealthCheckResult.Degraded($"DynamoDB table status: {status}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("DynamoDB connection failed.", ex);
        }
    }
}

