// =============================================================================
// NewsletterX - Program.cs (Composition Root)
// =============================================================================
// This is the application entry point and "Composition Root" where all 
// dependencies are wired together.
// =============================================================================

using NewsletterX.Api.Middleware;
using NewsletterX.Host.BackgroundServices;
using NewsletterX.Host.Configuration;
using NewsletterX.Providers.Extensions;
using NewsletterX.Repositories.Extensions;
using Serilog;

// Bootstrap Serilog early to capture startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting NewsletterX API...");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // =========================================================================
    // Service Registration
    // =========================================================================

    // Database (PostgreSQL via EF Core)
    builder.Services.AddDatabase(builder.Configuration);

    // AWS Services (DynamoDB, SNS, SQS)
    builder.Services.AddAwsServices(builder.Configuration);

    // Repositories (data access layer)
    builder.Services.AddRepositories();

    // Providers (business logic layer) + Options
    builder.Services.AddProviders();
    builder.Services.AddProviderOptions(builder.Configuration);

    // JWT Authentication
    builder.Services.AddJwtAuthentication(builder.Configuration);

    // Rate Limiting
    builder.Services.AddRateLimitingPolicies(builder.Configuration);

    // Health Checks
    builder.Services.AddHealthChecksConfiguration(builder.Configuration);

    // Controllers
    builder.Services.AddControllers()
        .AddApplicationPart(typeof(NewsletterX.Api.Controllers.HealthController).Assembly);

    // Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "NewsletterX API",
            Version = "v1",
            Description = "A production-grade newsletter subscription service."
        });

        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Enter 'Bearer' [space] and then your token.",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Background Services
    builder.Services.AddHostedService<EmailSenderWorker>();
    builder.Services.AddHostedService<AuditLogWorker>();
    builder.Services.AddHostedService<WeeklyNewsletterJob>();

    // =========================================================================
    // Build Application
    // =========================================================================
    var app = builder.Build();

    // Apply migrations in development
    await app.ApplyMigrationsAsync();

    // =========================================================================
    // Middleware Pipeline (ORDER MATTERS!)
    // =========================================================================

    // 1. Correlation ID (first - sets logging context)
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 2. Exception Handling (catches all unhandled exceptions)
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 3. Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    // 4. Swagger (all environments for this demo)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NewsletterX API v1");
        options.RoutePrefix = string.Empty;
    });

    // 5. Routing
    app.UseRouting();

    // 6. Rate Limiting
    app.UseRateLimiter();

    // 7. Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // 8. Endpoints
    app.MapControllers();
    app.MapHealthCheckEndpoints();

    Log.Information("NewsletterX API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

