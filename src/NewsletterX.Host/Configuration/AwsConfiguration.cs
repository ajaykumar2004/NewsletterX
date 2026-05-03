namespace NewsletterX.Host.Configuration;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

/// <summary>
/// AWS services configuration.
/// </summary>
/// <remarks>
/// CLIENT REGISTRATION PATTERN:
/// ───────────────────────────
/// All AWS SDK clients are registered as SINGLETONS:
/// - They manage internal HTTP connection pools
/// - Thread-safe by design
/// - Creating per-request = socket exhaustion
/// 
/// LOCALSTACK CONFIGURATION:
/// - ServiceURL points to LocalStack (http://localhost:4566)
/// - Credentials are ignored by LocalStack but required by SDK
/// </remarks>
public static class AwsConfiguration
{
    public static IServiceCollection AddAwsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var awsSection = configuration.GetSection("AWS");
        var serviceUrl = awsSection["ServiceURL"];
        var region = awsSection["Region"] ?? "us-east-1";
        var useHttp = bool.TryParse(awsSection["UseHttp"], out var http) && http;

        // DynamoDB Client
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var config = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            };

            if (!string.IsNullOrEmpty(serviceUrl))
            {
                config.ServiceURL = serviceUrl;
                if (useHttp)
                {
                    config.UseHttp = true;
                }
            }

            return new AmazonDynamoDBClient(config);
        });

        // SNS Client
        services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var config = new AmazonSimpleNotificationServiceConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            };

            if (!string.IsNullOrEmpty(serviceUrl))
            {
                config.ServiceURL = serviceUrl;
                if (useHttp)
                {
                    config.UseHttp = true;
                }
            }

            return new AmazonSimpleNotificationServiceClient(config);
        });

        // SQS Client
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var config = new AmazonSQSConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            };

            if (!string.IsNullOrEmpty(serviceUrl))
            {
                config.ServiceURL = serviceUrl;
                if (useHttp)
                {
                    config.UseHttp = true;
                }
            }

            return new AmazonSQSClient(config);
        });

        return services;
    }
}

