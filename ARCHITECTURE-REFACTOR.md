# Architecture Refactor Plan

This document describes the architectural changes needed to move the ExpenseTracker .NET backend from its current state (controllers with direct `AppDbContext` access) to a clean onion architecture with proper separation of concerns.

## Current State

The codebase has three projects:

- `ExpenseTracker.Api` - ASP.NET Core Web API (controllers, models/DTOs, services, middleware)
- `ExpenseTracker.Core` - Domain layer (entities, enums, interfaces, records)
- `ExpenseTracker.Infrastructure` - Data access (EF Core `AppDbContext`, configurations, external integrations)

The project structure looks like onion architecture but **the dependency flow is wrong**. Controllers in `ExpenseTracker.Api` directly inject and use `AppDbContext` for all CRUD operations, query building, validation, and business logic. There is no repository abstraction and only a partial service layer.

## Problems

### 1. Controllers have direct database access

Every controller injects `AppDbContext` and performs raw EF Core queries inline. Example from `TransactionsController`:

```csharp
public sealed class TransactionsController(AppDbContext dbContext, ...) : ApiControllerBase
{
  // Builds LINQ queries directly
  // Calls dbContext.Transactions.Add(...)
  // Calls dbContext.SaveChangesAsync(...)
  // Calls dbContext.Categories.AnyAsync(...)
}
```

Affected controllers: `TransactionsController`, `CategoriesController`, `AnalyticsController`, `InvestmentsController`, `InvestmentProvidersController`, `MerchantRulesController`, `SettingsController`, `AuthController`, `AdminController`, `LlmLogsController`, `LlmProvidersController`, `RawMessagesController`, `WebhookController`, `DiagnosticController`.

### 2. No repository layer

There are zero repository interfaces or implementations. All data access is scattered across controllers and a few services.

### 3. Business logic lives in controllers

Controllers handle: authentication checks, input validation, entity creation, query building with complex filters, audit logging, merchant rule upserts, category reassignment on delete, bulk operations, and CSV export. None of this belongs in a controller.

### 4. Services also access DbContext directly

Services like `InvestmentAnalyticsService`, `PortfolioHistoryService`, and `NarrativeService` inject `AppDbContext` directly instead of going through repositories. DTOs are defined in the same file as the service (e.g., `InvestmentAnalyticsService.cs` contains 10+ record types at the bottom).

### 5. No formal validation

Validation is manual and inline in each controller action: `if (string.IsNullOrWhiteSpace(...)) return BadRequest(...)`. No FluentValidation, no DataAnnotations.

### 6. No EF migrations

The app uses `EnsureCreatedAsync()` in `Program.cs` instead of EF migrations. Schema changes cannot be versioned or rolled back.

### 7. DTOs defined in inconsistent locations

Some DTOs are in `ExpenseTracker.Api/Models/`, some are inline in service files (e.g., `InvestmentAnalyticsService.cs`), some are in controller files (e.g., `InvestmentsController.cs` has `ManualAccountDto`, `CreateManualAccountRequest`, etc. at the bottom).

## Target Architecture (Onion Model)

```
ExpenseTracker.Core (innermost - no dependencies)
  Entities/         - domain entities (already exists)
  Enums/            - enums (already exists)
  Records/          - value objects / records (already exists)
  Interfaces/       - repository interfaces, service interfaces
  DTOs/             - shared DTOs used across layers

ExpenseTracker.Application (new project - depends on Core only)
  Services/         - business logic / use cases
  Validators/       - FluentValidation validators
  Mapping/          - entity-to-DTO mapping extensions

ExpenseTracker.Infrastructure (depends on Core only)
  Data/
    AppDbContext.cs
    Configurations/
    Repositories/   - repository implementations
    Migrations/     - EF migrations
  Llm/              - LLM provider integrations (already exists)
  Investments/      - investment provider integrations (already exists)

ExpenseTracker.Api (outermost - depends on Application, Infrastructure)
  Controllers/      - thin controllers that delegate to services
  Middleware/
  Program.cs        - DI composition root
```

## Step-by-Step Changes

### Phase 1: Introduce Repository Interfaces (Core)

Add repository interfaces to `ExpenseTracker.Core/Interfaces/`:

- `ITransactionRepository` - CRUD, filtered queries, bulk operations
- `ICategoryRepository` - CRUD, hierarchy queries, sort-order management
- `IMerchantRuleRepository` - CRUD, upsert by normalized merchant
- `IInvestmentAccountRepository` - CRUD, balance updates
- `IInvestmentProviderRepository` - CRUD, sync status
- `ISettingRepository` - get/set by key
- `IUserRepository` - auth-related queries
- `IAuditLogRepository` - write audit entries
- `ILlmCallLogRepository` - logging LLM calls
- `IRawMessageRepository` - SMS/message storage
- `ISummaryRepository` - narrative summaries
- `IPortfolioHistoryRepository` - historical snapshots
- `IHoldingRepository` - investment holdings

Each interface should expose domain-level operations, not raw `IQueryable`. For example:

```csharp
public interface ITransactionRepository
{
  Task<Transaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
  Task<(List<Transaction> Items, int TotalCount)> GetPagedAsync(Guid userId, TransactionFilter filter, int page, int pageSize, CancellationToken ct);
  Task<List<Transaction>> GetAllAsync(Guid userId, TransactionFilter filter, CancellationToken ct);
  Task AddAsync(Transaction transaction, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}
```

### Phase 2: Implement Repositories (Infrastructure)

Add implementations in `ExpenseTracker.Infrastructure/Data/Repositories/`:

- Each repository takes `AppDbContext` via constructor injection
- Repositories encapsulate all LINQ/EF queries
- No `IQueryable` leaks out of repositories — return materialized results
- The `BuildQuery` methods currently in controllers move into repositories

### Phase 3: Create Application Layer

Add a new project `ExpenseTracker.Application`:

- Move business logic out of controllers into service classes
- `TransactionService` - create, update, delete, recategorize, bulk-recategorize, export
- `CategoryService` - create, update, delete (with reassignment logic)
- `AnalyticsService` - dashboard, monthly, yearly, insights (move computation logic from `AnalyticsController`)
- `InvestmentService` - wraps `InvestmentAnalyticsService` + manual account CRUD + sync orchestration
- `AuthService` - login, register, refresh token logic
- `MerchantRuleService` - CRUD + upsert logic
- `SettingsService` - get/update settings

Services depend only on repository interfaces (from Core), not on `AppDbContext`.

### Phase 4: Add FluentValidation

Add FluentValidation to the Application layer:

- `CreateTransactionRequestValidator`
- `UpdateTransactionRequestValidator`
- `CreateCategoryRequestValidator`
- `UpdateCategoryRequestValidator`
- `RegisterRequestValidator`
- `LoginRequestValidator`
- etc.

Register validators in DI and use ASP.NET Core's automatic validation pipeline.

### Phase 5: Consolidate DTOs

Move all DTOs to a consistent location:

- Request/response DTOs stay in `ExpenseTracker.Api/Models/` (they are API-layer concerns)
- Shared domain DTOs (used by services) move to `ExpenseTracker.Core/DTOs/` or `ExpenseTracker.Application/DTOs/`
- Remove DTOs from service files and controller files

### Phase 6: Make Controllers Thin

Refactor every controller to:

1. Remove `AppDbContext` from constructor injection
2. Inject the appropriate service interface instead
3. Controller actions should only: parse request, call service, map result, return response
4. Move all `try/catch` blocks to a global exception-handling middleware or filter
5. Move audit logging into services or a cross-cutting concern

Example target controller:

```csharp
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(ITransactionService transactionService) : ApiControllerBase
{
  [HttpGet]
  public async Task<ActionResult<PagedResult<TransactionDto>>> GetAsync(
    [FromQuery] TransactionFilterParams filter, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }

    var result = await transactionService.GetPagedAsync(userId.Value, filter, ct);
    return Ok(result);
  }
}
```

### Phase 7: Add EF Migrations

- Remove `EnsureCreatedAsync()` from `Program.cs`
- Generate initial migration from current schema: `dotnet ef migrations add InitialCreate`
- Use `Database.MigrateAsync()` at startup (or run migrations separately)
- All future schema changes go through migrations

### Phase 8: Add Global Error Handling

Replace per-action `try/catch` blocks with:

- A global exception-handling middleware or `IExceptionHandler` (ASP.NET Core 8+)
- Custom exception types for domain errors (e.g., `NotFoundException`, `ValidationException`)
- Services throw domain exceptions, middleware maps them to HTTP responses

### Phase 9: Move Background Services

Background services (`SmsProcessingBackgroundService`, `DailyNarrativeWorker`, `InvestmentSyncWorker`, `NarrativeRegenerationWorker`) currently live in `ExpenseTracker.Api`. They should depend on Application-layer services, not on `AppDbContext` directly. Consider moving their orchestration logic to the Application layer.

## Dependency Rules

- `Core` depends on nothing
- `Application` depends only on `Core`
- `Infrastructure` depends only on `Core`
- `Api` depends on `Application` and `Infrastructure` (for DI wiring only)
- **Never** reference `AppDbContext` from `Api` or `Application`
- **Never** reference `Infrastructure` types from `Application`

## What NOT to Change

- Entity definitions in `Core/Entities/` are fine as-is
- EF configurations in `Infrastructure/Data/Configurations/` are fine
- The LLM provider pattern (`ILlmCategorizationProvider` interface + implementations) is already well-structured
- The investment provider pattern (`IInvestmentDataProvider`) is already well-structured
- JWT authentication setup is fine
- Rate limiting and CORS configuration is fine
- Primary constructors, sealed classes, records for DTOs are all good modern C# patterns to keep
