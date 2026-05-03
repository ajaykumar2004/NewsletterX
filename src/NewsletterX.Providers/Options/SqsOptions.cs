namespace NewsletterX.Providers.Options;

/// <summary>
/// SQS configuration options.
/// </summary>
public class SqsOptions
{
    public const string SectionName = "Sqs";

    /// <summary>
    /// URL of the email send queue.
    /// </summary>
    public string EmailSendQueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL of the audit log queue.
    /// </summary>
    public string AuditLogQueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Max messages to receive per poll (1-10).
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// Long polling wait time in seconds (0-20).
    /// </summary>
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    /// Visibility timeout in seconds.
    /// </summary>
    public int VisibilityTimeoutSeconds { get; set; } = 30;
}

