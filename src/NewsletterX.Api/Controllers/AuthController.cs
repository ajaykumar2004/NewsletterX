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
/// Controller for authentication operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthProvider _authProvider;
    private readonly IValidator<RegisterUserRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        IAuthProvider authProvider,
        IValidator<RegisterUserRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _authProvider = authProvider;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <param name="request">Registration details.</param>
    /// <returns>JWT token and user information.</returns>
    /// <response code="201">User created successfully.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="409">Email already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        // Validate request
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            
            return BadRequest(ApiErrorResponse.Validation(errors));
        }

        var result = await _authProvider.RegisterAsync(request);

        if (result.IsFailure)
        {
            if (result.ErrorCode == ErrorCodes.EmailAlreadyExists)
            {
                return Conflict(ApiErrorResponse.Create(result.ErrorCode, result.ErrorMessage!));
            }
            return BadRequest(ApiErrorResponse.Create(result.ErrorCode!, result.ErrorMessage!));
        }

        return CreatedAtAction(nameof(GetCurrentUser), result.Value);
    }

    /// <summary>
    /// Authenticate and get a JWT token.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>JWT token and user information.</returns>
    /// <response code="200">Login successful.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Validate request
        var validation = await _loginValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            
            return BadRequest(ApiErrorResponse.Validation(errors));
        }

        var result = await _authProvider.LoginAsync(request);

        if (result.IsFailure)
        {
            return Unauthorized(ApiErrorResponse.Create(result.ErrorCode!, result.ErrorMessage!));
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get current authenticated user's information.
    /// </summary>
    /// <returns>User information.</returns>
    /// <response code="200">User information retrieved.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiErrorResponse.Create(ErrorCodes.TokenInvalid, "Invalid token."));
        }

        var result = await _authProvider.GetCurrentUserAsync(userId.Value);

        if (result.IsFailure)
        {
            return NotFound(ApiErrorResponse.Create(result.ErrorCode!, result.ErrorMessage!));
        }

        return Ok(result.Value);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

