namespace NewsletterX.ContractModels.Responses;

/// <summary>
/// Response model for authentication operations (login/register).
/// </summary>
public class AuthResponse
{
    /// <summary>
    /// JWT access token.
    /// </summary>
    /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</example>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token type (always "Bearer").
    /// </summary>
    /// <example>Bearer</example>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Token expiration time in seconds.
    /// </summary>
    /// <example>3600</example>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// When the token expires (UTC).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Basic user information.
    /// </summary>
    public UserResponse User { get; set; } = null!;
}

