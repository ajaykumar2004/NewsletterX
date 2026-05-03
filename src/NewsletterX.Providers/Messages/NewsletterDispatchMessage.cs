namespace NewsletterX.Providers.Messages;

using NewsletterX.Types.Enums;

/// <summary>
/// Message published to SNS when a newsletter is dispatched.
/// </summary>
/// <remarks>
/// This message fans out to:
/// 1. email-send-queue → EmailSenderWorker → Sends emails
/// 2. audit-log-queue → AuditLogWorker → Logs to DynamoDB
/// 
/// MESSAGE DESIGN:
/// - DispatchId: Unique ID for this batch (idempotency key)
/// - NewsletterType: Which newsletter
/// - Subscribers: List of recipients (email, userId)
/// - Timestamp: When dispatch was initiated
/// - CorrelationId: Links to original request/job
/// </remarks>
public class NewsletterDispatchMessage
{
    /// <summary>
    /// Unique identifier for this dispatch batch.
    /// Used for idempotency - if same message received twice, only process once.
    /// </summary>
    public string DispatchId { get; set; } = string.Empty;

    /// <summary>
    /// Type of newsletter being dispatched.
    /// </summary>
    public NewsletterType NewsletterType { get; set; }

    /// <summary>
    /// List of subscribers to send to.
    /// </summary>
    public List<SubscriberInfo> Subscribers { get; set; } = new();

    /// <summary>
    /// When the dispatch was initiated.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Batch number (for multi-batch dispatches).
    /// </summary>
    public int BatchNumber { get; set; } = 1;

    /// <summary>
    /// Total number of batches in this dispatch.
    /// </summary>
    public int TotalBatches { get; set; } = 1;
}

/// <summary>
/// Information about a subscriber for dispatch.
/// </summary>
public class SubscriberInfo
{
    /// <summary>
    /// User ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Email address to send to.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Subscription ID (for tracking).
    /// </summary>
    public Guid SubscriptionId { get; set; }
}

