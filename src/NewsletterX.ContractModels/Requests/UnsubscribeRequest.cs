namespace NewsletterX.ContractModels.Requests;

/// <summary>
/// Request model for unsubscribing from a newsletter.
/// </summary>
public class UnsubscribeRequest
{
    /// <summary>
    /// Optional reason for unsubscribing (for feedback).
    /// </summary>
    /// <example>Too many emails</example>
    public string? Reason { get; set; }
}

