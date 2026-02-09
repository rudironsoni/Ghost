# Agent Instructions

This is a C# .NET 9 monorepo for Ghost, a stealth browser automation framework for job platform scraping.

## Project Structure

This is a 5-layer monorepo:
- **Layer 0 (Core)**: `src/Core/Ghost/` - Stealth browser automation kernel
- **Layer 1 (Contracts)**: `src/Contracts/` - Interface definitions shared across modules
- **Layer 2 (Platforms)**: `src/Platforms/` - Platform-specific implementations (LinkedIn, Indeed, Glassdoor, etc.)
- **Layer 3 (Hosting)**: `src/Hosting/` - ASP.NET Core Web API hosting infrastructure
- **Layer 4 (SDK)**: `src/Sdk/` - Meta-package for consumers

Tests mirror the src structure in `tests/` directory.

## Task Tracking

This project uses **bd** (beads) for issue tracking. Run `bd onboard` to get started.

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --status in_progress  # Claim work
bd close <id>         # Complete work
bd sync               # Sync with git
```

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
# Run all tests (USE --maxcpucount:1 and -nodereuse:false to prevent hangs)
dotnet test Ghost.sln --configuration Release --maxcpucount:1 -nodereuse:false

# Run all tests with verbosity
dotnet test Ghost.sln --configuration Release --verbosity normal --maxcpucount:1 -nodereuse:false

# Run all tests excluding E2E (CI style)
dotnet test Ghost.sln --configuration Release --filter "FullyQualifiedName!~E2E" --maxcpucount:1 -nodereuse:false

# Run tests for a specific project
dotnet test tests/Core/Ghost.Tests/Ghost.Tests.csproj --configuration Release -nodereuse:false

# Run a single test by fully qualified name
dotnet test tests/Core/Ghost.Tests --filter "FullyQualifiedName~GhostKernelTests" -nodereuse:false

# Run a specific test method
dotnet test tests/Core/Ghost.Tests --filter "FullyQualifiedName=Ghost.Core.Tests.GhostKernelTests.NewSessionAsyncUsesOptionsToCreateContext" -nodereuse:false

# Run tests with code coverage
dotnet test Ghost.sln --collect:"XPlat Code Coverage" --maxcpucount:1 -nodereuse:false
```

**IMPORTANT**: Always use `--maxcpucount:1` and `-nodereuse:false` when running the full test suite to prevent MSBuild child node crashes that cause test hangs. This forces sequential test execution and disables MSBuild node reuse.

**Alternative**: You can also set the environment variable globally to disable node reuse:
```bash
export MSBUILDDISABLENODEREUSE=1
```

## Lint/Format Commands

```bash
# Check code formatting (must match .editorconfig)
dotnet format Ghost.sln --verify-no-changes --verbosity diagnostic

# Apply code formatting fixes
dotnet format Ghost.sln

# Install dotnet-format tool if needed
dotnet tool install -g dotnet-format || dotnet tool update -g dotnet-format
```

## Code Style Guidelines

### General
- **Target Framework**: .NET 9.0 (`net9.0`)
- **Language Version**: `preview`
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled
- **Warnings as Errors**: All warnings must be fixed (no suppressions allowed)

### Formatting (from .editorconfig)
- Indent: 4 spaces (2 for JSON/YAML/Project files)
- Line endings: LF (`\n`)
- Charset: UTF-8
- Trim trailing whitespace: Yes
- Insert final newline: Yes

### Naming Conventions
- **Interfaces**: PascalCase with `I` prefix (e.g., `IJobScraper`)
- **Classes/Structs**: PascalCase (e.g., `GhostKernel`)
- **Methods**: PascalCase (e.g., `NewSessionAsync`)
- **Async methods**: Must end with `Async` suffix
- **Properties**: PascalCase (e.g., `MaxConcurrentSessions`)
- **Fields**: `_camelCase` with underscore prefix (private), or PascalCase (public)
- **Variables**: camelCase (e.g., `maxSessions`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE

### Namespace & Using Style
- Use **file-scoped namespaces**: `namespace Ghost.Core;` (not block-scoped)
- Usings go **outside** the namespace declaration
- Sort system directives first
- Do not separate import directive groups

```csharp
using System;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Playwright;

namespace Ghost.Core;
```

### Types & Nullability
- Use `var` when type is apparent: `var list = new List<string>();`
- Always use nullable annotations: `string? maybeNull`
- Use `ArgumentNullException.ThrowIfNull(param)` for null checks
- Prefer pattern matching: `if (obj is Type t)` instead of `as` + null check

### Error Handling
- Use exceptions for exceptional cases, not control flow
- Prefer specific exceptions over generic `Exception`
- Use `ArgumentNullException`, `InvalidOperationException`, `NotSupportedException` appropriately
- Document exceptions with `<exception>` XML docs when public API

### Async/Await
- Always use `async`/`await` - no blocking calls (`.Result`, `.Wait()`)
- Async methods must return `Task` or `Task<T>`
- Name async methods with `Async` suffix
- Use `CancellationToken` parameters and forward them

### Class Design
- Prefer `record` for immutable DTOs
- Use primary constructors where appropriate (C# 12+)
- Implement `IAsyncDisposable` for classes holding async resources
- Keep classes focused on single responsibility

### Testing
- Use **xUnit** for unit tests
- Use **NSubstitute** for mocking
- Use **FluentAssertions** for assertions
- Test class naming: `{ClassUnderTest}Tests`
- Test method naming: `{MethodName}_{Scenario}_{ExpectedResult}`

```csharp
public class GhostKernelTests
{
    [Fact]
    public async Task NewSessionAsync_WithValidOptions_CreatesContext()
    {
        // Arrange
        var playwright = Substitute.For<IPlaywright>();
        
        // Act
        var result = await kernel.NewSessionAsync(options);
        
        // Assert
        result.Should().NotBeNull();
    }
}
```

## Landing the Plane (Session Completion)

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed):
   ```bash
   dotnet build Ghost.sln --configuration Release --no-restore --warnaserror
   dotnet test Ghost.sln --configuration Release --filter "FullyQualifiedName!~E2E" -nodereuse:false
   dotnet format Ghost.sln --verify-no-changes
   ```
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd sync
   git push
   git status  # MUST show "up to with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds


<!-- BEGIN BEADS INTEGRATION -->
## Issue Tracking with bd (beads)

**IMPORTANT**: This project uses **bd (beads)** for ALL issue tracking. Do NOT use markdown TODOs, task lists, or other tracking methods.

### Why bd?

- Dependency-aware: Track blockers and relationships between issues
- Git-friendly: Auto-syncs to JSONL for version control
- Agent-optimized: JSON output, ready work detection, discovered-from links
- Prevents duplicate tracking systems and confusion

### Quick Start

**Check for ready work:**

```bash
bd ready --json
```

**Create new issues:**

```bash
bd create "Issue title" --description="Detailed context" -t bug|feature|task -p 0-4 --json
bd create "Issue title" --description="What this issue is about" -p 1 --deps discovered-from:bd-123 --json
```

**Claim and update:**

```bash
bd update bd-42 --status in_progress --json
bd update bd-42 --priority 1 --json
```

**Complete work:**

```bash
bd close bd-42 --reason "Completed" --json
```

### Issue Types

- `bug` - Something broken
- `feature` - New functionality
- `task` - Work item (tests, docs, refactoring)
- `epic` - Large feature with subtasks
- `chore` - Maintenance (dependencies, tooling)

### Priorities

- `0` - Critical (security, data loss, broken builds)
- `1` - High (major features, important bugs)
- `2` - Medium (default, nice-to-have)
- `3` - Low (polish, optimization)
- `4` - Backlog (future ideas)

### Workflow for AI Agents

1. **Check ready work**: `bd ready` shows unblocked issues
2. **Claim your task**: `bd update <id> --status in_progress`
3. **Work on it**: Implement, test, document
4. **Discover new work?** Create linked issue:
   - `bd create "Found bug" --description="Details about what was found" -p 1 --deps discovered-from:<parent-id>`
5. **Complete**: `bd close <id> --reason "Done"`

### Auto-Sync

bd automatically syncs with git:

- Exports to `.beads/issues.jsonl` after changes (5s debounce)
- Imports from JSONL when newer (e.g., after `git pull`)
- No manual export/import needed!

### Important Rules

- ✅ Use bd for ALL task tracking
- ✅ Always use `--json` flag for programmatic use
- ✅ Link discovered work with `discovered-from` dependencies
- ✅ Check `bd ready` before asking "what should I work on?"
- ❌ Do NOT create markdown TODO lists
- ❌ Do NOT use external issue trackers
- ❌ Do NOT duplicate tracking systems

For more details, see README.md and docs/QUICKSTART.md.

<!-- END BEADS INTEGRATION -->
