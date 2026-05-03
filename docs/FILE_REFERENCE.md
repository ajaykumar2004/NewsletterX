# NewsletterX - File-by-File Reference Guide

> This document provides a quick explanation of every file in the project.

---

## Solution Structure Overview

```
NewsletterX/
├── NewsletterX.sln              # Solution file
├── NuGet.config                 # NuGet package sources
├── .gitignore                   # Git ignore rules
├── .dockerignore                # Docker build ignore rules
├── README.md                    # Project overview
├── docs/
│   ├── TECHNICAL_DOCUMENTATION.md  # Full technical guide
│   └── FILE_REFERENCE.md           # This file
├── deployment/
│   ├── docker-compose.yml
│   └── localstack-init/
│       └── init-aws.sh
├── src/
│   ├── NewsletterX.Types/
│   ├── NewsletterX.ContractModels/
│   ├── NewsletterX.EntityModels/
│   ├── NewsletterX.Repositories/
│   ├── NewsletterX.Providers/
│   ├── NewsletterX.Api/
│   └── NewsletterX.Host/
└── test/
    └── NewsletterX.Tests/
```

---

## Root Files

| File | Purpose |
|------|---------|
| `NewsletterX.sln` | Visual Studio solution file that groups all projects |
| `NuGet.config` | Configures NuGet to use nuget.org (bypasses corporate feeds) |
| `.gitignore` | Excludes bin/, obj/, secrets from Git |
| `.dockerignore` | Excludes unnecessary files from Docker builds |
| `README.md` | Project overview, quick start, architecture diagram |

---

## deployment/

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Defines PostgreSQL and LocalStack containers for local dev |
| `localstack-init/init-aws.sh` | Creates SNS topics, SQS queues, DynamoDB table on LocalStack startup |

**docker-compose.yml services:**
- `postgres`: PostgreSQL 16 database
- `localstack`: AWS emulator (SNS, SQS, DynamoDB)

---

## src/NewsletterX.Types/

**Purpose:** Foundation layer with zero dependencies. Contains enums, constants, and shared types.

| File | Purpose |
|------|---------|
| `NewsletterX.Types.csproj` | Project file, no external dependencies |
| `Enums/SubscriptionStatus.cs` | Active, Unsubscribed, Bounced, Complained, Paused |
| `Enums/DispatchStatus.cs` | Pending, Sent, Failed, Skipped, Bounced |
| `Enums/NewsletterType.cs` | Weekly, Monthly, BreakingNews, ProductUpdates |
| `Constants/AppConstants.cs` | Error codes, claim types, page sizes, BCrypt work factor |
| `Result.cs` | Result<T> pattern for explicit success/failure handling |

**Key Concepts:**
- Enums stored as integers in DB for efficiency
- Error codes are machine-readable (AUTH_1001, VAL_2001)
- Result<T> avoids exceptions for expected failures

---

## src/NewsletterX.ContractModels/

**Purpose:** API request/response DTOs and validation rules. Separate from EntityModels to decouple API from database.

| File | Purpose |
|------|---------|
| `NewsletterX.ContractModels.csproj` | References FluentValidation |
| `Requests/RegisterUserRequest.cs` | Email, Password, DisplayName |
| `Requests/LoginRequest.cs` | Email, Password |
| `Requests/SubscribeRequest.cs` | NewsletterType |
| `Requests/UnsubscribeRequest.cs` | Reason (optional feedback) |
| `Responses/AuthResponse.cs` | AccessToken, TokenType, ExpiresIn, User |
| `Responses/UserResponse.cs` | Id, Email, DisplayName, CreatedAt |
| `Responses/SubscriptionResponse.cs` | Id, NewsletterType, Status, SubscribedAt |
| `Responses/ApiErrorResponse.cs` | ErrorCode, Message, Details, TraceId |
| `Validators/RegisterUserRequestValidator.cs` | Email format, password strength rules |
| `Validators/LoginRequestValidator.cs` | Required fields only |
| `Validators/SubscribeRequestValidator.cs` | Valid enum value |

**Key Concepts:**
- Contracts don't expose internal fields (PasswordHash, DeletedAt)
- FluentValidation is testable and composable
- ApiErrorResponse provides consistent error format

---

## src/NewsletterX.EntityModels/

**Purpose:** Database entity classes for PostgreSQL (EF Core) and DynamoDB.

| File | Purpose |
|------|---------|
| `NewsletterX.EntityModels.csproj` | References EF Core, Npgsql, AWS SDK |
| `PostgreSQL/BaseEntity.cs` | Id, CreatedAt, UpdatedAt, DeletedAt (soft delete) |
| `PostgreSQL/UserEntity.cs` | Email, PasswordHash, DisplayName, LastLoginAt |
| `PostgreSQL/SubscriptionEntity.cs` | UserId, NewsletterType, Status, SubscribedAt |
| `PostgreSQL/NewsletterXDbContext.cs` | EF Core context with query filters, snake_case |
| `DynamoDB/EmailDispatchLogEntity.cs` | PK/SK design, TTL, audit attributes |
| `Migrations/*.cs` | EF Core migration files (auto-generated) |

**Key Concepts:**
- BaseEntity provides audit fields automatically
- Global query filters exclude soft-deleted records
- Snake_case naming matches PostgreSQL conventions
- DynamoDB uses composite key (NewsletterType + Timestamp)

---

## src/NewsletterX.Repositories/

**Purpose:** Data access layer with interface-based design for testability.

| File | Purpose |
|------|---------|
| `NewsletterX.Repositories.csproj` | References EF Core, AWS SDK |
| `Interfaces/IUserRepository.cs` | GetById, GetByEmail, Create, SoftDelete, Exists |
| `Interfaces/ISubscriptionRepository.cs` | GetByUser, GetActiveSubscribers, Create, UpdateStatus |
| `Interfaces/IEmailDispatchLogRepository.cs` | LogDispatch, LogBatch, UpdateStatus, GetHistory |
| `Implementations/UserRepository.cs` | EF Core queries with AsNoTracking |
| `Implementations/SubscriptionRepository.cs` | Includes User for email dispatch |
| `Implementations/EmailDispatchLogRepository.cs` | DynamoDB low-level API with conditional writes |
| `Extensions/RepositoryServiceCollectionExtensions.cs` | AddRepositories() DI registration |

**Key Concepts:**
- Interfaces enable mocking in unit tests
- AsNoTracking() improves read performance
- DynamoDB uses conditional writes for idempotency
- Batch operations handle DynamoDB 25-item limit

---

## src/NewsletterX.Providers/

**Purpose:** Business logic layer that orchestrates repositories and implements rules.

| File | Purpose |
|------|---------|
| `NewsletterX.Providers.csproj` | References BCrypt, JWT, AWS SDK |
| `Interfaces/IAuthProvider.cs` | Register, Login, GetCurrentUser |
| `Interfaces/ISubscriptionProvider.cs` | Subscribe, Unsubscribe, GetSubscriptions |
| `Interfaces/INewsletterDispatchProvider.cs` | DispatchNewsletter |
| `Interfaces/ISnsPublisher.cs` | Generic SNS publish wrapper |
| `Implementations/AuthProvider.cs` | BCrypt hashing, JWT generation, login logic |
| `Implementations/SubscriptionProvider.cs` | Duplicate checking, resubscribe logic |
| `Implementations/NewsletterDispatchProvider.cs` | Batching, SNS publishing |
| `Implementations/SnsPublisher.cs` | JSON serialization, message attributes |
| `Options/JwtOptions.cs` | Issuer, Audience, SecretKey, Expiration |
| `Options/SnsOptions.cs` | TopicArn |
| `Options/SqsOptions.cs` | QueueUrls, WaitTime, VisibilityTimeout |
| `Options/DynamoDbOptions.cs` | TableName, TtlDays |
| `Messages/NewsletterDispatchMessage.cs` | SNS/SQS message schema |
| `Extensions/ProviderServiceCollectionExtensions.cs` | AddProviders() DI registration |

**Key Concepts:**
- Providers return Result<T> instead of throwing exceptions
- BCrypt work factor of 12 balances security and speed
- JWT includes sub (userId), email, iat, exp claims
- Dispatch batches subscribers (100 per SNS message)

---

## src/NewsletterX.Api/

**Purpose:** HTTP layer with controllers and middleware.

| File | Purpose |
|------|---------|
| `NewsletterX.Api.csproj` | References Serilog for LogContext |
| `Controllers/HealthController.cs` | GET /health (liveness check) |
| `Controllers/AuthController.cs` | POST /register, POST /login, GET /me |
| `Controllers/SubscriptionsController.cs` | CRUD for subscriptions |
| `Middleware/CorrelationIdMiddleware.cs` | Extracts/generates X-Correlation-Id |
| `Middleware/ExceptionHandlingMiddleware.cs` | Global exception handler |

**Key Concepts:**
- Controllers only handle HTTP concerns (status codes, routing)
- Correlation ID flows through logs for distributed tracing
- Exception middleware returns consistent error format
- AllowAnonymous on auth endpoints, Authorize on others

---

## src/NewsletterX.Host/

**Purpose:** Application entry point, DI composition, configuration.

| File | Purpose |
|------|---------|
| `NewsletterX.Host.csproj` | References all other projects, Serilog, Swagger |
| `Program.cs` | DI registration, middleware pipeline |
| `appsettings.json` | All configuration with documentation |
| `appsettings.Development.json` | Dev overrides (verbose logging) |
| `Dockerfile` | Multi-stage build for production image |
| `Configuration/JwtConfiguration.cs` | JWT bearer authentication setup |
| `Configuration/AwsConfiguration.cs` | AWS client singletons |
| `Configuration/DatabaseConfiguration.cs` | EF Core + Npgsql setup |
| `Configuration/RateLimitingConfiguration.cs` | Fixed window rate limiter |
| `Configuration/HealthCheckConfiguration.cs` | Health check endpoints |
| `BackgroundServices/EmailSenderWorker.cs` | SQS consumer, sends emails |
| `BackgroundServices/AuditLogWorker.cs` | SQS consumer, writes to DynamoDB |
| `BackgroundServices/WeeklyNewsletterJob.cs` | Scheduled dispatch job |
| `HealthChecks/SqsHealthCheck.cs` | Checks SQS queue reachability |
| `HealthChecks/DynamoDbHealthCheck.cs` | Checks DynamoDB table status |

**Key Concepts:**
- AWS clients registered as singletons (connection pooling)
- DbContext registered as scoped (one per request)
- Middleware order: Correlation → Exception → Serilog → RateLimiter → Auth
- Background services use CancellationToken for graceful shutdown

---

## test/NewsletterX.Tests/

**Purpose:** Automated tests for the application.

| File | Purpose |
|------|---------|
| `NewsletterX.Tests.csproj` | References xUnit, Moq, FluentAssertions |
| `Unit/AuthProviderTests.cs` | Unit tests for AuthProvider with mocked repos |

**Test Cases:**
1. `RegisterAsync_WithValidRequest_ReturnsSuccess`
2. `RegisterAsync_WithExistingEmail_ReturnsFailure`
3. `LoginAsync_WithValidCredentials_ReturnsSuccess`
4. `LoginAsync_WithInvalidPassword_ReturnsFailure`
5. `LoginAsync_WithNonExistentUser_ReturnsFailure`

**Key Concepts:**
- Mock repositories to test business logic in isolation
- FluentAssertions for readable test assertions
- Test both happy path and error cases

---

## Dependency Flow

```
NewsletterX.Host
       │
       ├── NewsletterX.Api
       │       └── NewsletterX.Providers
       │               ├── NewsletterX.Repositories
       │               │       └── NewsletterX.EntityModels
       │               │               └── NewsletterX.Types
       │               └── NewsletterX.ContractModels
       │                       └── NewsletterX.Types
       │
       └── All other projects (for DI registration)
```

**Rule:** Dependencies flow downward only. No circular references.

---

## Quick Commands

```bash
# Build
dotnet build

# Run
cd src/NewsletterX.Host && dotnet run

# Test
dotnet test

# Start infrastructure
cd deployment && docker-compose up -d

# Create migration
dotnet ef migrations add Name --project src/NewsletterX.EntityModels --startup-project src/NewsletterX.Host

# Apply migration
dotnet ef database update --project src/NewsletterX.EntityModels --startup-project src/NewsletterX.Host
```

