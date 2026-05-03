namespace NewsletterX.Providers.Implementations;

using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using NewsletterX.Providers.Interfaces;

/// <summary>
/// Implementation of ISnsPublisher using AWS SNS.
/// </summary>
/// <remarks>
/// SNS PUBLISHING PATTERNS:
/// ───────────────────────
/// 1. Messages are JSON serialized
/// 2. Message attributes enable filtering at subscription level
/// 3. RawMessageDelivery means SQS gets the message as-is (no SNS envelope)
/// 
/// ERROR HANDLING:
/// - SDK has built-in retry with exponential backoff
/// - InvalidParameterException: Message too large (max 256KB)
/// - NotFoundException: Topic doesn't exist
/// - AuthorizationErrorException: No permission to publish
/// 
/// TRANSFERABLE PATTERN: This publisher works for any SNS topic.
/// </remarks>
public class SnsPublisher : ISnsPublisher
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly ILogger<SnsPublisher> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SnsPublisher(
        IAmazonSimpleNotificationService sns,
        ILogger<SnsPublisher> logger)
    {
        _sns = sns;
        _logger = logger;
    }

    public async Task<string> PublishAsync<T>(
        string topicArn, 
        T message, 
        CancellationToken cancellationToken = default) where T : class
    {
        return await PublishAsync(topicArn, message, new Dictionary<string, string>(), cancellationToken);
    }

    public async Task<string> PublishAsync<T>(
        string topicArn, 
        T message, 
        Dictionary<string, string> attributes,
        CancellationToken cancellationToken = default) where T : class
    {
        var messageJson = JsonSerializer.Serialize(message, JsonOptions);
        
        var request = new PublishRequest
        {
            TopicArn = topicArn,
            Message = messageJson
        };

        // Add message attributes
        foreach (var (key, value) in attributes)
        {
            request.MessageAttributes[key] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = value
            };
        }

        try
        {
            var response = await _sns.PublishAsync(request, cancellationToken);
            
            _logger.LogDebug(
                "Published message to SNS topic {TopicArn}. MessageId: {MessageId}", 
                topicArn, 
                response.MessageId);

            return response.MessageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Failed to publish message to SNS topic {TopicArn}", 
                topicArn);
            throw;
        }
    }
}

