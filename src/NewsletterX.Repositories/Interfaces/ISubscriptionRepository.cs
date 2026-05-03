namespace NewsletterX.Repositories.Interfaces;

using NewsletterX.EntityModels.PostgreSQL;
using NewsletterX.Types.Enums;

/// <summary>
/// Repository interface for subscription data access.
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    /// Gets a subscription by its unique ID.
    /// </summary>
    Task<SubscriptionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's subscription to a specific newsletter type.
    /// </summary>
    Task<SubscriptionEntity?> GetByUserAndTypeAsync(
        Guid userId, 
        NewsletterType newsletterType, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions for a user.
    /// </summary>
    Task<IReadOnlyList<SubscriptionEntity>> GetByUserIdAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active subscribers for a newsletter type (paginated).
    /// </summary>
    /// <remarks>
    /// Used by the dispatch job to get subscribers in batches.
    /// 
    /// PAGINATION PATTERN:
    /// - Skip/Take is simple but inefficient for large offsets
    /// - For production at scale, use keyset pagination (WHERE id > lastId)
    /// - For this project, Skip/Take is acceptable
    /// </remarks>
    Task<IReadOnlyList<SubscriptionEntity>> GetActiveSubscribersAsync(
        NewsletterType newsletterType,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active subscribers for a newsletter type.
    /// </summary>
    Task<int> GetActiveSubscriberCountAsync(
        NewsletterType newsletterType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new subscription.
    /// </summary>
    Task<SubscriptionEntity> CreateAsync(
        SubscriptionEntity subscription, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing subscription.
    /// </summary>
    Task<SubscriptionEntity> UpdateAsync(
        SubscriptionEntity subscription, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a subscription.
    /// </summary>
    Task<bool> UpdateStatusAsync(
        Guid id, 
        SubscriptionStatus status, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a subscription.
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user is subscribed to a newsletter type.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid userId, 
        NewsletterType newsletterType, 
        CancellationToken cancellationToken = default);
}

