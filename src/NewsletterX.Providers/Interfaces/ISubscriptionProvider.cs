namespace NewsletterX.Providers.Interfaces;

using NewsletterX.ContractModels.Requests;
using NewsletterX.ContractModels.Responses;
using NewsletterX.Types;
using NewsletterX.Types.Enums;

/// <summary>
/// Provider interface for subscription operations.
/// </summary>
public interface ISubscriptionProvider
{
    /// <summary>
    /// Subscribes a user to a newsletter.
    /// </summary>
    Task<Result<SubscriptionResponse>> SubscribeAsync(
        Guid userId, 
        SubscribeRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes a user from a newsletter.
    /// </summary>
    Task<Result> UnsubscribeAsync(
        Guid userId, 
        Guid subscriptionId, 
        UnsubscribeRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions for a user.
    /// </summary>
    Task<Result<IReadOnlyList<SubscriptionResponse>>> GetUserSubscriptionsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific subscription.
    /// </summary>
    Task<Result<SubscriptionResponse>> GetSubscriptionAsync(
        Guid userId,
        Guid subscriptionId, 
        CancellationToken cancellationToken = default);
}

