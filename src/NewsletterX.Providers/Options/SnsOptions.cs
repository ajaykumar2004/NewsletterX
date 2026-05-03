namespace NewsletterX.Providers.Options;

/// <summary>
/// SNS configuration options.
/// </summary>
public class SnsOptions
{
    public const string SectionName = "Sns";

    /// <summary>
    /// ARN of the SNS topic for newsletter dispatch.
    /// </summary>
    public string NewsletterDispatchTopicArn { get; set; } = string.Empty;
}

