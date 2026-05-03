# NewsletterX - Complete Technical Documentation

> A production-grade newsletter subscription service built with C# / ASP.NET Core 8.
> This document explains every architectural decision, code pattern, and infrastructure choice.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Solution Architecture](#2-solution-architecture)
3. [Project Structure Deep Dive](#3-project-structure-deep-dive)
4. [Database Design](#4-database-design)
5. [Authentication & Security](#5-authentication--security)
6. [Business Logic Layer](#6-business-logic-layer)
7. [Data Access Layer](#7-data-access-layer)
8. [API Layer](#8-api-layer)
9. [Messaging & Fan-Out Architecture](#9-messaging--fan-out-architecture)
10. [Background Services](#10-background-services)
11. [Observability & Health Checks](#11-observability--health-checks)
12. [Configuration Management](#12-configuration-management)
13. [Testing Strategy](#13-testing-strategy)
14. [Docker & Local Development](#14-docker--local-development)
15. [Production Considerations](#15-production-considerations)
16. [Transferable Patterns](#16-transferable-patterns)

---

## 1. Project Overview

### 1.1 Purpose

NewsletterX is a **portfolio project** designed to demonstrate production-grade infrastructure thinking. The business idea (newsletter subscriptions) is simple by design—the real goal is showing that **given any idea, the surrounding infrastructure is just a few tweaks**.

### 1.2 What the Application Does

```
┌─────────────────────────────────────────────────────────────┐
│                    USER FLOW                                 │
├─────────────────────────────────────────────────────────────┤
│  1. User registers with email/password                       │
│  2. User logs in, receives JWT token                         │
│  3. User subscribes to newsletters (Weekly, Monthly, etc.)   │
│  4. Weekly job dispatches newsletters to all subscribers     │
│  5. Each dispatch is logged to DynamoDB for auditing         │
└─────────────────────────────────────────────────────────────┘
```

### 1.3 Tech Stack

| Component | Technology | Why This Choice |
|-----------|------------|-----------------|
| **Runtime** | .NET 8 / ASP.NET Core | Latest LTS, excellent performance, familiar to enterprise |
| **Primary DB** | PostgreSQL | Open source, excellent JSON support, strong EF Core provider |
| **Audit Log** | DynamoDB | Append-only writes, automatic TTL, high throughput |
| **Messaging** | SNS + SQS | Decoupled fan-out, independent scaling, DLQ support |
| **Auth** | JWT | Stateless, scalable, no session storage needed |
| **Local AWS** | LocalStack | Same SDK code works locally and in production |
| **Logging** | Serilog | Structured logging, multiple sinks, rich ecosystem |

---

## 2. Solution Architecture

### 2.1 Layered Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      NewsletterX.Host                                │
│                    (Composition Root)                                │
│         Program.cs | DI Registration | Configuration                 │
│              Background Services | Health Checks                     │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       NewsletterX.Api                                │
│                      (HTTP Layer)                                    │
│              Controllers | Middleware | Filters                      │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    NewsletterX.Providers                             │
│                   (Business Logic Layer)                             │
│      IAuthProvider | ISubscriptionProvider | ISnsPublisher           │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   NewsletterX.Repositories                           │
│                     (Data Access Layer)                              │
│     IUserRepository | ISubscriptionRepository (PostgreSQL)           │
│              IEmailDispatchLogRepository (DynamoDB)                  │
└────────────────────────────┬────────────────────────────────────────┘
                             │
              ┌──────────────┴──────────────┐
              ▼                              ▼
┌───────────────────────────┐    ┌───────────────────────────┐
│  NewsletterX.EntityModels │    │   NewsletterX.Types       │
│   EF Core Entities        │    │  Enums | Constants        │
│   DynamoDB Models         │    │  Result<T> Pattern        │
└───────────────────────────┘    └───────────────────────────┘
```

### 2.2 Call Chain

```
HTTP Request 
    → Controller (HTTP concerns: routing, status codes)
        → Provider (Business logic: validation, orchestration)
            → Repository (Data access: queries, commands)
                → Database / AWS Service
```

**Why this separation?**

1. **Controllers** never touch the database directly—they don't know if data comes from PostgreSQL, Redis, or an API
2. **Providers** contain business rules but don't know HTTP status codes
3. **Repositories** execute queries but have no business logic
4. **Each layer is independently testable** with mocks

### 2.3 Dependency Direction

```
NewsletterX.Host ────────────────────────────────────────────┐
       │                                                      │
       ▼                                                      │
NewsletterX.Api ─────────────────────────────────────────┐   │
       │                                                  │   │
       ▼                                                  │   │
NewsletterX.Providers ──────────────────────────────┐    │   │
       │                                             │    │   │
       ├──────────────────────┐                      │    │   │
       ▼                      ▼                      │    │   │
NewsletterX.Repositories  NewsletterX.ContractModels │    │   │
       │                      │                      │    │   │
       ▼                      ▼                      │    │   │
NewsletterX.EntityModels  NewsletterX.Types ◄────────┴────┴───┘
       │                      ▲
       └──────────────────────┘

RULE: Dependencies flow DOWNWARD only. No circular references.
```

---

## 3. Project Structure Deep Dive

### 3.1 NewsletterX.Types (Foundation Layer)

**Purpose:** Shared primitives that have NO dependencies on other projects.

```
NewsletterX.Types/
├── Enums/
│   ├── SubscriptionStatus.cs   # Active, Unsubscribed, Bounced, etc.
│   ├── DispatchStatus.cs       # Pending, Sent, Failed, etc.
│   └── NewsletterType.cs       # Weekly, Monthly, BreakingNews
├── Constants/
│   └── AppConstants.cs         # Error codes, claim types, limits
└── Result.cs                   # Result<T> pattern for explicit errors
```

#### Key File: `Result.cs`

```csharp
// WHY RESULT<T>?
// ──────────────
// Instead of throwing exceptions for expected failures (user not found,
// validation failed), we return Result<T> which forces callers to handle both cases.

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }           // Only valid if IsSuccess
    public string? ErrorCode { get; }  // Machine-readable error
    public string? ErrorMessage { get } // Human-readable error
}

// USAGE:
var result = await _authProvider.LoginAsync(request);
if (result.IsFailure)
{
    return Unauthorized(result.ErrorMessage);
}
return Ok(result.Value);
```

**When to use exceptions vs Result<T>:**
- **Exceptions:** Unexpected failures (database down, null reference)
- **Result<T>:** Expected failures (wrong password, user exists)

---

### 3.2 NewsletterX.EntityModels (Data Layer)

**Purpose:** Define how data is stored in PostgreSQL and DynamoDB.

```
NewsletterX.EntityModels/
├── PostgreSQL/
│   ├── BaseEntity.cs           # Id, CreatedAt, UpdatedAt, DeletedAt
│   ├── UserEntity.cs           # Email, PasswordHash, etc.
│   ├── SubscriptionEntity.cs   # UserId, NewsletterType, Status
│   └── NewsletterXDbContext.cs # EF Core DbContext
├── DynamoDB/
│   └── EmailDispatchLogEntity.cs # Audit log with TTL
└── Migrations/
    └── InitialCreate.cs        # EF Core migration
```

#### Key File: `BaseEntity.cs`

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }          // Primary key
    public DateTime CreatedAt { get; set; } // Set on create
    public DateTime UpdatedAt { get; set; } // Updated on modify
    public DateTime? DeletedAt { get; set; } // Soft delete timestamp
    
    public bool IsDeleted => DeletedAt.HasValue;
}
```

**Why GUID for Primary Key?**
- Globally unique (safe for distributed systems)
- No sequential exposure (security)
- Can be generated client-side (before DB insert)
- Works with DynamoDB if needed

**Why Soft Delete?**
- Audit compliance (keep history)
- Easy recovery (just clear DeletedAt)
- Referential integrity (no orphaned FKs)

#### Key File: `NewsletterXDbContext.cs`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // 1. Global query filter for soft delete
    entity.HasQueryFilter(e => e.DeletedAt == null);
    
    // 2. Unique index on email (only for non-deleted users)
    entity.HasIndex(e => e.Email)
        .IsUnique()
        .HasFilter("deleted_at IS NULL");  // Partial index!
    
    // 3. Snake_case naming for PostgreSQL
    entity.SetTableName("users");  // Not "Users"
}
```

**Why Partial Unique Index?**
- Allows same email to be re-registered after account deletion
- Uniqueness enforced only for active users

---

### 3.3 NewsletterX.ContractModels (API Layer)

**Purpose:** Define the shape of API requests and responses.

```
NewsletterX.ContractModels/
├── Requests/
│   ├── RegisterUserRequest.cs   # Email, Password, DisplayName
│   ├── LoginRequest.cs          # Email, Password
│   ├── SubscribeRequest.cs      # NewsletterType
│   └── UnsubscribeRequest.cs    # Reason (optional)
├── Responses/
│   ├── AuthResponse.cs          # AccessToken, ExpiresAt, User
│   ├── UserResponse.cs          # Id, Email, CreatedAt
│   ├── SubscriptionResponse.cs  # Id, NewsletterType, Status
│   └── ApiErrorResponse.cs      # ErrorCode, Message, Details
└── Validators/
    ├── RegisterUserRequestValidator.cs
    ├── LoginRequestValidator.cs
    └── SubscribeRequestValidator.cs
```

#### Why Separate from EntityModels?

```
ENTITY (how it's stored)          CONTRACT (how it's transferred)
─────────────────────────         ───────────────────────────────
UserEntity                        UserResponse
  - Id                              - Id
  - Email                           - Email
  - PasswordHash  ← NEVER EXPOSE    - DisplayName
  - CreatedAt                       - CreatedAt
  - UpdatedAt     ← Not needed
  - DeletedAt     ← Not needed
```

- Entities can have DB-specific attributes
- Contracts stay clean for API consumers
- Can version contracts independently (V1 vs V2)

#### Key File: `RegisterUserRequestValidator.cs`

```csharp
public class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(320); // RFC 5321 max

        RuleFor(x => x.Password)
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Need uppercase")
            .Matches(@"[a-z]").WithMessage("Need lowercase")
            .Matches(@"[0-9]").WithMessage("Need digit")
            .Matches(@"[\W_]").WithMessage("Need special char");
    }
}
```

**Why FluentValidation over DataAnnotations?**
- **Testable:** Validators are classes, can unit test
- **Composable:** Reuse rules across DTOs
- **Complex rules:** Async validation, cross-property checks
- **DI-friendly:** Auto-discovered and registered

---

### 3.4 NewsletterX.Repositories (Data Access Layer)

**Purpose:** Abstract database operations behind interfaces.

```
NewsletterX.Repositories/
├── Interfaces/
│   ├── IUserRepository.cs
│   ├── ISubscriptionRepository.cs
│   └── IEmailDispatchLogRepository.cs
├── Implementations/
│   ├── UserRepository.cs           # EF Core → PostgreSQL
│   ├── SubscriptionRepository.cs   # EF Core → PostgreSQL
│   └── EmailDispatchLogRepository.cs # AWS SDK → DynamoDB
└── Extensions/
    └── RepositoryServiceCollectionExtensions.cs
```

#### Key Pattern: Repository Interface

```csharp
public interface IUserRepository
{
    // Naming convention:
    // - GetBy*Async: Returns single entity or null
    // - Find*Async: Returns collection
    // - Create/Update/Delete*Async: Mutations
    // - Exists*Async: Boolean checks
    
    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<UserEntity?> GetByEmailAsync(string email, CancellationToken ct);
    Task<UserEntity> CreateAsync(UserEntity user, CancellationToken ct);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);
}
```

**Why Interfaces?**
1. **Testability:** Mock `IUserRepository` in unit tests
2. **Swappability:** Could replace EF Core with Dapper
3. **Single Responsibility:** Repositories only do data access

#### Key File: `EmailDispatchLogRepository.cs`

```csharp
public async Task LogDispatchAsync(EmailDispatchLogEntity entity, CancellationToken ct)
{
    var request = new PutItemRequest
    {
        TableName = "EmailDispatchLog",
        Item = EntityToAttributes(entity),
        // IDEMPOTENCY: Only write if doesn't exist
        ConditionExpression = "attribute_not_exists(NewsletterType)"
    };

    try
    {
        await _dynamoDb.PutItemAsync(request, ct);
    }
    catch (ConditionalCheckFailedException)
    {
        // Item already exists - expected for retries
        _logger.LogDebug("Dispatch log already exists (idempotent)");
    }
}
```

**DynamoDB Patterns Used:**
- **Conditional writes** for idempotency
- **Batch writes** with automatic chunking (max 25 items)
- **TTL** for automatic expiration
- **Low-level API** for fine-grained control

---

### 3.5 NewsletterX.Providers (Business Logic Layer)

**Purpose:** Implement business rules, orchestrate repository calls.

```
NewsletterX.Providers/
├── Interfaces/
│   ├── IAuthProvider.cs
│   ├── ISubscriptionProvider.cs
│   ├── INewsletterDispatchProvider.cs
│   └── ISnsPublisher.cs
├── Implementations/
│   ├── AuthProvider.cs              # Login, Register, JWT
│   ├── SubscriptionProvider.cs      # Subscribe, Unsubscribe
│   ├── NewsletterDispatchProvider.cs # Dispatch to SNS
│   └── SnsPublisher.cs              # Generic SNS wrapper
├── Options/
│   ├── JwtOptions.cs
│   ├── SnsOptions.cs
│   ├── SqsOptions.cs
│   └── DynamoDbOptions.cs
├── Messages/
│   └── NewsletterDispatchMessage.cs # SNS/SQS message schema
└── Extensions/
    └── ProviderServiceCollectionExtensions.cs
```

#### Key File: `AuthProvider.cs`

```csharp
public async Task<Result<AuthResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct)
{
    // BUSINESS RULE: Email must be unique
    if (await _userRepository.ExistsByEmailAsync(request.Email, ct))
    {
        return Result<AuthResponse>.Failure(
            ErrorCodes.EmailAlreadyExists, 
            "An account with this email already exists.");
    }

    // SECURITY: Hash password with BCrypt
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(
        request.Password, 
        BCrypt.Net.BCrypt.GenerateSalt(workFactor: 12));

    var user = new UserEntity
    {
        Email = request.Email.ToLowerInvariant(), // Normalize
        PasswordHash = passwordHash,
        DisplayName = request.DisplayName
    };

    user = await _userRepository.CreateAsync(user, ct);
    
    return Result<AuthResponse>.Success(CreateAuthResponse(user));
}
```

**Key Decisions:**
- **BCrypt work factor 12:** Balance between security and speed
- **Email normalization:** Lowercase for consistent lookups
- **Result pattern:** Explicit success/failure, no exceptions

#### Key File: `NewsletterDispatchProvider.cs`

```csharp
public async Task<Result<int>> DispatchNewsletterAsync(
    NewsletterType newsletterType, 
    string correlationId,
    CancellationToken ct)
{
    // Get total count for batch calculation
    var totalCount = await _subscriptionRepository.GetActiveSubscriberCountAsync(newsletterType, ct);
    var totalBatches = (int)Math.Ceiling((double)totalCount / BatchSize);
    
    // Process in batches of 100
    for (var skip = 0; skip < totalCount; skip += BatchSize)
    {
        var subscribers = await _subscriptionRepository.GetActiveSubscribersAsync(
            newsletterType, skip, BatchSize, ct);

        var message = new NewsletterDispatchMessage
        {
            DispatchId = $"{newsletterType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{batchNumber}",
            NewsletterType = newsletterType,
            Subscribers = subscribers.Select(s => new SubscriberInfo
            {
                UserId = s.UserId,
                Email = s.User?.Email ?? string.Empty
            }).ToList(),
            CorrelationId = correlationId
        };

        // Publish to SNS → fans out to email-send + audit-log queues
        await _snsPublisher.PublishAsync(_snsOptions.TopicArn, message, ct);
    }
    
    return Result<int>.Success(totalCount);
}
```

**Batching Pattern:**
- Process subscribers in chunks (100 per batch)
- Each batch = one SNS message
- Prevents huge messages (SNS limit: 256KB)
- Enables parallel processing by consumers

---

### 3.6 NewsletterX.Api (HTTP Layer)

**Purpose:** Handle HTTP concerns—routing, model binding, status codes.

```
NewsletterX.Api/
├── Controllers/
│   ├── HealthController.cs
│   ├── AuthController.cs
│   └── SubscriptionsController.cs
└── Middleware/
    ├── CorrelationIdMiddleware.cs
    └── ExceptionHandlingMiddleware.cs
```

#### Key File: `CorrelationIdMiddleware.cs`

```csharp
public async Task InvokeAsync(HttpContext context)
{
    // 1. Get or generate correlation ID
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N")[..12];

    // 2. Store for use in controllers/services
    context.Items["CorrelationId"] = correlationId;

    // 3. Add to response headers
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    // 4. Push to Serilog context (all logs include it)
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await _next(context);
    }
}
```

**Why Correlation IDs?**
```
HTTP Request (abc123)
    ├── Log: "User login attempt" [CorrelationId: abc123]
    ├── SNS Message published    [CorrelationId: abc123]
    │       ├── SQS Consumer 1   [CorrelationId: abc123]
    │       └── SQS Consumer 2   [CorrelationId: abc123]
    │               └── DynamoDB write [CorrelationId: abc123]
    └── Response sent

When debugging: grep logs for "abc123" → see entire request flow
```

#### Key File: `AuthController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]  // No auth required
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        // 1. Validate with FluentValidation
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            
            return BadRequest(ApiErrorResponse.Validation(errors));
        }

        // 2. Call provider (business logic)
        var result = await _authProvider.RegisterAsync(request);

        // 3. Map Result to HTTP response
        if (result.IsFailure)
        {
            if (result.ErrorCode == ErrorCodes.EmailAlreadyExists)
                return Conflict(...);  // 409
            return BadRequest(...);    // 400
        }

        return CreatedAtAction(...);   // 201
    }
}
```

**Controller Responsibilities:**
- ✅ HTTP status codes
- ✅ Request validation
- ✅ Model binding
- ✅ Route handling
- ❌ Business logic (that's Providers)
- ❌ Database queries (that's Repositories)

---

### 3.7 NewsletterX.Host (Composition Root)

**Purpose:** Wire everything together—DI, configuration, middleware pipeline.

```
NewsletterX.Host/
├── Program.cs                       # Entry point, DI, pipeline
├── appsettings.json                 # Configuration
├── appsettings.Development.json     # Dev overrides
├── Dockerfile                       # Container image
├── Configuration/
│   ├── JwtConfiguration.cs          # JWT bearer setup
│   ├── AwsConfiguration.cs          # AWS clients
│   ├── DatabaseConfiguration.cs     # EF Core setup
│   ├── RateLimitingConfiguration.cs # Rate limiter
│   └── HealthCheckConfiguration.cs  # Health endpoints
├── BackgroundServices/
│   ├── EmailSenderWorker.cs         # SQS consumer
│   ├── AuditLogWorker.cs            # SQS consumer
│   └── WeeklyNewsletterJob.cs       # Scheduled job
└── HealthChecks/
    ├── SqsHealthCheck.cs
    └── DynamoDbHealthCheck.cs
```

#### Key File: `Program.cs`

```csharp
// 1. DATABASE
builder.Services.AddDatabase(builder.Configuration);

// 2. AWS SERVICES (Singleton clients!)
builder.Services.AddAwsServices(builder.Configuration);

// 3. REPOSITORIES (Scoped - one per request)
builder.Services.AddRepositories();

// 4. PROVIDERS (Scoped - one per request)
builder.Services.AddProviders();
builder.Services.AddProviderOptions(builder.Configuration);

// 5. JWT AUTH
builder.Services.AddJwtAuthentication(builder.Configuration);

// 6. RATE LIMITING
builder.Services.AddRateLimitingPolicies(builder.Configuration);

// 7. BACKGROUND SERVICES
builder.Services.AddHostedService<EmailSenderWorker>();
builder.Services.AddHostedService<AuditLogWorker>();
builder.Services.AddHostedService<WeeklyNewsletterJob>();

// MIDDLEWARE PIPELINE (ORDER MATTERS!)
app.UseMiddleware<CorrelationIdMiddleware>(); // 1st: sets context
app.UseMiddleware<ExceptionHandlingMiddleware>(); // 2nd: catches all
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthCheckEndpoints();
```

**Middleware Order:**
```
Request ──► Correlation ID ──► Exception Handler ──► Serilog ──► Rate Limiter
                                                                      │
Response ◄── Controllers ◄── Authorization ◄── Authentication ◄───────┘
```

---

## 4. Database Design

### 4.1 PostgreSQL Schema

```sql
-- USERS TABLE
CREATE TABLE users (
    id UUID PRIMARY KEY,
    email VARCHAR(320) NOT NULL,  -- RFC 5321 max length
    password_hash VARCHAR(60) NOT NULL,  -- BCrypt hash
    display_name VARCHAR(100),
    email_verified BOOLEAN DEFAULT FALSE,
    last_login_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL,
    deleted_at TIMESTAMP  -- NULL = active, non-NULL = soft deleted
);

-- Partial unique index: email unique only for non-deleted users
CREATE UNIQUE INDEX ix_users_email_unique 
ON users(email) 
WHERE deleted_at IS NULL;

-- SUBSCRIPTIONS TABLE
CREATE TABLE subscriptions (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    newsletter_type INT NOT NULL,  -- Enum as int
    status INT NOT NULL DEFAULT 0,
    subscribed_at TIMESTAMP NOT NULL,
    unsubscribed_at TIMESTAMP,
    unsubscribe_reason VARCHAR(500),
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL,
    deleted_at TIMESTAMP
);

-- One active subscription per user per newsletter type
CREATE UNIQUE INDEX ix_subscriptions_user_newsletter_unique
ON subscriptions(user_id, newsletter_type)
WHERE deleted_at IS NULL;

-- For "get all active subscribers for newsletter X"
CREATE INDEX ix_subscriptions_newsletter_status
ON subscriptions(newsletter_type, status)
WHERE deleted_at IS NULL;
```

### 4.2 DynamoDB Schema

```
Table: EmailDispatchLog

Primary Key:
  - Partition Key (PK): NewsletterType (String)
  - Sort Key (SK): DispatchTimestamp (String, ISO 8601)

Attributes:
  - DispatchId: String (idempotency key)
  - UserId: String
  - Email: String
  - Status: String (Pending, Sent, Failed)
  - AttemptCount: Number
  - ErrorMessage: String (optional)
  - CorrelationId: String
  - ExpirationTime: Number (Unix timestamp for TTL)
  - CreatedAt: String

TTL Attribute: ExpirationTime (auto-delete after 90 days)
```

**Key Design Rationale:**

```
PK = NewsletterType    → Groups all dispatches for a newsletter
SK = Timestamp#ID      → Sorted by time, unique per dispatch

QUERIES SUPPORTED:
✅ Get all dispatches for "Weekly" newsletter (Query on PK)
✅ Get dispatches from last 7 days (Query on PK + SK range)
✅ Get specific dispatch (GetItem with PK + SK)

QUERIES NOT SUPPORTED (would need GSI):
❌ Get all dispatches for a specific user
❌ Get all failed dispatches across all newsletters
```

---

## 5. Authentication & Security

### 5.1 Password Hashing with BCrypt

```csharp
// REGISTRATION: Hash password
var hash = BCrypt.Net.BCrypt.HashPassword(
    password, 
    BCrypt.Net.BCrypt.GenerateSalt(workFactor: 12));

// Result: $2a$12$LQv3c1yqBw...
// Format: $<algo>$<work>$<22-char-salt><31-char-hash>

// LOGIN: Verify password
bool isValid = BCrypt.Net.BCrypt.Verify(inputPassword, storedHash);
```

**Why BCrypt?**
- **Adaptive:** Work factor can increase with hardware
- **Salted:** Each hash has unique salt (prevents rainbow tables)
- **Slow by design:** Makes brute-force expensive
- **Battle-tested:** Decades of security research

### 5.2 JWT Token Structure

```
Header.Payload.Signature
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.
eyJzdWIiOiIxMjM0NTY3ODkwIiwiZW1haWwiOiJ1c2VyQGV4YW1wbGUuY29tIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjE1MTYyNDI2MjJ9.
SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
```

**Claims we include:**
- `sub`: User ID (standard claim)
- `email`: User's email
- `iat`: Issued at timestamp
- `exp`: Expiration timestamp
- `jti`: Unique token ID

**Security Configuration:**

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,        // Check token issuer
    ValidateAudience = true,      // Check intended audience
    ValidateLifetime = true,      // Reject expired tokens
    ValidateIssuerSigningKey = true,  // Verify signature
    ClockSkew = TimeSpan.Zero     // No tolerance for expiry
};
```

### 5.3 Rate Limiting

```csharp
// Fixed Window Rate Limiter on auth endpoints
options.AddPolicy("auth", context =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString();
    
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: clientIp,
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,           // 10 requests
            Window = TimeSpan.FromSeconds(60),  // per minute
            QueueLimit = 0              // Reject immediately when full
        });
});
```

**Why rate limit auth endpoints?**
- Prevents brute-force password attacks
- Prevents credential stuffing
- Prevents registration spam

---

## 6. Business Logic Layer

### 6.1 Provider Pattern

```csharp
// INTERFACE: Defines capability
public interface IAuthProvider
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
}

// IMPLEMENTATION: Contains business logic
public class AuthProvider : IAuthProvider
{
    private readonly IUserRepository _userRepository;  // Data access
    private readonly JwtOptions _jwtOptions;           // Configuration
    private readonly ILogger<AuthProvider> _logger;    // Logging

    public async Task<Result<AuthResponse>> RegisterAsync(...)
    {
        // Business rule: Email must be unique
        // Business rule: Password must be hashed
        // Business rule: User must be created
        // Business rule: Token must be generated
    }
}
```

### 6.2 Validation vs Business Rules

```
VALIDATION (ContractModels/Validators)     BUSINESS RULES (Providers)
─────────────────────────────────────      ─────────────────────────
• Email format is valid                    • Email is not already taken
• Password has 8+ characters               • User has permission to subscribe
• NewsletterType is valid enum             • Can't subscribe twice to same newsletter
• Required fields are present              • User must exist to log in

WHERE: API layer (before Provider)         WHERE: Provider layer
WHEN: Every request, synchronously         WHEN: After validation passes
```

---

## 7. Data Access Layer

### 7.1 Connection Pooling

```csharp
// POSTGRESQL (Npgsql)
// Connection pooling is automatic via connection string
"Max Pool Size=50;Min Pool Size=5;Connection Idle Lifetime=300"

// EF Core DbContext is SCOPED (one per request)
// Underlying connections are POOLED by Npgsql
services.AddDbContext<NewsletterXDbContext>(options => 
    options.UseNpgsql(connectionString));

// DYNAMODB
// Client MUST be SINGLETON - it manages its own HTTP pool
services.AddSingleton<IAmazonDynamoDB>(sp => new AmazonDynamoDBClient(config));

// ❌ WRONG: Creating client per request = socket exhaustion
// ✅ RIGHT: Singleton client, reused across all requests
```

### 7.2 EF Core Patterns

```csharp
// READ-ONLY QUERIES: Use AsNoTracking()
return await _dbContext.Users
    .AsNoTracking()  // Don't track changes, better performance
    .FirstOrDefaultAsync(u => u.Id == id, ct);

// SOFT DELETE: Automatic via global query filter
// All queries exclude deleted records by default
entity.HasQueryFilter(e => e.DeletedAt == null);

// TO SEE DELETED RECORDS:
await _dbContext.Users
    .IgnoreQueryFilters()  // Bypass soft delete filter
    .Where(u => u.DeletedAt != null)
    .ToListAsync();

// AUDIT FIELDS: Auto-set in SaveChanges
public override Task<int> SaveChangesAsync(...)
{
    foreach (var entry in ChangeTracker.Entries<BaseEntity>())
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.CreatedAt = DateTime.UtcNow;
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
    return base.SaveChangesAsync(...);
}
```

---

## 8. API Layer

### 8.1 Controller Structure

```csharp
[ApiController]                    // Enables automatic model validation
[Route("api/[controller]")]        // Route: /api/subscriptions
[Authorize]                        // Requires authentication
[Produces("application/json")]     // Response content type
public class SubscriptionsController : ControllerBase
{
    [HttpPost]                     // POST /api/subscriptions
    [ProducesResponseType(typeof(SubscriptionResponse), 201)]
    [ProducesResponseType(typeof(ApiErrorResponse), 400)]
    [ProducesResponseType(typeof(ApiErrorResponse), 401)]
    [ProducesResponseType(typeof(ApiErrorResponse), 409)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var userId = GetUserId();  // From JWT claims
        var result = await _provider.SubscribeAsync(userId, request);
        
        if (result.IsFailure)
        {
            return result.ErrorCode switch
            {
                ErrorCodes.AlreadySubscribed => Conflict(...),
                _ => BadRequest(...)
            };
        }
        
        return CreatedAtAction(nameof(GetSubscription), new { id = result.Value.Id }, result.Value);
    }
}
```

### 8.2 Error Response Format

```json
{
    "errorCode": "AUTH_1005",
    "message": "An account with this email already exists.",
    "details": null,
    "traceId": "abc123def456"
}

// Validation errors include field-specific details:
{
    "errorCode": "VAL_2001",
    "message": "One or more validation errors occurred.",
    "details": {
        "email": ["Email is required.", "Invalid email format."],
        "password": ["Password must be at least 8 characters."]
    },
    "traceId": "xyz789"
}
```

---

## 9. Messaging & Fan-Out Architecture

### 9.1 SNS → SQS Fan-Out Pattern

```
NewsletterDispatchProvider.DispatchAsync()
            │
            ▼
    ┌───────────────────┐
    │   SNS Topic       │
    │ newsletter-dispatch│
    └───────────────────┘
            │
            │ (fan-out to all subscribers)
            │
    ┌───────┴───────┐
    ▼               ▼
┌─────────┐   ┌─────────┐
│ SQS     │   │ SQS     │
│ email-  │   │ audit-  │
│ send    │   │ log     │
└────┬────┘   └────┬────┘
     │              │
     ▼              ▼
┌─────────┐   ┌─────────┐
│EmailSend│   │AuditLog │
│Worker   │   │Worker   │
└────┬────┘   └────┬────┘
     │              │
     ▼              ▼
  Sends          Writes to
  emails         DynamoDB
```

**Why SNS + SQS?**
1. **Decoupling:** Publisher doesn't know about consumers
2. **Adding consumers:** Just add another SQS subscription
3. **Independent scaling:** Each queue processes at its own rate
4. **Independent failure handling:** One queue's DLQ doesn't affect others

### 9.2 Message Schema

```csharp
public class NewsletterDispatchMessage
{
    public string DispatchId { get; set; }      // Idempotency key
    public NewsletterType NewsletterType { get; set; }
    public List<SubscriberInfo> Subscribers { get; set; }
    public DateTime Timestamp { get; set; }
    public string CorrelationId { get; set; }   // For tracing
    public int BatchNumber { get; set; }        // 1 of 5, 2 of 5, etc.
    public int TotalBatches { get; set; }
}

public class SubscriberInfo
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public Guid SubscriptionId { get; set; }
}
```

### 9.3 Dead Letter Queue (DLQ)

```
┌─────────┐    (3 failures)    ┌─────────┐
│ Main    │ ─────────────────► │ DLQ     │
│ Queue   │                    │         │
└─────────┘                    └─────────┘

Configuration:
- maxReceiveCount: 3 (move to DLQ after 3 failures)
- DLQ retention: 14 days (time to investigate)
- Alert on DLQ message count > 0
```

---

## 10. Background Services

### 10.1 SQS Consumer Pattern

```csharp
public class AuditLogWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Long poll: wait up to 20s for messages
                var messages = await ReceiveMessagesAsync(stoppingToken);
                
                foreach (var message in messages)
                {
                    try
                    {
                        await ProcessMessageAsync(message, stoppingToken);
                        
                        // Delete ONLY after successful processing
                        await DeleteMessageAsync(message, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process message");
                        // Don't delete - message will reappear after visibility timeout
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // Graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in consumer loop");
                await Task.Delay(5000, stoppingToken); // Back off
            }
        }
    }
}
```

**Key Patterns:**
- **Long polling:** `WaitTimeSeconds=20` reduces empty responses
- **Batch receive:** Up to 10 messages per call
- **Visibility timeout:** Message hidden while processing
- **Delete after success:** Failed messages retry automatically
- **Graceful shutdown:** Finish in-flight work before stopping

### 10.2 Scheduled Job Pattern

```csharp
public class WeeklyNewsletterJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (ShouldDispatch()) // Check day/time
            {
                await DispatchWeeklyNewsletterAsync(stoppingToken);
            }
        }
    }
    
    private bool ShouldDispatch()
    {
        var now = DateTime.UtcNow;
        return now.DayOfWeek == DayOfWeek.Monday && now.Hour == 9;
    }
}
```

**For Production (multi-instance):**
Use Hangfire with distributed lock to ensure only one instance runs the job.

---

## 11. Observability & Health Checks

### 11.1 Structured Logging with Serilog

```csharp
// Configuration in appsettings.json
"Serilog": {
    "MinimumLevel": {
        "Default": "Information",
        "Override": {
            "Microsoft": "Warning",
            "Microsoft.EntityFrameworkCore": "Warning"
        }
    },
    "WriteTo": [
        {
            "Name": "Console",
            "Args": {
                "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} | {Message:lj}{NewLine}{Exception}"
            }
        }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
}

// Usage in code
_logger.LogInformation(
    "User {UserId} subscribed to {NewsletterType}",
    userId,          // Structured property
    newsletterType); // Structured property

// Output:
// [10:23:45 INF] abc123 | User 550e8400-e29b-41d4-a716-446655440000 subscribed to Weekly
```

### 11.2 Health Check Endpoints

```
GET /health          → Basic liveness (is app running?)
GET /health/ready    → Full readiness (all dependencies OK?)
GET /health/db       → Database only

Response:
{
    "status": "Healthy",
    "totalDuration": 45.23,
    "checks": [
        {
            "name": "postgresql",
            "status": "Healthy",
            "duration": 12.5,
            "description": null
        },
        {
            "name": "sqs",
            "status": "Healthy",
            "duration": 23.1,
            "description": "SQS reachable. Pending messages: 0"
        },
        {
            "name": "dynamodb",
            "status": "Healthy",
            "duration": 9.6,
            "description": "DynamoDB table 'EmailDispatchLog' is active."
        }
    ]
}
```

---

## 12. Configuration Management

### 12.1 appsettings.json Structure

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Host=localhost;Port=5432;Database=newsletterx;..."
    },
    
    "JwtSettings": {
        "Issuer": "NewsletterX",
        "Audience": "NewsletterX",
        "SecretKey": "CHANGE_THIS_IN_PRODUCTION",
        "AccessTokenExpirationMinutes": 60
    },
    
    "AWS": {
        "Region": "us-east-1",
        "ServiceURL": "http://localhost:4566"  // LocalStack
    },
    
    "Sns": {
        "NewsletterDispatchTopicArn": "arn:aws:sns:us-east-1:000000000000:newsletter-dispatch"
    },
    
    "Sqs": {
        "EmailSendQueueUrl": "http://localhost:4566/000000000000/email-send-queue",
        "WaitTimeSeconds": 20,
        "VisibilityTimeoutSeconds": 30
    },
    
    "RateLimiting": {
        "Auth": {
            "WindowSeconds": 60,
            "PermitLimit": 10
        }
    }
}
```

### 12.2 Options Pattern

```csharp
// Define options class
public class JwtOptions
{
    public const string SectionName = "JwtSettings";
    public string Issuer { get; set; }
    public string SecretKey { get; set; }
    public int AccessTokenExpirationMinutes { get; set; }
}

// Register in DI
services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

// Inject and use
public class AuthProvider
{
    private readonly JwtOptions _options;
    
    public AuthProvider(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }
}
```

---

## 13. Testing Strategy

### 13.1 Unit Tests (Providers)

```csharp
public class AuthProviderTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly AuthProvider _authProvider;

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsFailure()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);  // Email exists

        // Act
        var result = await _authProvider.RegisterAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AUTH_1005");
    }
}
```

### 13.2 Test Pyramid

```
        ┌───────────────┐
        │   E2E Tests   │  Few, slow, high confidence
        │   (Manual)    │
        ├───────────────┤
        │  Integration  │  Some, medium speed
        │    Tests      │  (WebApplicationFactory + LocalStack)
        ├───────────────┤
        │  Unit Tests   │  Many, fast, isolated
        │  (Providers)  │  (Mock repositories)
        └───────────────┘
```

---

## 14. Docker & Local Development

### 14.1 docker-compose.yml

```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: newsletterx
      POSTGRES_PASSWORD: newsletterx_dev_password
      POSTGRES_DB: newsletterx
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD", "pg_isready", "-U", "newsletterx"]

  localstack:
    image: localstack/localstack:3.8
    environment:
      SERVICES: sns,sqs,dynamodb
      PERSISTENCE: 1
    ports:
      - "4566:4566"
    volumes:
      - ./localstack-init:/etc/localstack/init/ready.d
```

### 14.2 LocalStack Init Script

```bash
#!/bin/bash
# Creates AWS resources on LocalStack startup

# Create DLQs
aws --endpoint-url=http://localhost:4566 sqs create-queue --queue-name email-send-dlq
aws --endpoint-url=http://localhost:4566 sqs create-queue --queue-name audit-log-dlq

# Create main queues with redrive policy
aws --endpoint-url=http://localhost:4566 sqs create-queue \
    --queue-name email-send-queue \
    --attributes '{"RedrivePolicy": "{\"deadLetterTargetArn\":\"...\",\"maxReceiveCount\":\"3\"}"}'

# Create SNS topic
aws --endpoint-url=http://localhost:4566 sns create-topic --name newsletter-dispatch

# Subscribe SQS to SNS (fan-out)
aws --endpoint-url=http://localhost:4566 sns subscribe \
    --topic-arn arn:aws:sns:us-east-1:000000000000:newsletter-dispatch \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:us-east-1:000000000000:email-send-queue

# Create DynamoDB table
aws --endpoint-url=http://localhost:4566 dynamodb create-table \
    --table-name EmailDispatchLog \
    --attribute-definitions \
        AttributeName=NewsletterType,AttributeType=S \
        AttributeName=DispatchTimestamp,AttributeType=S \
    --key-schema \
        AttributeName=NewsletterType,KeyType=HASH \
        AttributeName=DispatchTimestamp,KeyType=RANGE \
    --billing-mode PAY_PER_REQUEST
```

---

## 15. Production Considerations

### 15.1 What Would Change for Production

| Area | Local | Production |
|------|-------|------------|
| **AWS** | LocalStack | Real AWS |
| **Secrets** | appsettings.json | AWS Secrets Manager |
| **Rate Limiting** | In-memory | Redis-backed |
| **Scheduled Jobs** | PeriodicTimer | Hangfire with Redis |
| **Database** | Local PostgreSQL | RDS with read replicas |
| **Logging** | Console | CloudWatch Logs |
| **Health Checks** | Basic | ALB health checks |

### 15.2 Security Checklist

```
[ ] JWT secret from Secrets Manager, not config
[ ] HTTPS everywhere (TLS 1.3)
[ ] Database connection with SSL
[ ] Rate limiting on all public endpoints
[ ] Input validation on all requests
[ ] Password hashing with BCrypt (work factor 12+)
[ ] Soft delete for audit compliance
[ ] CORS configured for specific origins
[ ] Security headers (CSP, X-Frame-Options, etc.)
```

---

## 16. Transferable Patterns

### These patterns work for ANY microservice:

1. **Layered Architecture**
   - Controller → Provider → Repository → Database
   - Each layer has clear responsibility

2. **Result Pattern**
   - `Result<T>` for expected failures
   - Exceptions for unexpected failures

3. **Repository Pattern**
   - Interface-based data access
   - Enables mocking and swapping implementations

4. **Options Pattern**
   - Strongly-typed configuration
   - `IOptions<T>` injection

5. **Soft Delete**
   - Global query filters
   - Partial unique indexes

6. **Connection Pool Awareness**
   - Npgsql pools automatically
   - AWS clients as singletons

7. **SNS → SQS Fan-Out**
   - One publish, multiple consumers
   - Independent scaling and failure handling

8. **DynamoDB Key Design**
   - PK/SK for efficient queries
   - TTL for automatic cleanup

9. **Correlation IDs**
   - Trace requests across services
   - Essential for debugging

10. **Health Checks**
    - Liveness vs Readiness
    - Check all dependencies

---

## Quick Reference

### Start Development
```bash
cd deployment && docker-compose up -d
cd ../src/NewsletterX.Host && dotnet run
# Open http://localhost:5000
```

### Run Tests
```bash
dotnet test
```

### Create Migration
```bash
dotnet ef migrations add MigrationName --project src/NewsletterX.EntityModels --startup-project src/NewsletterX.Host
```

### Apply Migration
```bash
dotnet ef database update --project src/NewsletterX.EntityModels --startup-project src/NewsletterX.Host
```

---

**This document covers every aspect of the NewsletterX implementation. The patterns and decisions here are transferable to any production microservice.**

