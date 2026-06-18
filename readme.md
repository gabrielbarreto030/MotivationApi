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
Day 43: Goal archiving (IsArchived flag on Goal entity: POST /goals/{id}/archive, DELETE /goals/{id}/archive, GET /goals/archived; default listing excludes archived; includeArchived=true query param to include them) - OK
Day 44: List Motivations (GET /goals/{goalId}/motivations endpoint: ListByGoalAsync added to IMotivationService and MotivationService, returns all motivational phrases for a goal with ownership validation) - OK
Day 45: Update Motivation text (allow editing motivation text via PUT /goals/{goalId}/motivations/{motivationId}: UpdateText method on Motivation entity, UpdateAsync on repository and service, returns updated motivation; also fixed Goal defensive copy to preserve IsArchived) - OK
Day 46: Step completion undo (allow reverting a completed step back to incomplete via DELETE /goals/{goalId}/steps/{stepId}/complete: Uncomplete method on Step entity, UncompleteAsync on IStepService and StepService, returns updated step with IsCompleted=false and CompletedAt=null; throws 409 if step is not completed) - OK
Day 47: Goal clone (POST /goals/{id}/clone: duplicates a goal with all its steps; cloned goal gets "Copy of" title prefix, Pending status, not archived; steps are cloned with title/notes/dueDate/priority preserved but completion reset to false; CloneAsync added to IGoalService and GoalService; fixed StepUncompleteTests constructor) - OK
Day 48: Change user password (PUT /users/password: authenticated user submits currentPassword + newPassword; validates current password matches, rejects if new equals current, updates hash and persists; UpdatePassword on User entity, UpdateAsync on IUserRepository/UserRepository with cache invalidation, ChangePasswordAsync on IAuthService/AuthService; returns 204 No Content on success, 401 if current password wrong, 400 for validation errors) - OK
Day 49: Change user email (PUT /users/email: authenticated user submits currentPassword + newEmail; validates password, rejects if new email equals current (case-insensitive), checks uniqueness, updates email and persists; UpdateEmail on User entity, UpdateEmailAsync on IUserRepository/UserRepository invalidating both old and new email cache keys, ChangeEmailAsync on IAuthService/AuthService; returns 204 No Content on success, 401 if password wrong, 409 if email taken, 400 for validation errors) - OK
Day 50: Account deletion (DELETE /users/me: authenticated user confirms their password to permanently delete their account; all related goals, steps, and motivations are removed in the same transaction; DeleteAsync on IUserRepository/UserRepository with full cascade cleanup and cache invalidation, DeleteAccountAsync on IAuthService/AuthService; returns 204 No Content on success, 401 if password wrong, 400 for validation errors; data of other users is not affected) - OK
Day 51: Pinned goals (IsPinned flag on Goal entity: Pin()/Unpin() methods; POST /goals/{id}/pin to pin a goal, DELETE /goals/{id}/pin to unpin, GET /goals/pinned to list all pinned goals; business rule: max 3 pinned goals per user, returns 409 if limit exceeded or goal already pinned/not pinned; PinAsync/UnpinAsync/GetPinnedAsync on IGoalService/GoalService; IsPinned included in all goal responses; fixed defensive copy in GoalRepository to preserve IsPinned) - OK
Day 52: Goal completion date tracking (CompletedAt nullable DateTime? on Goal entity: auto-set to UTC now when UpdateStatus transitions to Completed, cleared when transitioning away from Completed, not overwritten if already Completed; constructor accepts optional completedAt for hydration; CompletedAt included in CreateGoalResponse and UpdateGoalResponse; GoalRepository defensive copy updated to preserve CompletedAt) - OK
Day 53: Goal keyword search (search query param on GET /goals: filters goals where Title or Description contains the search term, case-insensitive, partial match; Search property added to GoalFilterRequest with whitespace trimming; filter applied in GoalService.ListByUserFilteredAsync combining with existing status/priority/archive filters; search logged in structured log; GoalsController.List updated with search param; fixed pre-existing build errors in GoalCompletionDateTests.cs) - OK
Day 54: Step reordering (Order int field on Step entity: auto-assigned sequentially on creation via CountByGoalAsync; UpdateOrder method validates order >= 1; PUT /goals/{goalId}/steps/{stepId}/order endpoint to change order; default step listing sorts by Order asc; StepFilterRequest default SortBy changed from "title" to "order"; Order included in all step responses; ReorderAsync on IStepService/StepService; CountByGoalAsync on IStepRepository/StepRepository) - OK
Day 55: Goal tags (optional Tags on Goal entity stored as comma-delimited TagsRaw string: SetTags(IEnumerable<string>?) sets/clears tags, Update() accepts tags/clearTags params; Tags included in CreateGoalRequest/UpdateGoalRequest and in all goal responses; tag query param added to GET /goals for exact case-insensitive match filtering; GoalFilterRequest.Tag property; GoalRepository defensive copy preserves TagsRaw) - OK
Day 56: Step tags (optional Tags on Step entity stored as comma-delimited TagsRaw string: SetTags(IEnumerable<string>?) sets/clears tags; constructor accepts optional tagsRaw for hydration; Tags included in CreateStepRequest/UpdateStepRequest (with ClearTags flag) and in all step responses; tag query param added to GET /goals/{goalId}/steps for exact case-insensitive match filtering combined with existing filters; StepFilterRequest.Tag property with whitespace trimming) - OK
Day 57: Step keyword search (search query param on GET /goals/{goalId}/steps: filters steps where Title or Notes contains the search term, case-insensitive, partial match; Search property added to StepFilterRequest with whitespace trimming; filter applied in StepService.ListByGoalFilteredAsync combining with existing isCompleted/priority/tag filters; search logged in structured log; StepsController.List updated with search param) - OK
Day 58: Motivation keyword search (search query param on GET /goals/{goalId}/motivations: filters motivations where Text contains the search term, case-insensitive, partial match; MotivationFilterRequest DTO with Search and pagination; ListByGoalFilteredAsync added to IMotivationService and MotivationService returning PagedResponse<AddMotivationResponse>; MotivationsController.List updated with search/page/pageSize params; search logged in structured log) - OK
Day 59: Motivation sorting (sortBy and sortOrder query params on GET /goals/{goalId}/motivations: sort by "text" (alphabetical) or "createdAt" (chronological), ascending or descending; CreatedAt field added to Motivation entity and hydrated in constructor; SortBy/SortOrder added to MotivationFilterRequest defaulting to createdAt/asc; sorting applied in MotivationService.ListByGoalFilteredAsync after search filter; CreatedAt included in AddMotivationResponse; AppDbContext updated; MotivationsController.List updated with sortBy/sortOrder params) - OK
Day 60: Motivation tags (optional Tags on Motivation entity stored as comma-delimited TagsRaw string: SetTags(IEnumerable<string>?) sets/clears tags; constructor accepts optional tagsRaw for hydration; Tags included in AddMotivationRequest (for creation) and UpdateMotivationRequest (with ClearTags flag) and in all motivation responses; tag query param added to GET /goals/{goalId}/motivations for exact case-insensitive match filtering combined with existing search/sort filters; MotivationFilterRequest.Tag property with whitespace trimming) - OK
Day 61: Global user statistics endpoint (GET /users/stats: returns comprehensive stats for the authenticated user; IUserStatsService/UserStatsService dedicated service aggregates data from IGoalRepository, IStepRepository, and IMotivationRepository; response includes TotalGoals, PinnedGoals, ArchivedGoals, OverdueGoals, GoalsByStatus breakdown, TotalSteps, CompletedSteps, PendingSteps, OverdueSteps, and TotalMotivations; registered in DI; UserStatsTests.cs covers all stat fields including edge cases for overdue, pinned, archived, multi-goal aggregation, and data isolation between users) - OK
Day 62: Motivation favorites (IsFavorite flag on Motivation entity: Favorite()/Unfavorite() domain methods with guard throws; constructor accepts optional isFavorite for hydration; IsFavorite included in AddMotivationResponse; POST /goals/{goalId}/motivations/{motivationId}/favorite to mark as favorite (returns 409 if already favorite), DELETE /goals/{goalId}/motivations/{motivationId}/favorite to unmark (returns 409 if not favorite); onlyFavorites=true query param on GET /goals/{goalId}/motivations filters to only favorited motivations; FavoriteAsync/UnfavoriteAsync on IMotivationService/MotivationService; AppDbContext maps IsFavorite; MotivationFavoriteTests.cs covers domain, application service, and filter scenarios) - OK
Day 63: Motivation rating (optional Rating 1-5 on Motivation entity: Rate(int)/ClearRating() domain methods; constructor accepts optional rating for hydration; Rating included in AddMotivationResponse; PUT /goals/{goalId}/motivations/{motivationId}/rating to set rating (1-5), DELETE /goals/{goalId}/motivations/{motivationId}/rating to clear; minRating query param on GET /goals/{goalId}/motivations filters to motivations with Rating >= minRating (unrated motivations excluded); RateAsync/ClearRatingAsync on IMotivationService/MotivationService; RateMotivationRequest DTO; AppDbContext maps Rating; MotivationRatingTests.cs covers domain, service, and filter scenarios) - OK
Day 64: Motivation statistics per goal (GET /goals/{goalId}/motivations/stats: returns aggregated stats for motivations in a goal; MotivationStatsResponse DTO with TotalMotivations, TotalFavorites, RatedMotivations, AverageRating (nullable double), and TagBreakdown (Dictionary<string,int>); GetStatsAsync added to IMotivationService/MotivationService; stats endpoint added to MotivationsController; returns 400 if goal not found, 403 if unauthorized; MotivationStatsTests.cs covers empty goal, total/favorite/rating counts, average rating, tag breakdown, combined scenarios, and authorization) - OK
Day 65: Step statistics per goal (GET /goals/{goalId}/steps/stats: returns aggregated stats for steps in a goal; StepStatsResponse DTO with TotalSteps, CompletedSteps, PendingSteps, OverdueSteps, CompletionPercentage (double 0-100), PriorityBreakdown (Dictionary<string,int>), and TagBreakdown (Dictionary<string,int>); GetStatsAsync added to IStepService/StepService; stats endpoint added to StepsController; returns 400 if goal not found, 403 if unauthorized; StepStatsTests.cs covers empty goal, total/completed/pending counts, overdue (completed overdue steps not counted), completion percentage, priority breakdown, tag breakdown, combined scenario, and authorization) - OK
Day 66: Goal statistics per user (GET /goals/stats: returns aggregated stats for all goals of the authenticated user; GoalStatsResponse DTO with TotalGoals, GoalsByStatus (Dictionary<string,int>), GoalsByPriority (Dictionary<string,int>), ArchivedGoals, PinnedGoals, OverdueGoals, TagBreakdown (Dictionary<string,int>), and AvgCompletionDays (nullable double - average days from CreatedAt to CompletedAt for completed goals); GetStatsAsync added to IGoalService/GoalService; stats endpoint GET /goals/stats added to GoalsController; GoalStatsTests.cs covers empty user, total including archived, status breakdown, priority breakdown, archived/pinned counts, overdue (completed goals past deadline not counted), tag breakdown, null AvgCompletionDays when no completed goals, computed AvgCompletionDays for completed goals, data isolation between users, and combined full scenario) - OK
Day 67: User activity streak tracking (GET /users/streak: returns CurrentStreak, LongestStreak, and LastActivityDate for the authenticated user; streak is defined as consecutive calendar days (UTC) where at least one step was completed; CurrentStreak counts consecutive days ending today or yesterday (streak still active if completed yesterday); LongestStreak is the maximum consecutive day run across all activity; IStreakService/StreakService dedicated service aggregates step CompletedAt dates across all user goals; UserStreakResponse DTO; endpoint registered in UsersController; IStreakService registered in DI; UserStreakTests.cs covers no goals, no completed steps, single step today, single step yesterday, step 2+ days ago (streak broken), three consecutive days, gap breaking current streak while preserving longest, multiple completions same day counting as one day, longest streak larger than current, LastActivityDate correctness, data isolation between users, and steps across multiple goals combined) - OK
Day 68: Weekly activity report (GET /users/weekly-report: returns a 7-day activity summary for the authenticated user; WeeklyReportResponse DTO with WeekStart, WeekEnd, TotalStepsCompleted, TotalGoalsProgressed (distinct goals with at least one step completed this week), DailyBreakdown (IReadOnlyList<DailyActivityEntry> with Date and StepsCompleted, always 7 entries ordered asc), MostActiveDay (nullable DateTime - day with most completions), and AverageStepsPerDay (rounded to 2 decimal places); DailyActivityEntry record; IWeeklyReportService/WeeklyReportService aggregates step CompletedAt dates from all user goals filtering to the last 6 days + today (UTC); registered in DI; endpoint added to UsersController; WeeklyReportTests.cs covers no goals, only pending steps, steps within/outside the window, daily breakdown always 7 entries sorted ascending, multiple steps same day aggregated, most active day identification, average calculation, goals progressed distinct count, WeekStart/WeekEnd span, data isolation between users, and combined full scenario) - OK
Day 69: Monthly activity report (GET /users/monthly-report: returns a 30-day activity summary for the authenticated user; MonthlyReportResponse DTO with MonthStart, MonthEnd, TotalStepsCompleted, TotalGoalsProgressed (distinct goals with at least one step completed this month), WeeklyBreakdown (IReadOnlyList<WeeklyActivityEntry> with WeekNumber/WeekStart/WeekEnd/StepsCompleted, always 5 entries ordered by WeekNumber asc - 30 days divided into 5 buckets of 6 days each), MostActiveDay (nullable DateTime - single calendar day with most completions), MostProductiveWeek (nullable int 1-5 - week number with most steps, null if no steps), and AverageStepsPerDay (rounded to 2 decimal places); WeeklyActivityEntry record; IMonthlyReportService/MonthlyReportService aggregates step CompletedAt dates from all user goals filtering to last 29 days + today (UTC); registered in DI; endpoint added to UsersController; MonthlyReportTests.cs covers no goals, only pending steps, steps within/outside the 30-day window (29 days ago included, 30 days ago excluded), weekly breakdown always 5 entries sorted ascending, steps assigned to correct week buckets, most active day identification, most productive week selection, average steps per day, goals progressed distinct count, MonthStart/MonthEnd 30-day span, data isolation between users, and combined full scenario) - OK
Day 70: Yearly activity report (GET /users/yearly-report: returns a 365-day activity summary for the authenticated user; YearlyReportResponse DTO with YearStart, YearEnd, TotalStepsCompleted, TotalGoalsProgressed (distinct goals with at least one step completed this year), MonthlyBreakdown (IReadOnlyList<MonthlyActivityEntry> with MonthNumber/MonthStart/MonthEnd/StepsCompleted, always 12 entries ordered by MonthNumber asc - 365 days divided into 12 buckets of 30 days each, with bucket 12 absorbing the remaining 35 days), MostActiveDay (nullable DateTime - single calendar day with most completions), MostProductiveMonth (nullable int 1-12 - month number with most steps, null if no steps), and AverageStepsPerDay (rounded to 2 decimal places); MonthlyActivityEntry record; IYearlyReportService/YearlyReportService aggregates step CompletedAt dates from all user goals filtering to last 364 days + today (UTC); registered in DI; endpoint added to UsersController; YearlyReportTests.cs covers no goals, only pending steps, steps within/outside the 365-day window (364 days ago included, 365 days ago excluded), monthly breakdown always 12 entries sorted ascending, steps assigned to correct month buckets (first/last), most active day identification, most productive month selection, average steps per day, goals progressed distinct count, YearStart/YearEnd 365-day span, data isolation between users, and combined full scenario) - OK
Day 71: Activity heatmap data (GET /users/activity-heatmap: returns day-by-day step completion counts for the last 365 days for the authenticated user; ActivityHeatmapResponse DTO with WindowStart, WindowEnd, TotalStepsCompleted, ActiveDays (number of distinct days with at least one completion), and Entries (IReadOnlyList<HeatmapEntry> with Date and Count, always 365 entries ordered ascending - zero-filled for days with no completions); HeatmapEntry record; IActivityHeatmapService/ActivityHeatmapService aggregates step CompletedAt dates from all user goals filtering to last 364 days + today (UTC); registered in DI; endpoint added to UsersController; ActivityHeatmapTests.cs covers no goals, only pending steps, steps within/outside the 365-day window (364 days ago included, 365 days ago excluded), always 365 entries sorted ascending, first entry is 364 days ago/last entry is today, step today counted in last entry, step 364 days ago counted in first entry, multiple steps same day aggregated, active days counts distinct days, WindowStart/WindowEnd 365-day span, data isolation between users, and combined full scenario) - OK
Day 72: Recent activity feed (GET /users/recent-activity?page=1&pageSize=20: returns a paginated chronological feed of recently completed steps across all goals for the authenticated user, ordered by CompletedAt desc; RecentActivityEntry record with StepId, StepTitle, GoalId, GoalTitle, CompletedAt; RecentActivityResponse with TotalCount, Page, PageSize, Entries; IRecentActivityService/RecentActivityService aggregates steps from all user goals, filters to only completed ones, sorts by CompletedAt desc, applies pagination; pageSize clamped 1-100, page clamped to >= 1; registered in DI; endpoint added to UsersController; RecentActivityTests.cs covers no goals, only pending steps, single completed step entry fields, ordering desc by CompletedAt, steps across multiple goals ordered correctly, pagination TotalCount includes all, second page returns correct entries, page beyond data returns empty, pageSize 0 clamped to 1, pageSize over 100 clamped to 100, negative page clamped to 1, data isolation between users, mixed completed/pending only completed returned, GoalTitle correctly mapped per entry) - OK
Day 73: Daily completion summary (GET /users/daily-summary?date=yyyy-MM-dd: returns a grouped summary of steps completed on a specific date for the authenticated user; date param is optional and defaults to today UTC; returns 400 for invalid date format; DailySummaryResponse DTO with Date, TotalStepsCompleted, GoalsProgressed, and Entries (IReadOnlyList<DailySummaryGoalEntry> ordered by GoalTitle asc); DailySummaryGoalEntry has GoalId, GoalTitle, and Steps (IReadOnlyList<DailySummaryStepEntry> ordered by CompletedAt asc); DailySummaryStepEntry has StepId, StepTitle, CompletedAt; goals with no steps completed on the date are excluded; IDailySummaryService/DailySummaryService aggregates steps from all user goals filtering by date using UTC date comparison; registered in DI; endpoint added to UsersController; DailySummaryTests.cs covers no goals, only pending steps, correct date returned, single step today, step yesterday excluded for today query, step yesterday included when querying yesterday, multiple steps same goal grouped under one entry, steps ordered by CompletedAt asc within entry, multiple goals each grouped separately, entries ordered by GoalTitle asc, mixed completed/pending only completed included, data isolation between users, GoalsProgressed counts distinct goals, goal with no steps on date excluded from entries, and combined full scenario) - OK

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
