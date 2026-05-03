namespace NewsletterX.EntityModels.PostgreSQL;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core DbContext for NewsletterX.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISIONS:
/// ───────────────────────
/// 1. SOFT DELETE via global query filters
///    - All queries automatically exclude deleted records
///    - Use IgnoreQueryFilters() to see deleted records
/// 
/// 2. AUDIT FIELDS auto-populated in SaveChanges
///    - CreatedAt set on add
///    - UpdatedAt set on add/modify
/// 
/// 3. SNAKE_CASE naming convention for PostgreSQL
///    - PostgreSQL convention is lowercase with underscores
///    - EF Core uses PascalCase by default
/// 
/// CONNECTION POOLING:
/// ──────────────────
/// - DbContext is SCOPED (one per HTTP request)
/// - Underlying connections are POOLED by Npgsql
/// - Don't create DbContext manually; use DI
/// 
/// TRANSFERABLE PATTERN: This DbContext structure works for any EF Core project.
/// </remarks>
public class NewsletterXDbContext : DbContext
{
    public NewsletterXDbContext(DbContextOptions<NewsletterXDbContext> options) 
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply snake_case naming convention for PostgreSQL
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Table names
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));

            // Column names
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            // Key names
            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()!));
            }

            // Foreign key names
            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));
            }

            // Index names
            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }
        }

        ConfigureUserEntity(modelBuilder);
        ConfigureSubscriptionEntity(modelBuilder);
    }

    private static void ConfigureUserEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(320); // RFC 5321 max email length

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(60); // BCrypt hash length

            entity.Property(e => e.DisplayName)
                .HasMaxLength(100);

            // Unique index on email for active (non-deleted) users only
            // This allows re-registration of soft-deleted emails
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasFilter("deleted_at IS NULL")
                .HasDatabaseName("ix_users_email_unique");

            // Global query filter for soft delete
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
    }

    private static void ConfigureSubscriptionEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubscriptionEntity>(entity =>
        {
            entity.ToTable("subscriptions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.UnsubscribeReason)
                .HasMaxLength(500);

            // Foreign key to User
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

            // Unique constraint: one active subscription per user per newsletter type
            entity.HasIndex(e => new { e.UserId, e.NewsletterType })
                .IsUnique()
                .HasFilter("deleted_at IS NULL")
                .HasDatabaseName("ix_subscriptions_user_newsletter_unique");

            // Index for "get all active subscribers for a newsletter"
            entity.HasIndex(e => new { e.NewsletterType, e.Status })
                .HasFilter("deleted_at IS NULL")
                .HasDatabaseName("ix_subscriptions_newsletter_status");

            // Global query filter for soft delete
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
    }

    /// <summary>
    /// Automatically sets audit fields on save.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SetAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Automatically sets audit fields on save (async).
    /// </summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void SetAuditFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Id = entry.Entity.Id == Guid.Empty ? Guid.NewGuid() : entry.Entity.Id;
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    // Prevent changing CreatedAt
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    break;
            }
        }
    }

    /// <summary>
    /// Converts PascalCase to snake_case.
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0) result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}

