namespace NewsletterX.Repositories.Implementations;

using Microsoft.EntityFrameworkCore;
using NewsletterX.EntityModels.PostgreSQL;
using NewsletterX.Repositories.Interfaces;

/// <summary>
/// EF Core implementation of IUserRepository.
/// </summary>
/// <remarks>
/// IMPLEMENTATION NOTES:
/// ────────────────────
/// 1. AsNoTracking() used for read-only queries (better performance)
/// 2. Email comparison is case-insensitive (ToLower())
/// 3. All operations respect the global soft-delete query filter
/// 
/// CONNECTION POOLING:
/// - DbContext is injected as SCOPED (one per request)
/// - Npgsql pools the underlying database connections
/// - No manual connection management needed
/// </remarks>
public class UserRepository : IUserRepository
{
    private readonly NewsletterXDbContext _dbContext;

    public UserRepository(NewsletterXDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<UserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public async Task<UserEntity> CreateAsync(UserEntity user, CancellationToken cancellationToken = default)
    {
        // Normalize email to lowercase for consistent storage
        user.Email = user.Email.ToLowerInvariant();
        
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return user;
    }

    public async Task<UserEntity> UpdateAsync(UserEntity user, CancellationToken cancellationToken = default)
    {
        // Ensure email is normalized
        user.Email = user.Email.ToLowerInvariant();
        
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return user;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        
        if (user == null)
            return false;

        user.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        return await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }
}

