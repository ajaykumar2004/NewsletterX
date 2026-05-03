namespace NewsletterX.EntityModels.PostgreSQL;

/// <summary>
/// Represents a user in the system.
/// </summary>
/// <remarks>
/// DATABASE DESIGN DECISIONS:
/// ─────────────────────────
/// 1. Email is unique and indexed (login identifier)
/// 2. Password is stored as BCrypt hash (never plain text!)
/// 3. Soft delete via inherited DeletedAt field
/// 4. No navigation properties to Subscriptions (loaded separately for performance)
/// 
/// SCHEMA:
/// CREATE TABLE users (
///     id UUID PRIMARY KEY,
///     email VARCHAR(320) NOT NULL UNIQUE,  -- RFC 5321 max length
///     password_hash VARCHAR(60) NOT NULL,   -- BCrypt hash length
///     created_at TIMESTAMP NOT NULL,
///     updated_at TIMESTAMP NOT NULL,
///     deleted_at TIMESTAMP NULL
/// );
/// CREATE UNIQUE INDEX ix_users_email ON users(email) WHERE deleted_at IS NULL;
/// 
/// INDEX DECISION: Partial unique index on email WHERE deleted_at IS NULL
/// - Allows same email to be re-registered after soft delete
/// - Enforces uniqueness only for active users
/// </remarks>
public class UserEntity : BaseEntity
{
    /// <summary>
    /// User's email address. Must be unique among active users.
    /// Max length 320 per RFC 5321 (64 local + @ + 255 domain).
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt hash of the user's password.
    /// NEVER store plain text passwords!
    /// </summary>
    /// <remarks>
    /// BCrypt format: $2a$12$...
    /// - $2a$: Algorithm version
    /// - 12$: Work factor (2^12 iterations)
    /// - ...: 22 chars salt + 31 chars hash
    /// 
    /// Total length: 60 characters (fixed)
    /// </remarks>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name for the user.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether the user's email has been verified.
    /// </summary>
    /// <remarks>
    /// For V1, we skip email verification.
    /// In production, you'd send a verification email on registration.
    /// </remarks>
    public bool EmailVerified { get; set; } = false;

    /// <summary>
    /// Last login timestamp for security auditing.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}

