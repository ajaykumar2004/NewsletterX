namespace NewsletterX.Repositories.Extensions;

using Microsoft.Extensions.DependencyInjection;
using NewsletterX.Repositories.Implementations;
using NewsletterX.Repositories.Interfaces;

/// <summary>
/// Extension methods for registering repository services.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Extension Methods for DI Registration
/// ─────────────────────────────────────────────────────────────
/// Each layer provides its own AddXxx extension method:
/// - Encapsulates registration details
/// - Called from Program.cs in a specific order
/// - Makes Program.cs clean and readable
/// 
/// TRANSFERABLE PATTERN: This extension method pattern works for any layer.
/// </remarks>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers all repository implementations.
    /// </summary>
    /// <remarks>
    /// All repositories are registered as SCOPED:
    /// - One instance per HTTP request
    /// - Matches DbContext lifetime (also scoped)
    /// - Repositories can share the same DbContext instance
    /// 
    /// WHY NOT SINGLETON?
    /// - Repositories depend on DbContext which is scoped
    /// - Scoped services cannot depend on other scoped services if registered as singleton
    /// 
    /// WHY NOT TRANSIENT?
    /// - Would create new instances within same request
    /// - Could lead to inconsistent state if same entity modified twice
    /// </remarks>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // PostgreSQL repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

        // DynamoDB repositories
        services.AddScoped<IEmailDispatchLogRepository, EmailDispatchLogRepository>();

        return services;
    }
}

