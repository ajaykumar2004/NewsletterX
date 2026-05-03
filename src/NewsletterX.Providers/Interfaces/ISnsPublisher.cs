namespace NewsletterX.Providers.Interfaces;

/// <summary>
/// Provider interface for SNS publishing.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Generic Publisher Interface
/// ──────────────────────────────────────────────────
/// - Abstracts AWS SNS from business logic
/// - Enables testing with mock publisher
/// - Single responsibility: just publishes messages
/// 
/// TRANSFERABLE PATTERN: This interface works for any message broker.
/// Implement for SNS, RabbitMQ, Kafka, etc.
/// </remarks>
public interface ISnsPublisher
{
    /// <summary>
    /// Publishes a message to an SNS topic.
    /// </summary>
    /// <typeparam name="T">Message type (will be JSON serialized).</typeparam>
    /// <param name="topicArn">ARN of the SNS topic.</param>
    /// <param name="message">Message to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Message ID from SNS.</returns>
    Task<string> PublishAsync<T>(string topicArn, T message, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes a message with message attributes.
    /// </summary>
    Task<string> PublishAsync<T>(
        string topicArn, 
        T message, 
        Dictionary<string, string> attributes,
        CancellationToken cancellationToken = default) where T : class;
}

