# NewsletterX

A **production-grade** newsletter subscription service built with C# / ASP.NET Core 8. This project demonstrates enterprise infrastructure patterns and best practices — the business idea is a vehicle to showcase that **given any idea, the surrounding infrastructure is just a few tweaks**.

## 🎯 Purpose

This is a **portfolio project** designed to demonstrate:
- Clean layered architecture that scales
- Production-ready AWS integration (SNS, SQS, DynamoDB)
- Infrastructure patterns transferable to any microservice
- Real-world concerns: connection pooling, idempotency, observability

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        NewsletterX.Host                          │
│                    (Composition Root)                            │
│         Program.cs | DI | Configuration | Background Services    │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                        NewsletterX.Api                           │
│                      (HTTP Layer)                                │
│            Controllers | Middleware | Filters                    │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                     NewsletterX.Providers                        │
│                    (Business Logic)                              │
│     IAuthProvider | ISubscriptionProvider | ISnsPublisher        │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    NewsletterX.Repositories                      │
│                      (Data Access)                               │
│      IUserRepository | ISubscriptionRepository (PostgreSQL)      │
│              IEmailLogRepository (DynamoDB)                      │
└─────────────────────────────────────────────────────────────────┘
                                │
                ┌───────────────┴───────────────┐
                ▼                               ▼
┌───────────────────────────┐    ┌───────────────────────────┐
│  NewsletterX.EntityModels │    │   NewsletterX.Types       │
│   EF Core + DynamoDB      │    │  Enums | Constants        │
└───────────────────────────┘    └───────────────────────────┘
```

### Call Chain
```
HTTP Request → Controller → Provider → Repository → Database/AWS
```

## 📋 Features

| Feature | Status | Description |
|---------|--------|-------------|
| JWT Authentication | 🔜 | Register, login, protected routes |
| User Management | 🔜 | CRUD operations with soft delete |
| Subscriptions | 🔜 | Subscribe/unsubscribe to newsletters |
| SNS Fan-out | 🔜 | One publish → multiple SQS queues |
| SQS Consumers | 🔜 | Background workers with DLQ |
| DynamoDB Audit | 🔜 | Append-only dispatch log with TTL |
| Rate Limiting | 🔜 | Protect auth endpoints |
| Health Checks | ✅ | DB + SQS connectivity checks |
| Structured Logging | ✅ | Serilog with correlation IDs |

## 🛠️ Tech Stack

- **Runtime**: .NET 8 / ASP.NET Core
- **Primary DB**: PostgreSQL via EF Core
- **Audit Log**: AWS DynamoDB
- **Messaging**: AWS SNS + SQS
- **Auth**: JWT Bearer tokens
- **Logging**: Serilog (structured)
- **Local AWS**: LocalStack
- **Containers**: Docker

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [AWS CLI](https://aws.amazon.com/cli/) (optional, for verifying LocalStack)

### 1. Start Infrastructure
```bash
cd deployment
docker-compose up -d
```

This starts:
- PostgreSQL on `localhost:5432`
- LocalStack on `localhost:4566` (SNS, SQS, DynamoDB)

### 2. Run the API
```bash
cd src/NewsletterX.Host
dotnet run
```

### 3. Open Swagger UI
Navigate to: http://localhost:5000

### 4. Verify LocalStack Resources
```bash
# List SNS topics
aws --endpoint-url=http://localhost:4566 sns list-topics

# List SQS queues
aws --endpoint-url=http://localhost:4566 sqs list-queues

# List DynamoDB tables
aws --endpoint-url=http://localhost:4566 dynamodb list-tables
```

## 📁 Project Structure

```
NewsletterX/
├── src/
│   ├── NewsletterX.Host/              # Entry point, DI, config
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Configuration/             # Strongly-typed options
│   │   ├── BackgroundServices/        # SQS consumers, scheduled jobs
│   │   └── HealthChecks/
│   │
│   ├── NewsletterX.Api/               # HTTP layer
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   └── Filters/
│   │
│   ├── NewsletterX.Providers/         # Business logic
│   │   ├── Interfaces/
│   │   ├── Implementations/
│   │   ├── Options/                   # Provider-specific config
│   │   └── Messages/                  # SQS/SNS message DTOs
│   │
│   ├── NewsletterX.Repositories/      # Data access
│   │   ├── Interfaces/
│   │   ├── Implementations/
│   │   └── Extensions/
│   │
│   ├── NewsletterX.EntityModels/      # Persistence models
│   │   ├── PostgreSQL/                # EF Core entities
│   │   └── DynamoDB/                  # DynamoDB documents
│   │
│   ├── NewsletterX.ContractModels/    # API DTOs
│   │   ├── Requests/
│   │   ├── Responses/
│   │   └── Validators/                # FluentValidation
│   │
│   └── NewsletterX.Types/             # Shared primitives
│       ├── Enums/
│       └── Constants/
│
├── test/
│   └── NewsletterX.Tests/
│       ├── Unit/
│       └── Integration/
│
├── deployment/
│   ├── docker-compose.yml
│   └── localstack-init/
│       └── init-aws.sh                # Creates SNS/SQS/DynamoDB
│
└── NewsletterX.sln
```

## 🔑 Key Patterns Demonstrated

### 1. Connection Pool Awareness
```csharp
// PostgreSQL: Npgsql pools connections automatically
"Max Pool Size=50;Min Pool Size=5;Connection Idle Lifetime=300"

// DynamoDB: Client MUST be singleton (manages own HTTP pool)
services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
```

### 2. SNS → SQS Fan-out
```
NewsletterDispatchProvider.DispatchAsync()
        │
        ▼
   SNS Topic (newsletter-dispatch)
        │
        ├────────────────┬────────────────┐
        ▼                ▼                ▼
   SQS Queue        SQS Queue       (future queues)
   (email-send)     (audit-log)
        │                │
        ▼                ▼
   EmailSender      AuditLogWorker
   Worker           → DynamoDB
```

### 3. DynamoDB Key Design
```
Table: EmailDispatchLog
PK: NewsletterType (e.g., "weekly")
SK: DispatchTimestamp#DispatchId

TTL: Auto-delete after 90 days
```

### 4. Structured Logging with Correlation IDs
```
[10:23:45 INF] abc123 | Processing subscription for user@example.com
[10:23:46 INF] abc123 | Published to SNS: newsletter-dispatch
[10:23:47 INF] abc123 | Logged dispatch to DynamoDB
```

## 📚 Architectural Decisions

Each file contains detailed comments explaining **why** decisions were made. Key documents:
- `src/NewsletterX.Host/appsettings.json` - Configuration rationale
- `deployment/docker-compose.yml` - Infrastructure decisions
- `deployment/localstack-init/init-aws.sh` - AWS resource design
- Project `.csproj` files - Dependency reasoning

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 📖 License

MIT

---

**Built to demonstrate production-grade infrastructure thinking.** The newsletter is just the vehicle — the patterns are the product.

