# Coding Style Guide (for LLMs)

This document defines the coding style for the ExpenseTracker .NET backend. Follow these rules exactly when writing or modifying C# code.

## Indentation and Whitespace

- Use **2 spaces** for indentation. Never use tabs. Never use 4 spaces.
- No trailing whitespace on any line.
- One blank line between methods. No multiple consecutive blank lines.
- No blank lines after opening braces or before closing braces.
- End every file with a single newline.

```csharp
// Correct: 2-space indentation
public async Task<ActionResult> GetAsync(CancellationToken ct)
{
  var userId = GetCurrentUserId();
  if (userId is null)
  {
    return Unauthorized();
  }

  var result = await _service.GetAsync(userId.Value, ct);
  return Ok(result);
}
```

## Braces

- **Always** use braces for `if`, `else`, `for`, `foreach`, `while`, `do`, `using`, `lock`, and `try/catch/finally` blocks, even for single-line bodies.
- Opening brace goes on a **new line** (Allman style), aligned with the statement.
- The body inside braces is indented by 2 spaces.
- `else` and `catch`/`finally` go on their own line, not on the same line as the closing brace.

```csharp
// Correct
if (userId is null)
{
  return Unauthorized();
}
else
{
  return Ok();
}

// Wrong - no braces
if (userId is null)
  return Unauthorized();

// Wrong - same-line brace (K&R style)
if (userId is null) {
  return Unauthorized();
}

// Wrong - single-line if
if (userId is null) return Unauthorized();
```

## Naming Conventions (Microsoft Standard)

- **PascalCase** for: classes, structs, records, interfaces, enums, enum values, methods, properties, events, constants, namespaces, type parameters.
- **camelCase** for: local variables, method parameters, lambda parameters.
- **_camelCase** (leading underscore) for: private fields.
- **IPascalCase** (leading I) for: interfaces.
- **TPascalCase** (leading T) for: type parameters (e.g., `TEntity`, `TResult`).
- **Async** suffix for all async methods that return `Task` or `ValueTask`.
- No abbreviations except widely known ones (`Id`, `Dto`, `Url`, `Http`, `Jwt`, `Llm`).
- Boolean properties/variables should read as questions: `IsActive`, `HasValue`, `CanDelete`.

```csharp
public sealed class TransactionService : ITransactionService
{
  private readonly ITransactionRepository _transactionRepository;

  public TransactionService(ITransactionRepository transactionRepository)
  {
    _transactionRepository = transactionRepository;
  }

  public async Task<TransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
  {
    var transaction = await _transactionRepository.GetByIdAsync(id, userId, ct);
    return transaction?.ToDto();
  }
}
```

## Formatting

- One statement per line.
- One declaration per line.
- Maximum line length: 120 characters. Break long lines at logical points.
- When breaking method calls across lines, indent continuation lines by 2 spaces.
- When breaking method parameters across lines, put each parameter on its own line.
- Use parentheses to clarify operator precedence in complex expressions.

```csharp
// Long method signature - break parameters
public async Task<ActionResult<PagedResult<TransactionDto>>> GetAsync(
  [FromQuery] TransactionFilterParams filter,
  CancellationToken ct)
{
  var result = await _transactionService.GetPagedAsync(
    userId.Value,
    filter,
    ct);
  return Ok(result);
}
```

## Type Usage

- Use language keywords over framework types: `string` not `String`, `int` not `Int32`, `bool` not `Boolean`.
- Use `var` when the type is obvious from the right side of the assignment. Use explicit types when the type is not obvious.
- Use `nameof()` instead of hardcoded strings for member names.
- Use string interpolation `$"..."` over `string.Format` or concatenation.
- Use `is null` / `is not null` over `== null` / `!= null`.

```csharp
// Correct
var user = await _userRepository.GetByIdAsync(id, ct);
List<TransactionDto> results = GetComplexResults();
var name = nameof(Transaction);
var message = $"Failed to fetch {name} with id {id}.";
if (user is null)
{
  return NotFound();
}
```

## Null Handling

- Use nullable reference types. The project has nullable enabled.
- Use `?.` (null-conditional) and `??` (null-coalescing) operators.
- Never use `!` (null-forgiving) operator except on navigation properties in entities (e.g., `public User User { get; set; } = null!;`).

## LINQ

- Use method syntax (`Where`, `Select`, `OrderBy`) not query syntax (`from ... where ... select`).
- When chaining multiple LINQ methods, put each method on its own line.

```csharp
var results = transactions
  .Where(x => x.UserId == userId && !x.IsDeleted)
  .OrderByDescending(x => x.TransactionDate)
  .Select(x => x.ToDto())
  .ToList();
```

## Async/Await

- All I/O-bound methods must be async.
- Always pass `CancellationToken` as the last parameter and name it `ct`.
- Always forward `ct` to downstream async calls.
- Never use `.Result` or `.Wait()` on tasks.
- Use `ValueTask` only when the common path is synchronous.

## Classes and Types

- Use `sealed` on classes that are not designed for inheritance.
- Use `record` for DTOs and immutable data types.
- Use `sealed record` for request/response models.
- Use primary constructors for classes with simple DI injection.
- Use file-scoped namespaces: `namespace ExpenseTracker.Api.Controllers;`
- One type per file, except for small related records (request/response pairs for the same endpoint).
- DTOs and request models for a domain area can be grouped in one file (e.g., `TransactionModels.cs`).

```csharp
namespace ExpenseTracker.Core.Interfaces;

public interface ITransactionRepository
{
  Task<Transaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
}
```

```csharp
namespace ExpenseTracker.Api.Models;

public sealed record CreateTransactionRequest(
  decimal Amount,
  Direction Direction,
  TransactionType TransactionType,
  DateTime TransactionDate,
  Guid CategoryId,
  string? MerchantRaw,
  string? Notes);
```

## Access Modifiers

- Always specify access modifiers explicitly. Never rely on defaults.
- Order: `public`, `internal`, `protected`, `private`.
- Within a class, order members: constants, fields, constructors, properties, methods.
- `static` members before instance members within each group.

## Comments

- Default to writing no comments.
- Only add a comment when the why is non-obvious: a hidden constraint, a subtle invariant, a workaround for a specific bug.
- Never write `// TODO`, `// HACK`, `// FIXME` comments without an associated issue number.
- Never write XML doc comments (`///`) on internal types. Only on public API surface when building a library.

## Error Handling

- Use global exception handling middleware. Do not wrap every controller action in try/catch.
- Throw specific exceptions from services: `NotFoundException`, `ValidationException`, `ConflictException`.
- Never catch `Exception` and return a generic 500 unless in global middleware.
- Never swallow exceptions silently.

## Dependency Injection

- Inject interfaces, not concrete types.
- Controllers receive service interfaces, never `AppDbContext`.
- Services receive repository interfaces, never `AppDbContext`.
- Only repository implementations and `Program.cs` should reference `AppDbContext`.
- Register scoped services for request-scoped operations.
- Register singleton services only for truly stateless/thread-safe components.

## File Organization

```
// Using directives (sorted: System first, then project namespaces)
using System.Text.Json;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

// File-scoped namespace
namespace ExpenseTracker.Api.Controllers;

// Type definition
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(ITransactionService transactionService) : ApiControllerBase
{
  // Members
}
```

## Using Directives

- Sort `System.*` namespaces first, then third-party, then project namespaces.
- Remove unused usings.
- Use global usings in `GlobalUsings.cs` for namespaces used in nearly every file.

## Tests

- Name test methods: `MethodName_Scenario_ExpectedResult`.
- Use `Arrange`, `Act`, `Assert` sections (no comments needed, just blank line separation).
- One assertion per test when practical.
- Use `FluentAssertions` for assertions.

## Entity Framework

- Use `AsNoTracking()` for read-only queries.
- Use `Include()` / `ThenInclude()` explicitly. No lazy loading.
- Keep EF configurations in separate `IEntityTypeConfiguration<T>` classes.
- Use migrations, not `EnsureCreated()`.

## Controller Conventions

- Route prefix: `api/{resource}` in kebab-case.
- Use `[FromQuery]` for GET parameters, `[FromBody]` for POST/PUT/PATCH bodies.
- Return `ActionResult<T>` with typed responses.
- Use `CancellationToken ct` on every async action.
- Controllers are thin: validate user identity, call service, return result.
