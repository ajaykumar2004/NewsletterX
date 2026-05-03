namespace NewsletterX.Types.Enums;

/// <summary>
/// Types of newsletters available in the system.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Enum vs Database Table
/// ───────────────────────────────────────────────
/// For V1, using an enum is simpler:
/// - No admin UI needed to manage newsletter types
/// - Compile-time safety
/// - Easy to reference in code
/// 
/// When to migrate to a database table:
/// - Need to add newsletter types without code deployment
/// - Need per-newsletter configuration (send time, template, etc.)
/// - Need user-created newsletters
/// 
/// TRANSFERABLE PATTERN: Start with enum, migrate to DB when complexity demands.
/// </remarks>
public enum NewsletterType
{
    /// <summary>
    /// Weekly digest newsletter - sent every Monday.
    /// </summary>
    Weekly = 0,

    /// <summary>
    /// Monthly summary newsletter.
    /// </summary>
    Monthly = 1,

    /// <summary>
    /// Breaking news - sent immediately when published.
    /// </summary>
    BreakingNews = 2,

    /// <summary>
    /// Product updates and feature announcements.
    /// </summary>
    ProductUpdates = 3
}

