namespace NewsletterX.Providers.Implementations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsletterX.Providers.Interfaces;
using NewsletterX.Providers.Messages;
using NewsletterX.Providers.Options;
using NewsletterX.Repositories.Interfaces;
using NewsletterX.Types;
using NewsletterX.Types.Constants;
using NewsletterX.Types.Enums;

/// <summary>
/// Implementation of INewsletterDispatchProvider.
/// </summary>
/// <remarks>
/// DISPATCH FLOW:
/// ─────────────
/// 1. Get all active subscribers for the newsletter type (paginated)
/// 2. Batch subscribers into groups (e.g., 100 per message)
/// 3. Publish each batch to SNS
/// 4. SNS fans out to email-send-queue and audit-log-queue
/// 
/// WHY BATCH?
/// - Reduces number of SNS publishes (cost)
/// - Single message per batch = easier idempotency
/// - Batch size balances throughput vs message size
/// 
/// IDEMPOTENCY:
/// - DispatchId is unique per batch
/// - Consumers use DispatchId to detect duplicates
/// </remarks>
public class NewsletterDispatchProvider : INewsletterDispatchProvider
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ISnsPublisher _snsPublisher;
    private readonly SnsOptions _snsOptions;
    private readonly ILogger<NewsletterDispatchProvider> _logger;
    private const int BatchSize = 100; // Subscribers per SNS message

    public NewsletterDispatchProvider(
        ISubscriptionRepository subscriptionRepository,
        ISnsPublisher snsPublisher,
        IOptions<SnsOptions> snsOptions,
        ILogger<NewsletterDispatchProvider> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _snsPublisher = snsPublisher;
        _snsOptions = snsOptions.Value;
        _logger = logger;
    }

    public async Task<Result<int>> DispatchNewsletterAsync(
        NewsletterType newsletterType, 
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting newsletter dispatch for {NewsletterType}. CorrelationId: {CorrelationId}",
            newsletterType,
            correlationId);

        var totalCount = await _subscriptionRepository.GetActiveSubscriberCountAsync(
            newsletterType, 
            cancellationToken);

        if (totalCount == 0)
        {
            _logger.LogInformation(
                "No active subscribers for {NewsletterType}",
                newsletterType);
            return Result<int>.Success(0);
        }

        var dispatchTime = DateTime.UtcNow;
        var totalBatches = (int)Math.Ceiling((double)totalCount / BatchSize);
        var processedCount = 0;
        var batchNumber = 0;

        // Process in batches
        for (var skip = 0; skip < totalCount; skip += BatchSize)
        {
            batchNumber++;
            var subscribers = await _subscriptionRepository.GetActiveSubscribersAsync(
                newsletterType,
                skip,
                BatchSize,
                cancellationToken);

            if (subscribers.Count == 0) break;

            var message = new NewsletterDispatchMessage
            {
                DispatchId = $"{newsletterType}_{dispatchTime:yyyyMMddHHmmss}_{batchNumber}",
                NewsletterType = newsletterType,
                Timestamp = dispatchTime,
                CorrelationId = correlationId,
                BatchNumber = batchNumber,
                TotalBatches = totalBatches,
                Subscribers = subscribers.Select(s => new SubscriberInfo
                {
                    UserId = s.UserId,
                    Email = s.User?.Email ?? string.Empty,
                    SubscriptionId = s.Id
                }).ToList()
            };

            // Publish to SNS (fans out to email-send and audit-log queues)
            var attributes = new Dictionary<string, string>
            {
                { "NewsletterType", newsletterType.ToString() },
                { "CorrelationId", correlationId }
            };

            await _snsPublisher.PublishAsync(
                _snsOptions.NewsletterDispatchTopicArn,
                message,
                attributes,
                cancellationToken);

            processedCount += subscribers.Count;
            
            _logger.LogDebug(
                "Published batch {BatchNumber}/{TotalBatches} with {Count} subscribers",
                batchNumber,
                totalBatches,
                subscribers.Count);
        }

        _logger.LogInformation(
            "Newsletter dispatch complete for {NewsletterType}. Total subscribers: {Count}, Batches: {Batches}",
            newsletterType,
            processedCount,
            batchNumber);

        return Result<int>.Success(processedCount);
    }
}

