namespace NewsletterX.Providers.Implementations;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NewsletterX.ContractModels.Requests;
using NewsletterX.ContractModels.Responses;
using NewsletterX.EntityModels.PostgreSQL;
using NewsletterX.Providers.Interfaces;
using NewsletterX.Providers.Options;
using NewsletterX.Repositories.Interfaces;
using NewsletterX.Types;
using NewsletterX.Types.Constants;

/// <summary>
/// Implementation of IAuthProvider.
/// </summary>
/// <remarks>
/// SECURITY IMPLEMENTATION:
/// ───────────────────────
/// 1. Passwords hashed with BCrypt (adaptive, salted)
/// 2. JWT tokens for stateless authentication
/// 3. Constant-time password comparison (BCrypt handles this)
/// 4. No password returned in any response
/// 
/// JWT CLAIMS:
/// - sub: User ID (standard claim)
/// - email: User email
/// - iat: Issued at timestamp
/// - exp: Expiration timestamp
/// </remarks>
public class AuthProvider : IAuthProvider
{
    private readonly IUserRepository _userRepository;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthProvider> _logger;

    public AuthProvider(
        IUserRepository userRepository,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthProvider> logger)
    {
        _userRepository = userRepository;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterUserRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
            return Result<AuthResponse>.Failure(
                ErrorCodes.EmailAlreadyExists, 
                "An account with this email already exists.");
        }

        // Hash password with BCrypt
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(
            request.Password, 
            BCrypt.Net.BCrypt.GenerateSalt(AppConstants.BcryptWorkFactor));

        // Create user entity
        var user = new UserEntity
        {
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            DisplayName = request.DisplayName
        };

        user = await _userRepository.CreateAsync(user, cancellationToken);
        
        _logger.LogInformation("User registered: {UserId} ({Email})", user.Id, user.Email);

        // Generate token and return response
        return Result<AuthResponse>.Success(CreateAuthResponse(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request, 
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent email: {Email}", request.Email);
            return Result<AuthResponse>.Failure(
                ErrorCodes.InvalidCredentials, 
                "Invalid email or password.");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login attempt with invalid password for: {Email}", request.Email);
            return Result<AuthResponse>.Failure(
                ErrorCodes.InvalidCredentials, 
                "Invalid email or password.");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
        
        _logger.LogInformation("User logged in: {UserId}", user.Id);

        return Result<AuthResponse>.Success(CreateAuthResponse(user));
    }

    public async Task<Result<UserResponse>> GetCurrentUserAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            return Result<UserResponse>.Failure(
                ErrorCodes.UserNotFound, 
                "User not found.");
        }

        return Result<UserResponse>.Success(MapToUserResponse(user));
    }

    private AuthResponse CreateAuthResponse(UserEntity user)
    {
        var token = GenerateJwtToken(user);
        var expiresIn = _jwtOptions.AccessTokenExpirationMinutes * 60;

        return new AuthResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            User = MapToUserResponse(user)
        };
    }

    private string GenerateJwtToken(UserEntity user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserResponse MapToUserResponse(UserEntity user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            EmailVerified = user.EmailVerified,
            CreatedAt = user.CreatedAt
        };
    }
}

