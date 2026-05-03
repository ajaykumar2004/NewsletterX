namespace NewsletterX.Host.BackgroundServices;

using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using NewsletterX.EntityModels.DynamoDB;
using NewsletterX.Providers.Messages;
using NewsletterX.Providers.Options;
using NewsletterX.Repositories.Interfaces;
using NewsletterX.Types.Enums;

/// <summary>
/// Background service that consumes messages from the audit log queue
/// and writes dispatch logs to DynamoDB.
/// </summary>
/// <remarks>
/// SQS CONSUMER PATTERNS:
/// ─────────────────────
/// 1. Long polling (WaitTimeSeconds > 0) reduces empty responses
/// 2. Batch receive (MaxNumberOfMessages up to 10) improves throughput
/// 3. Visibility timeout prevents duplicate processing
/// 4. Delete message only AFTER successful processing
/// 5. Graceful shutdown via CancellationToken
/// 
/// IDEMPOTENCY:
/// - DynamoDB uses conditional writes to prevent duplicates
/// - DispatchId in message is the idempotency key
/// - Same message received twice = only one DynamoDB record
/// 
/// ERROR HANDLING:
/// - Exceptions leave message visible (will be retried)
/// - After maxReceiveCount failures, moves to DLQ
/// - Monitor DLQ for operational issues
/// </remarks>
public class AuditLogWorker : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SqsOptions _sqsOptions;
    private readonly DynamoDbOptions _dynamoDbOptions;
    private readonly ILogger<AuditLogWorker> _logger;

    public AuditLogWorker(
        IAmazonSQS sqsClient,
        IServiceScopeFactory scopeFactory,
        IOptions<SqsOptions> sqsOptions,
        IOptions<DynamoDbOptions> dynamoDbOptions,
        ILogger<AuditLogWorker> logger)
    {
        _sqsClient = sqsClient;
        _scopeFactory = scopeFactory;
        _sqsOptions = sqsOptions.Value;
        _dynamoDbOptions = dynamoDbOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuditLogWorker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuditLogWorker. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("AuditLogWorker stopped.");
    }

    private async Task ProcessMessagesAsync(CancellationToken stoppingToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _sqsOptions.AuditLogQueueUrl,
            MaxNumberOfMessages = _sqsOptions.MaxNumberOfMessages,
            WaitTimeSeconds = _sqsOptions.WaitTimeSeconds,
            MessageAttributeNames = new List<string> { "All" }
        };

        var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

        if (response.Messages.Count == 0)
        {
            return; // No messages, long poll will wait
        }

        _logger.LogDebug("Received {Count} messages from audit log queue", response.Messages.Count);

        foreach (var message in response.Messages)
        {
            try
            {
                await ProcessMessageAsync(message, stoppingToken);
                
                // Delete message after successful processing
                await _sqsClient.DeleteMessageAsync(
                    _sqsOptions.AuditLogQueueUrl, 
                    message.ReceiptHandle, 
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Failed to process message {MessageId}. Will be retried.",
                    message.MessageId);
                // Don't delete - message will become visible again after visibility timeout
            }
        }
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        var dispatchMessage = JsonSerializer.Deserialize<NewsletterDispatchMessage>(
            message.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (dispatchMessage == null)
        {
            _logger.LogWarning("Failed to deserialize message {MessageId}", message.MessageId);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEmailDispatchLogRepository>();

        var logEntries = dispatchMessage.Subscribers.Select(subscriber =>
        {
            var timestamp = dispatchMessage.Timestamp;
            return new EmailDispatchLogEntity
            {
                NewsletterType = dispatchMessage.NewsletterType.ToString(),
                DispatchTimestamp = EmailDispatchLogEntity.CreateSortKey(timestamp, $"{dispatchMessage.DispatchId}_{subscriber.UserId}"),
                DispatchId = dispatchMessage.DispatchId,
                UserId = subscriber.UserId.ToString(),
                Email = subscriber.Email,
                Status = DispatchStatus.Pending.ToString(),
                AttemptCount = 1,
                CorrelationId = dispatchMessage.CorrelationId,
                ExpirationTime = EmailDispatchLogEntity.CalculateExpirationTime(timestamp, _dynamoDbOptions.TtlDays),
                CreatedAt = DateTime.UtcNow.ToString("O")
            };
        }).ToList();

        await repository.LogDispatchBatchAsync(logEntries, stoppingToken);

        _logger.LogInformation(
            "Logged {Count} dispatch entries for {NewsletterType}. DispatchId: {DispatchId}",
            logEntries.Count,
            dispatchMessage.NewsletterType,
            dispatchMessage.DispatchId);
    }
}

