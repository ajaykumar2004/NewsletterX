namespace NewsletterX.Types.Constants;

/// <summary>
/// Application-wide constants.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Constants vs Configuration
/// ──────────────────────────────────────────────────
/// Use constants for values that:
/// - Never change between environments
/// - Are used at compile time (attributes, etc.)
/// - Are truly constant (math, physics, standards)
/// 
/// Use configuration (appsettings.json) for values that:
/// - Differ between dev/staging/prod
/// - May need tuning without redeployment
/// - Are secrets or environment-specific
/// </remarks>
public static class AppConstants
{
    /// <summary>
    /// Default page size for paginated queries.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Maximum page size to prevent abuse.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Minimum password length for security.
    /// </summary>
    public const int MinPasswordLength = 8;

    /// <summary>
    /// BCrypt work factor. Higher = more secure but slower.
    /// 12 is a good balance for 2024+ hardware.
    /// </summary>
    public const int BcryptWorkFactor = 12;

    /// <summary>
    /// Correlation ID header name for distributed tracing.
    /// </summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// Claim type for user ID in JWT tokens.
    /// </summary>
    public const string UserIdClaimType = "sub";

    /// <summary>
    /// Claim type for email in JWT tokens.
    /// </summary>
    public const string EmailClaimType = "email";
}

/// <summary>
/// Error codes for consistent error responses.
/// </summary>
/// <remarks>
/// TRANSFERABLE PATTERN: Structured error codes enable:
/// - Client-side error handling by code (not message parsing)
/// - Localization (map code to translated message)
/// - Metrics/alerting by error type
/// </remarks>
public static class ErrorCodes
{
    // Authentication errors (1xxx)
    public const string InvalidCredentials = "AUTH_1001";
    public const string TokenExpired = "AUTH_1002";
    public const string TokenInvalid = "AUTH_1003";
    public const string UserNotFound = "AUTH_1004";
    public const string EmailAlreadyExists = "AUTH_1005";

    // Validation errors (2xxx)
    public const string ValidationFailed = "VAL_2001";
    public const string InvalidEmail = "VAL_2002";
    public const string WeakPassword = "VAL_2003";
    public const string InvalidNewsletterType = "VAL_2004";

    // Subscription errors (3xxx)
    public const string AlreadySubscribed = "SUB_3001";
    public const string NotSubscribed = "SUB_3002";
    public const string SubscriptionNotFound = "SUB_3003";

    // System errors (5xxx)
    public const string InternalError = "SYS_5001";
    public const string DatabaseError = "SYS_5002";
    public const string ExternalServiceError = "SYS_5003";
    public const string RateLimitExceeded = "SYS_5004";
}

/// <summary>
/// Cache key prefixes for consistent cache naming.
/// </summary>
public static class CacheKeys
{
    public const string UserById = "user:id:";
    public const string UserByEmail = "user:email:";
    public const string SubscriptionsByUser = "subscriptions:user:";
}

