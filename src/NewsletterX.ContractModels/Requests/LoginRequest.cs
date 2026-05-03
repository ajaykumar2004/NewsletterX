namespace NewsletterX.ContractModels.Requests;

/// <summary>
/// Request model for user login.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Email address of the account.
    /// </summary>
    /// <example>user@example.com</example>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Password for the account.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

