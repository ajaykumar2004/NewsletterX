# NewsletterX - API Reference

> Complete API documentation for the NewsletterX REST API.

---

## Base URL

```
Development: http://localhost:5000
Production:  https://api.newsletterx.com
```

## Authentication

All endpoints except `/api/auth/register` and `/api/auth/login` require a JWT bearer token.

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Endpoints

### Authentication

#### Register a new user

```http
POST /api/auth/register
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "SecureP@ssw0rd!",
    "displayName": "John Doe"
}
```

**Validation Rules:**
- `email`: Required, valid email format, max 320 characters
- `password`: Required, min 8 characters, must contain uppercase, lowercase, digit, special char
- `displayName`: Optional, max 100 characters

**Responses:**

```json
// 201 Created
{
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "tokenType": "Bearer",
    "expiresIn": 3600,
    "expiresAt": "2024-01-15T11:30:00Z",
    "user": {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "email": "user@example.com",
        "displayName": "John Doe",
        "emailVerified": false,
        "createdAt": "2024-01-15T10:30:00Z"
    }
}

// 400 Bad Request (Validation Error)
{
    "errorCode": "VAL_2001",
    "message": "One or more validation errors occurred.",
    "details": {
        "email": ["Invalid email format."],
        "password": ["Password must contain at least one uppercase letter."]
    },
    "traceId": "abc123def456"
}

// 409 Conflict (Email Exists)
{
    "errorCode": "AUTH_1005",
    "message": "An account with this email already exists.",
    "traceId": "abc123def456"
}
```

---

#### Login

```http
POST /api/auth/login
Content-Type: application/json

{
    "email": "user@example.com",
    "password": "SecureP@ssw0rd!"
}
```

**Responses:**

```json
// 200 OK
{
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "tokenType": "Bearer",
    "expiresIn": 3600,
    "expiresAt": "2024-01-15T11:30:00Z",
    "user": {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "email": "user@example.com",
        "displayName": "John Doe",
        "emailVerified": false,
        "createdAt": "2024-01-15T10:30:00Z"
    }
}

// 401 Unauthorized
{
    "errorCode": "AUTH_1001",
    "message": "Invalid email or password.",
    "traceId": "abc123def456"
}
```

---

#### Get Current User

```http
GET /api/auth/me
Authorization: Bearer <token>
```

**Responses:**

```json
// 200 OK
{
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "displayName": "John Doe",
    "emailVerified": false,
    "createdAt": "2024-01-15T10:30:00Z"
}

// 401 Unauthorized
{
    "errorCode": "AUTH_1003",
    "message": "Invalid token.",
    "traceId": "abc123def456"
}
```

---

### Subscriptions

#### Subscribe to a Newsletter

```http
POST /api/subscriptions
Authorization: Bearer <token>
Content-Type: application/json

{
    "newsletterType": "Weekly"
}
```

**Newsletter Types:**
- `Weekly` (0)
- `Monthly` (1)
- `BreakingNews` (2)
- `ProductUpdates` (3)

**Responses:**

```json
// 201 Created
{
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "newsletterType": "Weekly",
    "status": "Active",
    "subscribedAt": "2024-01-15T10:30:00Z",
    "unsubscribedAt": null
}

// 409 Conflict (Already Subscribed)
{
    "errorCode": "SUB_3001",
    "message": "Already subscribed to Weekly newsletter.",
    "traceId": "abc123def456"
}
```

---

#### Get All Subscriptions

```http
GET /api/subscriptions
Authorization: Bearer <token>
```

**Response:**

```json
// 200 OK
[
    {
        "id": "660e8400-e29b-41d4-a716-446655440001",
        "newsletterType": "Weekly",
        "status": "Active",
        "subscribedAt": "2024-01-15T10:30:00Z",
        "unsubscribedAt": null
    },
    {
        "id": "770e8400-e29b-41d4-a716-446655440002",
        "newsletterType": "Monthly",
        "status": "Unsubscribed",
        "subscribedAt": "2024-01-10T08:00:00Z",
        "unsubscribedAt": "2024-01-14T15:00:00Z"
    }
]
```

---

#### Get Single Subscription

```http
GET /api/subscriptions/{id}
Authorization: Bearer <token>
```

**Responses:**

```json
// 200 OK
{
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "newsletterType": "Weekly",
    "status": "Active",
    "subscribedAt": "2024-01-15T10:30:00Z",
    "unsubscribedAt": null
}

// 404 Not Found
{
    "errorCode": "SUB_3003",
    "message": "Subscription not found.",
    "traceId": "abc123def456"
}
```

---

#### Unsubscribe

```http
DELETE /api/subscriptions/{id}
Authorization: Bearer <token>
Content-Type: application/json

{
    "reason": "Too many emails"
}
```

**Note:** The request body is optional.

**Responses:**

```json
// 204 No Content

// 404 Not Found
{
    "errorCode": "SUB_3003",
    "message": "Subscription not found.",
    "traceId": "abc123def456"
}
```

---

### Health Checks

#### Basic Health Check (Liveness)

```http
GET /health
```

**Response:**

```json
// 200 OK
{
    "status": "Healthy",
    "totalDuration": 0.5,
    "checks": []
}
```

---

#### Full Health Check (Readiness)

```http
GET /health/ready
```

**Response:**

```json
// 200 OK
{
    "status": "Healthy",
    "totalDuration": 45.23,
    "checks": [
        {
            "name": "postgresql",
            "status": "Healthy",
            "duration": 12.5,
            "description": null,
            "exception": null
        },
        {
            "name": "sqs",
            "status": "Healthy",
            "duration": 23.1,
            "description": "SQS reachable. Pending messages: 0",
            "exception": null
        },
        {
            "name": "dynamodb",
            "status": "Healthy",
            "duration": 9.6,
            "description": "DynamoDB table 'EmailDispatchLog' is active.",
            "exception": null
        }
    ]
}

// 503 Service Unavailable
{
    "status": "Unhealthy",
    "totalDuration": 5012.3,
    "checks": [
        {
            "name": "postgresql",
            "status": "Unhealthy",
            "duration": 5000.0,
            "description": null,
            "exception": "Connection timeout"
        }
    ]
}
```

---

#### Database Health Check

```http
GET /health/db
```

Returns only database health status.

---

## Error Codes

### Authentication Errors (AUTH_1xxx)

| Code | Description |
|------|-------------|
| AUTH_1001 | Invalid email or password |
| AUTH_1002 | Token expired |
| AUTH_1003 | Token invalid |
| AUTH_1004 | User not found |
| AUTH_1005 | Email already exists |

### Validation Errors (VAL_2xxx)

| Code | Description |
|------|-------------|
| VAL_2001 | General validation failure |
| VAL_2002 | Invalid email format |
| VAL_2003 | Weak password |
| VAL_2004 | Invalid newsletter type |

### Subscription Errors (SUB_3xxx)

| Code | Description |
|------|-------------|
| SUB_3001 | Already subscribed |
| SUB_3002 | Not subscribed |
| SUB_3003 | Subscription not found |

### System Errors (SYS_5xxx)

| Code | Description |
|------|-------------|
| SYS_5001 | Internal server error |
| SYS_5002 | Database error |
| SYS_5003 | External service error |
| SYS_5004 | Rate limit exceeded |

---

## Rate Limiting

Auth endpoints (`/api/auth/*`) are rate limited:

- **Limit:** 10 requests per minute per IP
- **Window:** Fixed window (60 seconds)

When rate limited, you'll receive:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 45

{
    "errorCode": "SYS_5004",
    "message": "Too many requests. Please try again later."
}
```

---

## Headers

### Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Authorization` | Yes* | Bearer token for authenticated endpoints |
| `Content-Type` | Yes | `application/json` for POST/PUT/DELETE |
| `X-Correlation-Id` | No | Your correlation ID (auto-generated if missing) |

### Response Headers

| Header | Description |
|--------|-------------|
| `X-Correlation-Id` | Correlation ID for request tracing |
| `Retry-After` | Seconds to wait when rate limited |
| `Token-Expired` | Present if JWT token has expired |

---

## Swagger UI

Interactive API documentation is available at:

```
http://localhost:5000/
```

Click "Authorize" and enter your JWT token to test authenticated endpoints.

