namespace NewsletterX.Providers.Interfaces;

using NewsletterX.ContractModels.Requests;
using NewsletterX.ContractModels.Responses;
using NewsletterX.Types;

/// <summary>
/// Provider interface for authentication operations.
/// </summary>
public interface IAuthProvider
{
    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">Registration details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with auth response on success, or error on failure.</returns>
    Task<Result<AuthResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with auth response on success, or error on failure.</returns>
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current user's information.
    /// </summary>
    /// <param name="userId">User ID from JWT token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with user response on success, or error on failure.</returns>
    Task<Result<UserResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

