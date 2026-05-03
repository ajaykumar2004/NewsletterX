namespace NewsletterX.ContractModels.Responses;

/// <summary>
/// Response model for user information.
/// </summary>
public class UserResponse
{
    /// <summary>
    /// Unique user identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User's email address.
    /// </summary>
    /// <example>user@example.com</example>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's display name.
    /// </summary>
    /// <example>John Doe</example>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether the user's email has been verified.
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// When the account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

