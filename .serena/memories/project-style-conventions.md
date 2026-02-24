# Ghost Project - Style and Conventions

## Tech Stack
- **Language**: C# (.NET 10 / net10.0)
- **Test Framework**: xUnit v3
- **Project Type**: Layered monorepo
- **Package Management**: Central Package Management (CPM)

## Code Style Rules

### Formatting
- Use **2 spaces** for indentation
- Use semicolons
- Use double quotes for strings
- Use trailing commas in multi-line objects and arrays

### Naming Conventions
- **PascalCase** for types, methods, properties
- **camelCase** for local variables
- **PascalCase** for constants

### C# Language Features
- Prefer `var` when type is obvious from right-hand side
- Use `ConfigureAwait(false)` in library code
- Prefer async/await over Task.Result/Task.Wait()
- Use nullable reference types enabled
- Prefer records for DTOs and immutable data
- Use pattern matching where appropriate

### Async Patterns
```csharp
// Correct - use ConfigureAwait in library code
await SomeAsyncMethod().ConfigureAwait(false);

// Correct - prefer async/await
public async Task MyMethod()
{
    await Task.Delay(100);
}
```

### Dependency Injection
```csharp
// Registration
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddScoped<IScopedService, ScopedService>();

// Resolution (constructor injection)
public class MyController
{
    private readonly IMyService _service;
    public MyController(IMyService service)
    {
        _service = service;
    }
}
```

## Architecture Principles
- Organize code by **feature**, not by file type
- Keep related files close together
- Use dependency injection for better testability
- Implement proper error handling
- Follow single responsibility principle
- Prefer composition over inheritance

## Project Structure
This is a layered monorepo:
- **Layer 0 (Kernel)**: `src/Kernel/Ghost/` - Core abstractions and utilities
- **Layer 1 (Contracts)**: `src/Contracts/` - Interface definitions shared across modules
- **Layer 2 (Engine)**: `src/Engine/` - Scraping engine implementation
- **Layer 3 (Plugins)**: `src/Plugins/` - Platform-specific implementations
- **Layer 4 (Apps)**: `src/Apps/` - Host applications (Worker, WebApi)
- **Layer 5 (Cloud)**: `src/Cloud/` - Cloud infrastructure and delivery

Tests mirror the src structure in `tests/` directory.

## Anti-Patterns to Avoid

### Task.Delay Anti-Pattern
⚠️ **DO NOT use Task.Delay for reliability:**
- ❌ `await Task.Delay(1000)` to "wait for things to settle"
- ❌ Retry loops with fixed delays
- ❌ "Warmup delays" before operations

✅ **Correct patterns:**
- Budgets: Use `CancellationTokenSource.CancelAfter(budgetMs)` + fail-fast
- Throttles: Use `SemaphoreSlim` or token bucket rate limiters
- Retries: Use Polly with jittered exponential backoff
- Synchronization: Use `TaskCompletionSource` or proper async signaling

## Testing Guidelines
- Use xUnit v3 for all tests
- Name test methods descriptively: `MethodName_Scenario_ExpectedResult`
- Use `IAsyncLifetime` for async test setup/teardown
- Prefer `ConfigureAwait(false)` in library code under test
- Use `TaskCancelledException` assertions for timeout tests
- Mock external dependencies, test real behavior for core logic
