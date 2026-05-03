namespace NewsletterX.ContractModels.Responses;

using NewsletterX.Types.Enums;

/// <summary>
/// Response model for subscription information.
/// </summary>
public class SubscriptionResponse
{
    /// <summary>
    /// Unique subscription identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of newsletter.
    /// </summary>
    public NewsletterType NewsletterType { get; set; }

    /// <summary>
    /// Current subscription status.
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    /// <summary>
    /// When the user subscribed.
    /// </summary>
    public DateTime SubscribedAt { get; set; }

    /// <summary>
    /// When the user unsubscribed (if applicable).
    /// </summary>
    public DateTime? UnsubscribedAt { get; set; }
}

