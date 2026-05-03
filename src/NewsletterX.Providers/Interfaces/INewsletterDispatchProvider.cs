namespace NewsletterX.Providers.Interfaces;

using NewsletterX.Types;
using NewsletterX.Types.Enums;

/// <summary>
/// Provider interface for newsletter dispatch operations.
/// </summary>
public interface INewsletterDispatchProvider
{
    /// <summary>
    /// Dispatches a newsletter to all active subscribers.
    /// </summary>
    /// <param name="newsletterType">Type of newsletter to dispatch.</param>
    /// <param name="correlationId">Correlation ID for tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with number of subscribers notified.</returns>
    Task<Result<int>> DispatchNewsletterAsync(
        NewsletterType newsletterType, 
        string correlationId,
        CancellationToken cancellationToken = default);
}

