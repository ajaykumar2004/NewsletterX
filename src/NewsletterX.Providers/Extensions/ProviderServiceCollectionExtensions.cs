namespace NewsletterX.Providers.Extensions;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NewsletterX.ContractModels.Validators;
using NewsletterX.Providers.Implementations;
using NewsletterX.Providers.Interfaces;
using NewsletterX.Providers.Options;

/// <summary>
/// Extension methods for registering provider services.
/// </summary>
public static class ProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers all provider implementations and related services.
    /// </summary>
    public static IServiceCollection AddProviders(this IServiceCollection services)
    {
        // Register providers as scoped (one per request)
        services.AddScoped<IAuthProvider, AuthProvider>();
        services.AddScoped<ISubscriptionProvider, SubscriptionProvider>();
        services.AddScoped<INewsletterDispatchProvider, NewsletterDispatchProvider>();
        
        // SNS publisher can be singleton (stateless, thread-safe)
        services.AddSingleton<ISnsPublisher, SnsPublisher>();

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<RegisterUserRequestValidator>();

        return services;
    }

    /// <summary>
    /// Registers provider options from configuration.
    /// </summary>
    public static IServiceCollection AddProviderOptions(
        this IServiceCollection services, 
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SnsOptions>(configuration.GetSection(SnsOptions.SectionName));
        services.Configure<SqsOptions>(configuration.GetSection(SqsOptions.SectionName));
        services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.SectionName));

        return services;
    }
}

