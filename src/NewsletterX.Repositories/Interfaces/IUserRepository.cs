namespace NewsletterX.Repositories.Interfaces;

using NewsletterX.EntityModels.PostgreSQL;

/// <summary>
/// Repository interface for user data access.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Repository Interface
/// ───────────────────────────────────────────
/// - Abstracts EF Core from business logic (Providers)
/// - Enables unit testing with mocks (no database needed)
/// - Single Responsibility: Only data access, no business rules
/// 
/// METHOD NAMING CONVENTION:
/// - GetBy*Async: Returns single entity or null
/// - Find*Async: Returns collection (possibly empty)
/// - Create/Update/Delete*Async: Mutation operations
/// - Exists*Async: Returns bool for existence checks
/// 
/// TRANSFERABLE PATTERN: This interface structure works for any repository.
/// </remarks>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by their unique ID.
    /// </summary>
    /// <param name="id">User ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user or null if not found.</returns>
    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email address.
    /// </summary>
    /// <param name="email">Email address (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user or null if not found.</returns>
    Task<UserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="user">User entity to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created user with generated ID.</returns>
    Task<UserEntity> CreateAsync(UserEntity user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="user">User entity with updated values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user.</returns>
    Task<UserEntity> UpdateAsync(UserEntity user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a user by setting DeletedAt.
    /// </summary>
    /// <param name="id">User ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user with the given email exists.
    /// </summary>
    /// <param name="email">Email to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if exists, false otherwise.</returns>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
}

