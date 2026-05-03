namespace NewsletterX.Api.Controllers;

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewsletterX.ContractModels.Requests;
using NewsletterX.ContractModels.Responses;
using NewsletterX.Providers.Interfaces;
using NewsletterX.Types.Constants;

/// <summary>
/// Controller for subscription operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionProvider _subscriptionProvider;
    private readonly IValidator<SubscribeRequest> _subscribeValidator;

    public SubscriptionsController(
        ISubscriptionProvider subscriptionProvider,
        IValidator<SubscribeRequest> subscribeValidator)
    {
        _subscriptionProvider = subscriptionProvider;
        _subscribeValidator = subscribeValidator;
    }

    /// <summary>
    /// Subscribe to a newsletter.
    /// </summary>
    /// <param name="request">Subscription details.</param>
    /// <returns>The created subscription.</returns>
    /// <response code="201">Subscription created.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="409">Already subscribed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiErrorResponse.Create(ErrorCodes.TokenInvalid, "Invalid token."));
        }

        // Validate request
        var validation = await _subscribeValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            
            return BadRequest(ApiErrorResponse.Validation(errors));
        }

        var result = await _subscriptionProvider.SubscribeAsync(userId.Value, request);

        if (result.IsFailure)
        {
            if (result.ErrorCode == ErrorCodes.AlreadySubscribed)
            {
                return Conflict(ApiErrorResponse.Create(result.ErrorCode, result.ErrorMessage!));
            }
            return BadRequest(ApiErrorResponse.Create(result.ErrorCode!, result.ErrorMessage!));
        }

        return CreatedAtAction(
            nameof(GetSubscription), 
            new { id = result.Value!.Id }, 
            result.Value);
    }

    /// <summary>
    /// Get all subscriptions for the current user.
    /// </summary>
    /// <returns>List of subscriptions.</returns>
    /// <response code="200">Subscriptions retrieved.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSubscriptions()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiErrorResponse.Create(ErrorCodes.TokenInvalid, "Invalid token."));
        }

        var result = await _subscriptionProvider.GetUserSubscriptionsAsync(userId.Value);
        
        return Ok(result.Value);
    }

    /// <summary>
    /// Get a specific subscription.
    /// </summary>
    /// <param name="id">Subscription ID.</param>
    /// <returns>The subscription.</returns>
    /// <response code="200">Subscription retrieved.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Subscription not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscription(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiErrorResponse.Create(ErrorCodes.TokenInvalid, "Invalid token."));
        }

        var result = await _subscriptionProvider.GetSubscriptionAsync(userId.Value, id);

        if (result.IsFailure)
        {
            return NotFound(ApiErrorResponse.Create(result.ErrorCode!, result.ErrorMessage!));
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Unsubscribe from a newsletter.
    /// </summary>
    /// <param name="id">Subscription ID.</param>
    /// <param name="request">Optional unsubscribe reason.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Unsubscribed successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Subscription not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unsubscribe(Guid id, [FromBody] UnsubscribeRequest? request = null)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiErrorResponse.Create(ErrorCodes.TokenInvalid, "Invalid token."));
        }

        var result = await _subscriptionProvider.UnsubscribeAsync(userId.Value, id, request);

        if (result.IsFailure)
        {
            return NotFound(ApiErrorResponse.Create(result.ErrorCode!, result.ErrorMessage!));
        }

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

