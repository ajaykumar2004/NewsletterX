namespace NewsletterX.Providers.Options;

/// <summary>
/// JWT configuration options.
/// </summary>
/// <remarks>
/// Bound to "JwtSettings" section in appsettings.json.
/// 
/// SECURITY CONSIDERATIONS:
/// - SecretKey should be loaded from secrets manager in production
/// - Use asymmetric keys (RS256) for multi-service scenarios
/// - Keep AccessTokenExpirationMinutes short (15-60 min)
/// </remarks>
public class JwtOptions
{
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Token issuer (who created the token).
    /// </summary>
    public string Issuer { get; set; } = "NewsletterX";

    /// <summary>
    /// Token audience (who can use the token).
    /// </summary>
    public string Audience { get; set; } = "NewsletterX";

    /// <summary>
    /// Secret key for signing tokens (HS256).
    /// Must be at least 32 characters for security.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Access token expiration in minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;
}

