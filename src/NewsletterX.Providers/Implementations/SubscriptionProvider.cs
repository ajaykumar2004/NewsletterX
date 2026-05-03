namespace NewsletterX.Providers.Implementations;

using Microsoft.Extensions.Logging;
using NewsletterX.ContractModels.Requests;
using NewsletterX.ContractModels.Responses;
using NewsletterX.EntityModels.PostgreSQL;
using NewsletterX.Providers.Interfaces;
using NewsletterX.Repositories.Interfaces;
using NewsletterX.Types;
using NewsletterX.Types.Constants;
using NewsletterX.Types.Enums;

/// <summary>
/// Implementation of ISubscriptionProvider.
/// </summary>
public class SubscriptionProvider : ISubscriptionProvider
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SubscriptionProvider> _logger;

    public SubscriptionProvider(
        ISubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        ILogger<SubscriptionProvider> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<SubscriptionResponse>> SubscribeAsync(
        Guid userId, 
        SubscribeRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Result<SubscriptionResponse>.Failure(
                ErrorCodes.UserNotFound, 
                "User not found.");
        }

        // Check for existing subscription
        var existing = await _subscriptionRepository.GetByUserAndTypeAsync(
            userId, 
            request.NewsletterType, 
            cancellationToken);

        if (existing != null)
        {
            // If previously unsubscribed, reactivate
            if (existing.Status == SubscriptionStatus.Unsubscribed)
            {
                existing.Status = SubscriptionStatus.Active;
                existing.UnsubscribedAt = null;
                existing.UnsubscribeReason = null;
                existing.SubscribedAt = DateTime.UtcNow;
                
                await _subscriptionRepository.UpdateAsync(existing, cancellationToken);
                
                _logger.LogInformation(
                    "User {UserId} resubscribed to {NewsletterType}", 
                    userId, 
                    request.NewsletterType);
                
                return Result<SubscriptionResponse>.Success(MapToResponse(existing));
            }

            // Already subscribed
            return Result<SubscriptionResponse>.Failure(
                ErrorCodes.AlreadySubscribed, 
                $"Already subscribed to {request.NewsletterType} newsletter.");
        }

        // Create new subscription
        var subscription = new SubscriptionEntity
        {
            UserId = userId,
            NewsletterType = request.NewsletterType,
            Status = SubscriptionStatus.Active,
            SubscribedAt = DateTime.UtcNow
        };

        subscription = await _subscriptionRepository.CreateAsync(subscription, cancellationToken);
        
        _logger.LogInformation(
            "User {UserId} subscribed to {NewsletterType}", 
            userId, 
            request.NewsletterType);

        return Result<SubscriptionResponse>.Success(MapToResponse(subscription));
    }

    public async Task<Result> UnsubscribeAsync(
        Guid userId, 
        Guid subscriptionId, 
        UnsubscribeRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);

        if (subscription == null)
        {
            return Result.Failure(
                ErrorCodes.SubscriptionNotFound, 
                "Subscription not found.");
        }

        // Verify ownership
        if (subscription.UserId != userId)
        {
            return Result.Failure(
                ErrorCodes.SubscriptionNotFound, 
                "Subscription not found.");
        }

        if (subscription.Status == SubscriptionStatus.Unsubscribed)
        {
            return Result.Failure(
                ErrorCodes.NotSubscribed, 
                "Already unsubscribed.");
        }

        // Update status
        subscription.Status = SubscriptionStatus.Unsubscribed;
        subscription.UnsubscribedAt = DateTime.UtcNow;
        subscription.UnsubscribeReason = request?.Reason;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        
        _logger.LogInformation(
            "User {UserId} unsubscribed from {NewsletterType}. Reason: {Reason}", 
            userId, 
            subscription.NewsletterType,
            request?.Reason ?? "Not provided");

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<SubscriptionResponse>>> GetUserSubscriptionsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        var responses = subscriptions.Select(MapToResponse).ToList();
        
        return Result<IReadOnlyList<SubscriptionResponse>>.Success(responses);
    }

    public async Task<Result<SubscriptionResponse>> GetSubscriptionAsync(
        Guid userId,
        Guid subscriptionId, 
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);

        if (subscription == null || subscription.UserId != userId)
        {
            return Result<SubscriptionResponse>.Failure(
                ErrorCodes.SubscriptionNotFound, 
                "Subscription not found.");
        }

        return Result<SubscriptionResponse>.Success(MapToResponse(subscription));
    }

    private static SubscriptionResponse MapToResponse(SubscriptionEntity entity)
    {
        return new SubscriptionResponse
        {
            Id = entity.Id,
            NewsletterType = entity.NewsletterType,
            Status = entity.Status,
            SubscribedAt = entity.SubscribedAt,
            UnsubscribedAt = entity.UnsubscribedAt
        };
    }
}

