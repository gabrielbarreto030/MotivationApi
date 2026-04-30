# Motivation API

REST API for a personal motivation system, built with .NET 8 following DDD (Domain-Driven Design) and SOLID principles.

## Technologies

- **.NET 8** - Main framework
- **DDD** - Domain-Driven Design (Domain, Application, Infrastructure, API)
- **EF Core InMemory** - In-memory persistence
- **JWT Bearer** - Token-based authentication
- **Serilog** - Structured logging (console + file)
- **IMemoryCache** - Fast query caching
- **Swagger/OpenAPI** - Interactive documentation
- **HealthChecks** - Application health monitoring
- **xUnit** - Unit and integration tests

---

# Backlog (30 days)

Phase 1 - Foundations (Days 1-5)

Day 1: Solution + project scaffold (DDD structure) - OK
Day 2: Basic API setup + Swagger + HealthCheck - OK
Day 3: Domain entities (User, Goal, Step, Motivation) - OK
Day 4: EF Core InMemory + DbContext - OK
Day 5: Basic repository pattern - OK

Phase 2 - Authentication (Days 6-8)

Day 6: User registration - OK
Day 7: Login + JWT generation - OK
Day 8: Protect endpoints with Bearer auth - OK

Phase 3 - Goals (Days 9-13)

Day 9: Create Goal - OK
Day 10: List Goals by user - OK
Day 11: Update Goal - OK
Day 12: Delete Goal - OK
Day 13: Cache for Goal queries - OK

Phase 4 - Steps (Days 14-17)

Day 14: Create Step - OK
Day 15: List Steps - OK
Day 16: Mark Step as completed - OK
Day 17: Calculate Goal progress - OK

Phase 5 - Motivation Engine (Days 18-21)

Day 18: Add Motivation - OK
Day 19: Remove Motivation - OK
Day 20: Service that generates the daily message - OK
Day 21: Endpoint to get the daily motivation - OK

Phase 6 - Quality & Observability (Days 22-26)

Day 22: Structured logging with Serilog - OK
Day 23: Global error middleware - OK
Day 24: Domain unit tests - OK
Day 25: Application unit tests - OK
Day 26: API integration tests - OK

Phase 7 - Polishing (Days 27-30)

Day 27: Detailed Swagger documentation - OK
Day 28: Advanced HealthChecks - OK
Day 29: SOLID improvements & light refactors - OK
Day 30: Final README + usage examples - OK

Phase 8 - Consolidation (Days 31+)

Day 31: Refactor AuthService and GoalService (remove unnecessary dependencies) - OK
Day 32: Pagination in Goal and Step listings - OK
Day 33: Advanced listing filters (Goals by status, Steps by isCompleted) - OK
Day 34: Sorting options for Goals and Steps listings (sortBy, sortOrder) - OK
Day 35: User goals summary endpoint (GET /goals/summary with stats by status and steps) - OK
Day 36: Goal deadline tracking (Deadline field on Goal + GET /goals/overdue endpoint) - OK
Day 37: Goal priority levels (GoalPriority enum: None/Low/Medium/High + filter and sort by priority) - OK
Day 38: Goal notes field (optional Notes on Goal entity: set on create/update, clear with ClearNotes flag, included in all responses) - OK
Day 39: Step notes field (optional Notes on Step entity: set on create, update/clear via PUT /goals/{goalId}/steps/{stepId}, included in all responses) - OK
Day 40: Step title update (allow updating Step title via PUT /goals/{goalId}/steps/{stepId}: Title field added to UpdateStepRequest, UpdateAsync replaces UpdateNotesAsync handling both title and notes atomically) - OK
Day 41: Step due date (optional DueDate on Step entity: set on create/update, clear with ClearDueDate flag, IsOverdue computed field in all responses, GET /goals/{goalId}/steps/overdue endpoint) - OK
Day 42: Step priority levels (StepPriority enum: None/Low/Medium/High + filter and sort by priority on steps listing) - OK

---

## Project Structure

```text
DOTNET/
|-- src/
|   |-- Motivation.Api/                  # Presentation layer (Controllers, Middleware)
|   |   |-- Controllers/
|   |   |   |-- UsersController.cs
|   |   |   |-- GoalsController.cs
|   |   |   |-- StepsController.cs
|   |   |   |-- MotivationsController.cs
|   |   |   `-- DailyMessageController.cs
|   |   |-- Middleware/
|   |   |   `-- GlobalExceptionMiddleware.cs
|   |   |-- HealthChecks/
|   |   |   `-- HealthCheckResponseWriter.cs
|   |   |-- Models/
|   |   |   |-- LoginRequestDto.cs
|   |   |   `-- RegisterRequestDto.cs
|   |   |-- Services/
|   |   |   `-- CurrentUserService.cs
|   |   `-- Program.cs
|   |
|   |-- Motivation.Application/          # Application layer (Use Cases, DTOs, Interfaces)
|   |   |-- DTOs/
|   |   |-- Interfaces/
|   |   |-- Services/
|   |   `-- Exceptions/
|   |
|   |-- Motivation.Domain/               # Domain layer (Entities, Business Rules)
|   |   |-- Entities/
|   |   |   |-- User.cs
|   |   |   |-- Goal.cs
|   |   |   |-- Step.cs
|   |   |   |-- Motivation.cs
|   |   |   `-- GoalStatus.cs
|   |   `-- Interfaces/                  # Repository contracts
|   |
|   `-- Motivation.Infrastructure/       # Infrastructure layer (EF Core, JWT, Cache)
|       |-- Db/
|       |   `-- AppDbContext.cs
|       |-- Repositories/
|       |-- Services/
|       |   `-- JwtService.cs
|       `-- HealthChecks/
|
`-- tests/
    `-- Motivation.UnitTests/            # Unit and integration tests
```

---

## How to Run

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running the API

```bash
# Clone the repository
git clone <repository-url>
cd DOTNET

# Restore dependencies
dotnet restore

# Run the API
dotnet run --project src/Motivation.Api

# The API will be available at:
# http://localhost:5000
# https://localhost:5001
# Swagger UI: http://localhost:5000/swagger
```

### Running the Tests

```bash
dotnet test
```

---

## Configuration

`src/Motivation.Api/appsettings.json`:

```json
{
  "Jwt": {
    "Key": "super_secret_key_12345_32bytes_min",
    "Issuer": "motivation",
    "Audience": "motivation",
    "ExpiresMinutes": "120"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": { "path": "logs/motivation-.log", "rollingInterval": "Day" }
      }
    ]
  }
}
```

---

## API Endpoints

### Authentication

| Method | Route             | Auth | Description                   |
|--------|-------------------|------|-------------------------------|
| POST   | `/users/register` | No   | Register a new user           |
| POST   | `/users/login`    | No   | Log in and get a JWT token    |
| GET    | `/users/profile`  | Yes  | Authenticated user profile    |

### Goals

| Method | Route                   | Auth | Description                    |
|--------|-------------------------|------|--------------------------------|
| POST   | `/goals`                | Yes  | Create a new goal              |
| GET    | `/goals`                | Yes  | List the user's goals          |
| PUT    | `/goals/{id}`           | Yes  | Update a goal                  |
| DELETE | `/goals/{id}`           | Yes  | Delete a goal                  |
| GET    | `/goals/{id}/progress`  | Yes  | Calculate goal progress        |

### Steps

| Method | Route                                     | Auth | Description                    |
|--------|-------------------------------------------|------|--------------------------------|
| POST   | `/goals/{goalId}/steps`                   | Yes  | Create a step for a goal       |
| GET    | `/goals/{goalId}/steps`                   | Yes  | List a goal's steps            |
| PUT    | `/goals/{goalId}/steps/{stepId}/complete` | Yes  | Mark a step as completed       |

### Motivations

| Method | Route                                          | Auth | Description                    |
|--------|------------------------------------------------|------|--------------------------------|
| POST   | `/goals/{goalId}/motivations`                  | Yes  | Add a motivational phrase      |
| DELETE | `/goals/{goalId}/motivations/{motivationId}`   | Yes  | Remove a motivational phrase   |

### Daily Message

| Method | Route            | Auth | Description                         |
|--------|------------------|------|-------------------------------------|
| GET    | `/daily-message` | Yes  | Get the motivational message of the day |

### Health Checks

| Method | Route            | Auth | Description                    |
|--------|-----------------|------|--------------------------------|
| GET    | `/health`       | No   | Full status (DB + Cache)       |
| GET    | `/health/live`  | No   | Liveness probe                 |
| GET    | `/health/ready` | No   | Readiness probe (DB + Cache)   |

---

## Usage Examples

### 1. Register a User

```bash
curl -X POST http://localhost:5000/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "joao@example.com",
    "password": "Password@123"
  }'
```

**201 Created response:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "joao@example.com"
}
```

---

### 2. Log In

```bash
curl -X POST http://localhost:5000/users/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "joao@example.com",
    "password": "Password@123"
  }'
```

**200 OK response:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "joao@example.com",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIzZmE4NWY2NC01NzE3LTQ1NjItYjNmYy0yYzk2M2Y2NmFmYTYiLCJlbWFpbCI6ImpvYW9AZXhhbXBsZS5jb20iLCJleHAiOjE3MTMxNjgwMDB9.abc123"
}
```

> **Save the token.** Use it in the `Authorization: Bearer {token}` header for all remaining requests.

---

### 3. Create a Goal

```bash
curl -X POST http://localhost:5000/goals \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Learn .NET 8",
    "description": "Master C# and ASP.NET Core with DDD"
  }'
```

**201 Created response:**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Learn .NET 8",
  "description": "Master C# and ASP.NET Core with DDD",
  "status": "Pending",
  "createdAt": "2024-04-10T14:30:00Z"
}
```

---

### 4. List Goals

```bash
curl http://localhost:5000/goals \
  -H "Authorization: Bearer {token}"
```

**200 OK response:**
```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "title": "Learn .NET 8",
    "description": "Master C# and ASP.NET Core with DDD",
    "status": "Pending",
    "createdAt": "2024-04-10T14:30:00Z"
  }
]
```

---

### 5. Update a Goal

```bash
curl -X PUT http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Learn .NET 8 and Azure",
    "status": "InProgress"
  }'
```

**Available statuses:** `Pending`, `InProgress`, `Completed`, `Cancelled`

**200 OK response:**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Learn .NET 8 and Azure",
  "description": "Master C# and ASP.NET Core with DDD",
  "status": "InProgress",
  "createdAt": "2024-04-10T14:30:00Z"
}
```

---

### 6. Add Steps to a Goal

```bash
curl -X POST http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/steps \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Complete a basic C# course"
  }'
```

**201 Created response:**
```json
{
  "id": "b2c3d4e5-f6a7-8901-bcde-f01234567891",
  "goalId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Complete a basic C# course",
  "isCompleted": false,
  "completedAt": null
}
```

---

### 7. Mark a Step as Completed

```bash
curl -X PUT http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/steps/b2c3d4e5-f6a7-8901-bcde-f01234567891/complete \
  -H "Authorization: Bearer {token}"
```

**200 OK response:**
```json
{
  "id": "b2c3d4e5-f6a7-8901-bcde-f01234567891",
  "goalId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Complete a basic C# course",
  "isCompleted": true,
  "completedAt": "2024-04-10T15:00:00Z"
}
```

---

### 8. Check Goal Progress

```bash
curl http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/progress \
  -H "Authorization: Bearer {token}"
```

**200 OK response:**
```json
{
  "goalId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "totalSteps": 3,
  "completedSteps": 1,
  "progressPercentage": 33.33
}
```

---

### 9. Add a Motivational Phrase

```bash
curl -X POST http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/motivations \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Consistency is the key to mastery!"
  }'
```

**201 Created response:**
```json
{
  "id": "c3d4e5f6-a7b8-9012-cdef-012345678902",
  "goalId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "text": "Consistency is the key to mastery!"
}
```

---

### 10. Get the Daily Motivational Message

```bash
curl http://localhost:5000/daily-message \
  -H "Authorization: Bearer {token}"
```

**200 OK response:**
```json
{
  "message": "Consistency is the key to mastery!",
  "date": "2024-04-10"
}
```

> The message rotates daily among the phrases saved in the user's goals. If there are no phrases, it returns: *"Keep going! Every step forward is progress."*

---

### 11. Delete a Motivational Phrase

```bash
curl -X DELETE http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/motivations/c3d4e5f6-a7b8-9012-cdef-012345678902 \
  -H "Authorization: Bearer {token}"
```

**Response:** `204 No Content`

---

### 12. Delete a Goal

```bash
curl -X DELETE http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer {token}"
```

**Response:** `204 No Content`

---

### 13. Check API Health

```bash
# Full status with details
curl http://localhost:5000/health

# Liveness probe
curl http://localhost:5000/health/live

# Readiness probe
curl http://localhost:5000/health/ready
```

**Health response:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0050000"
    },
    "memory-cache": {
      "status": "Healthy",
      "duration": "00:00:00.0001000"
    }
  }
}
```

---

## Error Codes

| Code | Description                                            |
|------|--------------------------------------------------------|
| 400  | Bad Request - invalid or missing data                  |
| 401  | Unauthorized - missing, invalid, or expired token      |
| 403  | Forbidden - resource belongs to another user           |
| 404  | Not Found - resource not found                         |
| 409  | Conflict - email already registered or step completed  |
| 500  | Internal Server Error - unexpected server error        |

**Standard error format:**
```json
{
  "error": "Problem description"
}
```

---

## Full Usage Flow

```text
1. POST /users/register                 -> Create account
2. POST /users/login                    -> Get JWT token
3. POST /goals                          -> Create a goal
4. POST /goals/{id}/steps               -> Add steps to the goal (repeat as needed)
5. PUT  /goals/{id}/steps/{stepId}/complete -> Complete a step
6. GET  /goals/{id}/progress            -> Check progress (%)
7. POST /goals/{id}/motivations         -> Add motivational phrases
8. GET  /daily-message                  -> Receive the daily motivational message
9. PUT  /goals/{id}                     -> Update the goal status when finished
```

---

## Architecture

```text
+-----------------------------------------------------+
|                   Motivation.Api                    |
|   Controllers -> Middleware -> Services -> DTOs     |
+------------------------+----------------------------+
                         |
+------------------------v----------------------------+
|               Motivation.Application                |
|   Services -> Interfaces -> DTOs -> Exceptions      |
+------------------------+----------------------------+
                         |
+------------------------v----------------------------+
|                 Motivation.Domain                   |
|        Entities -> Interfaces (Repositories)        |
+------------------------+----------------------------+
                         |
+------------------------v----------------------------+
|             Motivation.Infrastructure               |
|   EF Core -> Repositories -> JwtService -> Cache    |
+-----------------------------------------------------+
```

### Applied SOLID Principles

- **S** - Single Responsibility: each service has a single responsibility (`AuthService`, `GoalService`, `MotivationService`, etc.)
- **O** - Open/Closed: extensible through interfaces and closed for direct modification
- **L** - Liskov Substitution: implementations can replace interfaces without breaking the contract
- **I** - Interface Segregation: specific interfaces per domain (`IGoalService`, `IStepService`, etc.)
- **D** - Dependency Inversion: dependencies go through abstractions (interfaces injected via DI)

---

## Technical Decisions

| Decision | Reason |
|----------|--------|
| EF Core InMemory | Simplicity for development without an external database |
| IMemoryCache for Goals | Reduces repeated database reads on frequent queries |
| JWT with a 120-minute expiration | Balances security and usability |
| Serilog with daily rolling files | Log traceability without unlimited growth |
| GlobalExceptionMiddleware | Centralized error handling with standardized responses |
| GoalStatus as an enum | Ensures valid domain values without magic strings |
| DailyMessage rotation by day of year | Variety without scheduling complexity |
