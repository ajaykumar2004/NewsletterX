namespace NewsletterX.EntityModels.PostgreSQL;

using NewsletterX.Types.Enums;

/// <summary>
/// Represents a user's subscription to a specific newsletter.
/// </summary>
/// <remarks>
/// DATABASE DESIGN DECISIONS:
/// ─────────────────────────
/// 1. Composite unique constraint on (UserId, NewsletterType) for active subscriptions
/// 2. Status enum stored as int (efficient, but requires migration for new values)
/// 3. Separate timestamps for subscribe/unsubscribe actions (audit trail)
/// 4. Foreign key to Users table with ON DELETE NO ACTION (prevent cascade)
/// 
/// SCHEMA:
/// CREATE TABLE subscriptions (
///     id UUID PRIMARY KEY,
///     user_id UUID NOT NULL REFERENCES users(id),
///     newsletter_type INT NOT NULL,
///     status INT NOT NULL DEFAULT 0,
///     subscribed_at TIMESTAMP NOT NULL,
///     unsubscribed_at TIMESTAMP NULL,
///     created_at TIMESTAMP NOT NULL,
///     updated_at TIMESTAMP NOT NULL,
///     deleted_at TIMESTAMP NULL
/// );
/// 
/// INDEXES:
/// - ix_subscriptions_user_newsletter: (user_id, newsletter_type) WHERE deleted_at IS NULL
///   → "Get all active subscriptions for a user"
/// - ix_subscriptions_newsletter_status: (newsletter_type, status) WHERE deleted_at IS NULL
///   → "Get all active subscribers for a newsletter type"
/// 
/// WHY SEPARATE FROM USERS TABLE?
/// - Users can have multiple subscriptions (different newsletter types)
/// - Subscription lifecycle is independent of user lifecycle
/// - Enables subscription-level analytics
/// </remarks>
public class SubscriptionEntity : BaseEntity
{
    /// <summary>
    /// Foreign key to the user who owns this subscription.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// Loaded explicitly when needed, not by default.
    /// </summary>
    public UserEntity? User { get; set; }

    /// <summary>
    /// Type of newsletter subscribed to.
    /// </summary>
    public NewsletterType NewsletterType { get; set; }

    /// <summary>
    /// Current status of the subscription.
    /// </summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    /// <summary>
    /// When the user originally subscribed.
    /// </summary>
    public DateTime SubscribedAt { get; set; }

    /// <summary>
    /// When the user unsubscribed (if applicable).
    /// Null if still subscribed.
    /// </summary>
    public DateTime? UnsubscribedAt { get; set; }

    /// <summary>
    /// Reason for unsubscribing (user-provided, optional).
    /// Useful for product feedback.
    /// </summary>
    public string? UnsubscribeReason { get; set; }
}

