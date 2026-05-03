namespace NewsletterX.Host.BackgroundServices;

using NewsletterX.Providers.Interfaces;
using NewsletterX.Types.Enums;

/// <summary>
/// Background service that dispatches newsletters on a schedule.
/// </summary>
/// <remarks>
/// SCHEDULING PATTERNS:
/// ───────────────────
/// 1. PeriodicTimer (used here): Simple, built-in, single-instance
/// 2. Hangfire: Distributed, persistent, dashboard, more features
/// 3. Quartz.NET: Enterprise-grade, complex scheduling
/// 
/// WHY PeriodicTimer?
/// - No external dependencies
/// - Good enough for single-instance deployments
/// - Simple to understand and maintain
/// 
/// FOR PRODUCTION (multi-instance):
/// - Use Hangfire with SQL/Redis storage
/// - Only one instance will run the job (distributed lock)
/// - Dashboard for monitoring and manual triggers
/// 
/// SCHEDULE: Every Monday at 9:00 AM (configurable)
/// </remarks>
public class WeeklyNewsletterJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeeklyNewsletterJob> _logger;

    public WeeklyNewsletterJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<WeeklyNewsletterJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("NewsletterDispatch:Enabled", false);
        
        if (!enabled)
        {
            _logger.LogInformation("WeeklyNewsletterJob is disabled.");
            return;
        }

        _logger.LogInformation("WeeklyNewsletterJob starting...");

        // For demo purposes, run every minute instead of weekly
        // In production, use a more sophisticated scheduler or cron expression
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for next tick
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;

                // Check if it's the right time to dispatch
                if (ShouldDispatch())
                {
                    await DispatchWeeklyNewsletterAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WeeklyNewsletterJob");
            }
        }

        _logger.LogInformation("WeeklyNewsletterJob stopped.");
    }

    private bool ShouldDispatch()
    {
        // For demo: always dispatch on each tick
        // For production: check day of week and time
        // Example: Monday at 9:00 AM UTC
        
        var now = DateTime.UtcNow;
        
        // Uncomment for real weekly schedule:
        // return now.DayOfWeek == DayOfWeek.Monday && now.Hour == 9 && now.Minute == 0;
        
        // For demo: log but don't actually dispatch every minute
        _logger.LogDebug("WeeklyNewsletterJob tick at {Time}", now);
        return false; // Set to true to enable
    }

    private async Task DispatchWeeklyNewsletterAsync(CancellationToken stoppingToken)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..12];
        
        _logger.LogInformation(
            "Starting weekly newsletter dispatch. CorrelationId: {CorrelationId}",
            correlationId);

        using var scope = _scopeFactory.CreateScope();
        var dispatchProvider = scope.ServiceProvider.GetRequiredService<INewsletterDispatchProvider>();

        var result = await dispatchProvider.DispatchNewsletterAsync(
            NewsletterType.Weekly,
            correlationId,
            stoppingToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Weekly newsletter dispatched to {Count} subscribers. CorrelationId: {CorrelationId}",
                result.Value,
                correlationId);
        }
        else
        {
            _logger.LogError(
                "Weekly newsletter dispatch failed: {ErrorCode} - {ErrorMessage}. CorrelationId: {CorrelationId}",
                result.ErrorCode,
                result.ErrorMessage,
                correlationId);
        }
    }
}

