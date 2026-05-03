namespace NewsletterX.Types.Enums;

/// <summary>
/// Represents the status of a user's subscription to a newsletter.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Why an Enum over a string?
/// ─────────────────────────────────────────────────
/// - Type safety: Compiler catches invalid values
/// - IntelliSense: Developers see valid options
/// - Database efficiency: Stored as int (4 bytes) vs varchar
/// - Refactoring: Renaming is safe across codebase
/// 
/// GOTCHA: Adding new values is safe; removing/renumbering is BREAKING.
/// Always add new values at the end to preserve database integrity.
/// 
/// TRANSFERABLE PATTERN: Use enums for fixed, well-known states.
/// Use strings for user-defined or frequently-changing values.
/// </remarks>
public enum SubscriptionStatus
{
    /// <summary>
    /// Subscription is active and will receive newsletters.
    /// </summary>
    Active = 0,

    /// <summary>
    /// User has unsubscribed. Soft state - can resubscribe.
    /// </summary>
    Unsubscribed = 1,

    /// <summary>
    /// Email bounced (invalid address). Requires manual intervention.
    /// </summary>
    Bounced = 2,

    /// <summary>
    /// User marked as spam. Do NOT send emails - legal implications.
    /// </summary>
    Complained = 3,

    /// <summary>
    /// Subscription is paused temporarily by user request.
    /// </summary>
    Paused = 4
}

