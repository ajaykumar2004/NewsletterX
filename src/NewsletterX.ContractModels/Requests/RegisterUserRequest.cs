namespace NewsletterX.ContractModels.Requests;

/// <summary>
/// Request model for user registration.
/// </summary>
public class RegisterUserRequest
{
    /// <summary>
    /// Email address for the new account.
    /// </summary>
    /// <example>user@example.com</example>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Password for the new account.
    /// Must be at least 8 characters.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name for the user.
    /// </summary>
    /// <example>John Doe</example>
    public string? DisplayName { get; set; }
}

