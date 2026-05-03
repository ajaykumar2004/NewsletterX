namespace NewsletterX.Repositories.Interfaces;

using NewsletterX.EntityModels.DynamoDB;
using NewsletterX.Types.Enums;

/// <summary>
/// Repository interface for email dispatch audit log (DynamoDB).
/// </summary>
/// <remarks>
/// DYNAMODB-SPECIFIC CONSIDERATIONS:
/// ─────────────────────────────────
/// 1. No transactions by default (unlike SQL)
/// 2. Conditional writes for idempotency
/// 3. Batch operations for efficiency (max 25 items)
/// 4. Eventually consistent reads by default (use consistent reads when needed)
/// </remarks>
public interface IEmailDispatchLogRepository
{
    /// <summary>
    /// Logs a single email dispatch attempt.
    /// </summary>
    /// <remarks>
    /// Uses conditional put to prevent duplicate writes (idempotency).
    /// </remarks>
    Task LogDispatchAsync(
        EmailDispatchLogEntity entity, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs multiple email dispatch attempts in a batch.
    /// </summary>
    /// <remarks>
    /// DynamoDB BatchWriteItem supports max 25 items per batch.
    /// This method handles chunking automatically.
    /// 
    /// GOTCHA: BatchWriteItem does NOT support conditional writes.
    /// For idempotency with batch writes, check existence first or use
    /// TransactWriteItems (more expensive).
    /// </remarks>
    Task LogDispatchBatchAsync(
        IEnumerable<EmailDispatchLogEntity> entities, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of an existing dispatch log.
    /// </summary>
    /// <remarks>
    /// Uses conditional update to ensure the record exists.
    /// </remarks>
    Task UpdateStatusAsync(
        string newsletterType,
        string dispatchTimestamp,
        DispatchStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets dispatch history for a newsletter type within a time range.
    /// </summary>
    /// <remarks>
    /// Uses Query operation on the partition key with sort key range condition.
    /// Results are ordered by DispatchTimestamp (newest first if descending).
    /// </remarks>
    Task<IReadOnlyList<EmailDispatchLogEntity>> GetDispatchHistoryAsync(
        NewsletterType newsletterType,
        DateTime startTime,
        DateTime endTime,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific dispatch log entry.
    /// </summary>
    Task<EmailDispatchLogEntity?> GetDispatchAsync(
        string newsletterType,
        string dispatchTimestamp,
        CancellationToken cancellationToken = default);
}

