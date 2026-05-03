namespace NewsletterX.Providers.Options;

/// <summary>
/// DynamoDB configuration options.
/// </summary>
public class DynamoDbOptions
{
    public const string SectionName = "DynamoDB";

    /// <summary>
    /// Name of the email dispatch log table.
    /// </summary>
    public string EmailDispatchLogTableName { get; set; } = "EmailDispatchLog";

    /// <summary>
    /// Number of days before records expire (TTL).
    /// </summary>
    public int TtlDays { get; set; } = 90;
}

