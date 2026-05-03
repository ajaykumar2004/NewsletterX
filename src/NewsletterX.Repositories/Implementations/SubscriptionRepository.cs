namespace NewsletterX.Repositories.Implementations;

using Microsoft.EntityFrameworkCore;
using NewsletterX.EntityModels.PostgreSQL;
using NewsletterX.Repositories.Interfaces;
using NewsletterX.Types.Enums;

/// <summary>
/// EF Core implementation of ISubscriptionRepository.
/// </summary>
public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly NewsletterXDbContext _dbContext;

    public SubscriptionRepository(NewsletterXDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SubscriptionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<SubscriptionEntity?> GetByUserAndTypeAsync(
        Guid userId, 
        NewsletterType newsletterType, 
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.NewsletterType == newsletterType, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionEntity>> GetByUserIdAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.NewsletterType)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionEntity>> GetActiveSubscribersAsync(
        NewsletterType newsletterType,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Include User to get email addresses for dispatch
        return await _dbContext.Subscriptions
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.NewsletterType == newsletterType && s.Status == SubscriptionStatus.Active)
            .OrderBy(s => s.Id) // Consistent ordering for pagination
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetActiveSubscriberCountAsync(
        NewsletterType newsletterType,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subscriptions
            .CountAsync(s => s.NewsletterType == newsletterType && s.Status == SubscriptionStatus.Active, cancellationToken);
    }

    public async Task<SubscriptionEntity> CreateAsync(
        SubscriptionEntity subscription, 
        CancellationToken cancellationToken = default)
    {
        subscription.SubscribedAt = DateTime.UtcNow;
        
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return subscription;
    }

    public async Task<SubscriptionEntity> UpdateAsync(
        SubscriptionEntity subscription, 
        CancellationToken cancellationToken = default)
    {
        _dbContext.Subscriptions.Update(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return subscription;
    }

    public async Task<bool> UpdateStatusAsync(
        Guid id, 
        SubscriptionStatus status, 
        CancellationToken cancellationToken = default)
    {
        var subscription = await _dbContext.Subscriptions.FindAsync(new object[] { id }, cancellationToken);
        
        if (subscription == null)
            return false;

        subscription.Status = status;
        
        if (status == SubscriptionStatus.Unsubscribed)
        {
            subscription.UnsubscribedAt = DateTime.UtcNow;
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subscription = await _dbContext.Subscriptions.FindAsync(new object[] { id }, cancellationToken);
        
        if (subscription == null)
            return false;

        subscription.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    public async Task<bool> ExistsAsync(
        Guid userId, 
        NewsletterType newsletterType, 
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subscriptions
            .AnyAsync(s => s.UserId == userId && s.NewsletterType == newsletterType, cancellationToken);
    }
}

