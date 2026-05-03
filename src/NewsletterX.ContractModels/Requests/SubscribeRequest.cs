namespace NewsletterX.ContractModels.Requests;

using NewsletterX.Types.Enums;

/// <summary>
/// Request model for subscribing to a newsletter.
/// </summary>
public class SubscribeRequest
{
    /// <summary>
    /// Type of newsletter to subscribe to.
    /// </summary>
    /// <example>Weekly</example>
    public NewsletterType NewsletterType { get; set; }
}

