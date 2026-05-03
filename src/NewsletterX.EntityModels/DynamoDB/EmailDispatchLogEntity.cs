namespace NewsletterX.EntityModels.DynamoDB;

using Amazon.DynamoDBv2.DataModel;
using NewsletterX.Types.Enums;

/// <summary>
/// DynamoDB document for email dispatch audit logs.
/// </summary>
/// <remarks>
/// DYNAMODB KEY DESIGN:
/// ───────────────────
/// Table: EmailDispatchLog
/// 
/// PRIMARY KEY (Composite):
/// - Partition Key (PK): NewsletterType (string, e.g., "Weekly")
/// - Sort Key (SK): DispatchTimestamp (string, ISO 8601 format)
/// 
/// WHY THIS KEY DESIGN?
/// 
/// 1. Partition Key = NewsletterType
///    - Groups all dispatches for a newsletter together
///    - Enables query: "Get all dispatches for the weekly newsletter"
///    - Good distribution if you have multiple newsletter types
/// 
/// 2. Sort Key = DispatchTimestamp
///    - Sorted chronologically within the partition
///    - Enables range queries: "Get dispatches from last 7 days"
///    - Format: "2024-01-15T10:30:00Z#guid" (timestamp#id for uniqueness)
/// 
/// ACCESS PATTERNS SUPPORTED:
/// - Get all dispatches for a newsletter type (Query on PK)
/// - Get dispatches in a time range (Query on PK + SK begins_with/between)
/// - Get a specific dispatch (GetItem with PK + SK)
/// 
/// ACCESS PATTERNS NOT SUPPORTED (would need GSI):
/// - Get all dispatches for a specific user (would need GSI: PK=UserId)
/// - Get all failed dispatches across all newsletters (would need GSI: PK=Status)
/// 
/// TTL (Time To Live):
/// - ExpirationTime attribute set to DispatchTimestamp + 90 days
/// - DynamoDB automatically deletes expired items (eventually, within 48h)
/// - Keeps table size manageable, reduces costs
/// 
/// TRANSFERABLE PATTERN: This append-only audit log design works for any
/// event sourcing or audit trail use case.
/// </remarks>
[DynamoDBTable("EmailDispatchLog")]
public class EmailDispatchLogEntity
{
    /// <summary>
    /// Partition Key: Newsletter type as string.
    /// </summary>
    /// <remarks>
    /// Stored as string (not int) for readability in DynamoDB console
    /// and to avoid issues if enum values change.
    /// </remarks>
    [DynamoDBHashKey("NewsletterType")]
    public string NewsletterType { get; set; } = string.Empty;

    /// <summary>
    /// Sort Key: Timestamp with unique suffix for ordering and uniqueness.
    /// Format: "2024-01-15T10:30:00.000Z#dispatch-guid"
    /// </summary>
    [DynamoDBRangeKey("DispatchTimestamp")]
    public string DispatchTimestamp { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this dispatch batch.
    /// </summary>
    [DynamoDBProperty("DispatchId")]
    public string DispatchId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the user this email was sent to.
    /// </summary>
    [DynamoDBProperty("UserId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Email address the newsletter was sent to.
    /// </summary>
    [DynamoDBProperty("Email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Status of the dispatch attempt.
    /// </summary>
    [DynamoDBProperty("Status")]
    public string Status { get; set; } = DispatchStatus.Pending.ToString();

    /// <summary>
    /// Number of send attempts (for retry tracking).
    /// </summary>
    [DynamoDBProperty("AttemptCount")]
    public int AttemptCount { get; set; } = 1;

    /// <summary>
    /// Error message if the dispatch failed.
    /// </summary>
    [DynamoDBProperty("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// Links this log to the original HTTP request or job run.
    /// </summary>
    [DynamoDBProperty("CorrelationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// TTL attribute for automatic expiration.
    /// Unix timestamp (seconds since epoch) when this record should expire.
    /// </summary>
    /// <remarks>
    /// DynamoDB TTL:
    /// - Set to dispatch time + 90 days
    /// - DynamoDB automatically deletes expired items
    /// - Deletion is eventually consistent (can take up to 48 hours)
    /// - No cost for TTL deletions
    /// 
    /// IMPORTANT: TTL attribute MUST be a Number type containing Unix epoch seconds.
    /// </remarks>
    [DynamoDBProperty("ExpirationTime")]
    public long ExpirationTime { get; set; }

    /// <summary>
    /// When the record was created (for debugging/auditing).
    /// </summary>
    [DynamoDBProperty("CreatedAt")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>
    /// Helper to create a properly formatted sort key.
    /// </summary>
    public static string CreateSortKey(DateTime timestamp, string dispatchId)
    {
        // ISO 8601 format ensures lexicographic ordering matches chronological ordering
        return $"{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}#{dispatchId}";
    }

    /// <summary>
    /// Helper to calculate TTL expiration time.
    /// </summary>
    public static long CalculateExpirationTime(DateTime timestamp, int ttlDays)
    {
        var expirationDate = timestamp.AddDays(ttlDays);
        return new DateTimeOffset(expirationDate).ToUnixTimeSeconds();
    }
}

