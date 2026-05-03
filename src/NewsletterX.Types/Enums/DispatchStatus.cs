namespace NewsletterX.Types.Enums;

/// <summary>
/// Represents the status of an email dispatch attempt.
/// </summary>
/// <remarks>
/// Used in the DynamoDB audit log to track the outcome of each email send attempt.
/// 
/// ARCHITECTURAL DECISION: Tracking dispatch status
/// ────────────────────────────────────────────────
/// - Enables retry logic: Only retry Pending or Failed
/// - Audit trail: Know exactly what happened to each email
/// - Metrics: Calculate success rate, identify problematic emails
/// </remarks>
public enum DispatchStatus
{
    /// <summary>
    /// Email is queued and waiting to be sent.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Email was successfully sent to the email provider (SES, SendGrid, etc.).
    /// Note: This doesn't guarantee delivery - just handoff to the provider.
    /// </summary>
    Sent = 1,

    /// <summary>
    /// Email send failed. Check ErrorMessage for details.
    /// May be retried depending on error type.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Email was skipped (e.g., user unsubscribed between queue and send).
    /// </summary>
    Skipped = 3,

    /// <summary>
    /// Email bounced after being sent.
    /// Updated asynchronously via webhook from email provider.
    /// </summary>
    Bounced = 4
}

