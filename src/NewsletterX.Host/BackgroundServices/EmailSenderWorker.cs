namespace NewsletterX.Host.BackgroundServices;

using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using NewsletterX.Providers.Messages;
using NewsletterX.Providers.Options;
using NewsletterX.Repositories.Interfaces;
using NewsletterX.Types.Enums;

/// <summary>
/// Background service that consumes messages from the email send queue
/// and sends emails to subscribers.
/// </summary>
/// <remarks>
/// EMAIL SENDING STRATEGY:
/// ──────────────────────
/// In production, this would integrate with an email provider:
/// - AWS SES
/// - SendGrid
/// - Mailgun
/// 
/// For this demo, we simulate sending by:
/// 1. Logging the "send" operation
/// 2. Updating the dispatch log status in DynamoDB
/// 
/// ABSTRACTION PATTERN:
/// - IEmailSender interface (not implemented here)
/// - ConsoleEmailSender for development
/// - SesEmailSender for production
/// </remarks>
public class EmailSenderWorker : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SqsOptions _sqsOptions;
    private readonly ILogger<EmailSenderWorker> _logger;

    public EmailSenderWorker(
        IAmazonSQS sqsClient,
        IServiceScopeFactory scopeFactory,
        IOptions<SqsOptions> sqsOptions,
        ILogger<EmailSenderWorker> logger)
    {
        _sqsClient = sqsClient;
        _scopeFactory = scopeFactory;
        _sqsOptions = sqsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailSenderWorker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EmailSenderWorker. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("EmailSenderWorker stopped.");
    }

    private async Task ProcessMessagesAsync(CancellationToken stoppingToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _sqsOptions.EmailSendQueueUrl,
            MaxNumberOfMessages = _sqsOptions.MaxNumberOfMessages,
            WaitTimeSeconds = _sqsOptions.WaitTimeSeconds,
            MessageAttributeNames = new List<string> { "All" }
        };

        var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

        if (response.Messages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Received {Count} messages from email send queue", response.Messages.Count);

        foreach (var message in response.Messages)
        {
            try
            {
                await ProcessMessageAsync(message, stoppingToken);
                
                await _sqsClient.DeleteMessageAsync(
                    _sqsOptions.EmailSendQueueUrl, 
                    message.ReceiptHandle, 
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Failed to process message {MessageId}. Will be retried.",
                    message.MessageId);
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

        foreach (var subscriber in dispatchMessage.Subscribers)
        {
            try
            {
                // Simulate sending email
                await SendEmailAsync(subscriber.Email, dispatchMessage.NewsletterType, stoppingToken);

                // Update status in DynamoDB
                var sortKey = $"{dispatchMessage.Timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}#{dispatchMessage.DispatchId}_{subscriber.UserId}";
                await repository.UpdateStatusAsync(
                    dispatchMessage.NewsletterType.ToString(),
                    sortKey,
                    DispatchStatus.Sent,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Sent {NewsletterType} newsletter to {Email}",
                    dispatchMessage.NewsletterType,
                    subscriber.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send email to {Email}",
                    subscriber.Email);

                // Update status as failed
                var sortKey = $"{dispatchMessage.Timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}#{dispatchMessage.DispatchId}_{subscriber.UserId}";
                await repository.UpdateStatusAsync(
                    dispatchMessage.NewsletterType.ToString(),
                    sortKey,
                    DispatchStatus.Failed,
                    ex.Message,
                    stoppingToken);
            }
        }
    }

    /// <summary>
    /// Simulates sending an email.
    /// In production, this would call AWS SES, SendGrid, etc.
    /// </summary>
    private Task SendEmailAsync(string email, NewsletterType newsletterType, CancellationToken stoppingToken)
    {
        // Simulate email sending delay
        _logger.LogDebug("Simulating email send to {Email} for {NewsletterType}", email, newsletterType);
        
        // In production:
        // await _emailSender.SendAsync(email, subject, body, stoppingToken);
        
        return Task.CompletedTask;
    }
}

