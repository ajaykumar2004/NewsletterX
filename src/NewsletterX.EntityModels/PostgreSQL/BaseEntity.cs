namespace NewsletterX.EntityModels.PostgreSQL;

/// <summary>
/// Base class for all PostgreSQL entities with common audit fields.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Base Entity Class
/// ─────────────────────────────────────────
/// Common fields across all entities:
/// - Id: Primary key (GUID for distributed systems)
/// - CreatedAt: When the record was created
/// - UpdatedAt: When the record was last modified
/// - DeletedAt: Soft delete timestamp (null = not deleted)
/// 
/// WHY GUID FOR PRIMARY KEY?
/// - Globally unique: Safe for distributed systems, data merging
/// - No sequential exposure: Can't guess IDs (security)
/// - Client-side generation: Can create ID before DB insert
/// - Works well with DynamoDB if you ever need it
/// 
/// TRADE-OFF: GUIDs are 16 bytes vs 4-8 for int/bigint.
/// For most applications, this overhead is negligible.
/// 
/// TRANSFERABLE PATTERN: This base entity works for any EF Core project.
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>
    /// Primary key. Generated client-side as GUID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// UTC timestamp when the record was created.
    /// Set automatically in DbContext.SaveChanges().
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the record was last updated.
    /// Updated automatically in DbContext.SaveChanges().
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the record was soft-deleted.
    /// Null means the record is active.
    /// </summary>
    /// <remarks>
    /// SOFT DELETE PATTERN:
    /// - Records are never physically deleted (for audit/compliance)
    /// - Global query filter excludes deleted records by default
    /// - Use IgnoreQueryFilters() to see deleted records when needed
    /// 
    /// ALTERNATIVE: Hard delete with archive table
    /// - Move deleted records to an archive table
    /// - Keeps main table clean and fast
    /// - More complex but better for high-volume tables
    /// </remarks>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Returns true if the entity has been soft-deleted.
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;
}

