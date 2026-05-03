namespace NewsletterX.Tests.Unit;

using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsletterX.ContractModels.Requests;
using NewsletterX.EntityModels.PostgreSQL;
using NewsletterX.Providers.Implementations;
using NewsletterX.Providers.Options;
using NewsletterX.Repositories.Interfaces;

/// <summary>
/// Unit tests for AuthProvider.
/// </summary>
/// <remarks>
/// UNIT TEST STRATEGY:
/// ──────────────────
/// - Mock all dependencies (IUserRepository)
/// - Test business logic in isolation
/// - No database, no external services
/// - Fast execution (milliseconds)
/// 
/// WHAT TO TEST:
/// - Happy path (success scenarios)
/// - Edge cases (empty input, null values)
/// - Error cases (user not found, wrong password)
/// 
/// WHAT NOT TO TEST HERE:
/// - Database queries (integration tests)
/// - HTTP layer (integration tests with WebApplicationFactory)
/// </remarks>
public class AuthProviderTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<AuthProvider>> _loggerMock;
    private readonly JwtOptions _jwtOptions;
    private readonly AuthProvider _authProvider;

    public AuthProviderTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<AuthProvider>>();
        _jwtOptions = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SecretKey = "ThisIsAVeryLongSecretKeyForTestingPurposes123!",
            AccessTokenExpirationMinutes = 60
        };

        _authProvider = new AuthProvider(
            _userRepositoryMock.Object,
            Options.Create(_jwtOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new RegisterUserRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            DisplayName = "Test User"
        };

        _userRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity user, CancellationToken _) =>
            {
                user.Id = Guid.NewGuid();
                user.CreatedAt = DateTime.UtcNow;
                return user;
            });

        // Act
        var result = await _authProvider.RegisterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.User.Email.Should().Be(request.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterUserRequest
        {
            Email = "existing@example.com",
            Password = "Password123!"
        };

        _userRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _authProvider.RegisterAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AUTH_1005"); // EmailAlreadyExists
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123!"
        };

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow
        };

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity u, CancellationToken _) => u);

        // Act
        var result = await _authProvider.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsFailure()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword!"
        };

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword!")
        };

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _authProvider.LoginAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AUTH_1001"); // InvalidCredentials
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "Password123!"
        };

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        // Act
        var result = await _authProvider.LoginAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AUTH_1001"); // InvalidCredentials
    }
}

