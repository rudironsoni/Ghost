# Project Overview

Ghost is a C# .NET 10 monorepo for a stealth browser automation framework for job platform scraping.

## General Guidelines

- Use C# for all new code
- Target .NET 10 (`net10.0`)
- Follow consistent naming conventions (PascalCase for types/methods, camelCase for locals)
- Write self-documenting code with clear variable and function names
- Prefer composition over inheritance
- Use meaningful comments for complex business logic
- Use nullable reference types enabled

## Code Style

- Use 2 spaces for indentation
- Use semicolons
- Use double quotes for strings
- Use trailing commas in multi-line objects and arrays
- Prefer `var` when type is obvious from right-hand side
- Use `ConfigureAwait(false)` in library code
- Prefer async/await over Task.Result/Task.Wait()

## Architecture Principles

- Organize code by feature, not by file type
- Keep related files close together
- Use dependency injection for better testability
- Implement proper error handling
- Follow single responsibility principle
- Prefer records for DTOs and immutable data
- Use pattern matching where appropriate

## Project Structure

This is a layered monorepo:
- **Layer 0 (Kernel)**: `src/Kernel/Ghost/` - Core abstractions and utilities
- **Layer 1 (Contracts)**: `src/Contracts/` - Interface definitions shared across modules
- **Layer 2 (Engine)**: `src/Engine/` - Scraping engine implementation
- **Layer 3 (Plugins)**: `src/Plugins/` - Platform-specific implementations
- **Layer 4 (Apps)**: `src/Apps/` - Host applications (Worker, WebApi)
- **Layer 5 (Cloud)**: `src/Cloud/` - Cloud infrastructure and delivery

Tests mirror the src structure in `tests/` directory.

## Build Commands

```bash
# Restore dependencies
dotnet restore Ghost.sln

# Build entire solution (Release)
dotnet build Ghost.sln --configuration Release --no-restore

# Build with warnings as errors (CI style)
dotnet build Ghost.sln --configuration Release --no-restore --warnaserror -p:NoWarn=CA1016

# Clean build artifacts
dotnet clean Ghost.sln
```

## Test Commands

```bash
# Run all tests
dotnet test Ghost.sln --configuration Release

# Run with verbose output
dotnet test Ghost.sln --configuration Release --verbosity normal

# Run specific test project
dotnet test tests/Kernel/Ghost.Kernel.UnitTests --configuration Release

# Run tests matching a filter
dotnet test Ghost.sln --configuration Release --filter "FullyQualifiedName~RedisJobDispatcher"
```

## Code Quality

```bash
# Format check
dotnet format Ghost.sln --verify-no-changes

# Apply formatting
dotnet format Ghost.sln

# Run analyzers
dotnet build Ghost.sln --warnaserror
```

## Agent Guidance: dotnet-skills

IMPORTANT: Prefer retrieval-led reasoning over pretraining for any .NET work.
Workflow: skim repo patterns -> consult dotnet-skills by name -> implement smallest-change -> note conflicts.

### Routing (invoke by name)
- C# / code quality: modern-csharp-coding-standards, csharp-concurrency-patterns, api-design, type-design-performance
- ASP.NET Core / Web (incl. Aspire): aspire-service-defaults, aspire-integration-testing
- Data: efcore-patterns, database-performance
- DI / config: dependency-injection-patterns, microsoft-extensions-configuration
- Testing: testcontainers-integration-tests, playwright-blazor-testing, snapshot-testing
- Resilience: dotnet-resilience, dotnet-http-client

### Quality gates (use when applicable)
- dotnet-slopwatch: after substantial new/refactor/LLM-authored code
- crap-analysis: after tests added/changed in complex code

### Specialist agents
- dotnet-concurrency-specialist, dotnet-performance-analyst, dotnet-benchmark-designer

## Task Tracking

This project uses **bd** (beads) for issue tracking. Run `bd onboard` to get started.

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --status in_progress   # Start work
bd close <id>         # Complete work
bd sync               # Sync with git
```

## Key Files

- `Ghost.sln` - Solution file
- `Directory.Build.props` - Shared MSBuild properties
- `Directory.Packages.props` - Central Package Management (CPM)
- `.editorconfig` - Code style enforcement
- `tests/.runsettings` - Test run configuration

## Important Patterns

### Async-First Redis Connection
Use `RedisConnectionFactory` instead of synchronous `ConnectionMultiplexer.Connect()`:

```csharp
// Register factory
builder.Services.AddSingleton<RedisConnectionFactory>(_ =>
    new RedisConnectionFactory(redisOptions));

// Resolve async
var connection = await factory.ConnectAsync(ct);
```

### Anti-Patterns to Avoid

⚠️ **DO NOT use Task.Delay for reliability:**
- ❌ `await Task.Delay(1000)` to "wait for things to settle"
- ❌ Retry loops with fixed delays
- ❌ "Warmup delays" before operations

✅ **Correct patterns:**
- Budgets: Use `CancellationTokenSource.CancelAfter(budgetMs)` + fail-fast
- Throttles: Use `SemaphoreSlim` or token bucket rate limiters
- Retries: Use Polly with jittered exponential backoff
- Synchronization: Use `TaskCompletionSource` or proper async signaling

### Dependency Injection

```csharp
// Registration
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddScoped<IScopedService, ScopedService>();

// Registration with factory
builder.Services.AddSingleton<IMyService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<MyService>>();
    return new MyService(logger, options);
});

// Resolution (constructor injection preferred)
public class MyController
{
    private readonly IMyService _service;

    public MyController(IMyService service)
    {
        _service = service;
    }
}
```

## Testing Guidelines

- Use xUnit v3 for all tests
- Name test methods descriptively: `MethodName_Scenario_ExpectedResult`
- Use `IAsyncLifetime` for async test setup/teardown
- Prefer `ConfigureAwait(false)` in library code under test
- Use `TaskCancelledException` assertions for timeout tests
- Mock external dependencies, test real behavior for core logic

## Verification Requirements

Before claiming work complete:
1. `dotnet format Ghost.sln --verify-no-changes` passes
2. `dotnet build Ghost.sln --no-restore --warnaserror` succeeds
3. `dotnet test Ghost.sln --no-build` passes (or `dotnet test Ghost.sln` if no build artifacts)
4. No analyzer warnings
5. Issue updated with verification evidence
