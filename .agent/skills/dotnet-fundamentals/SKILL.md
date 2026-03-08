---
name: dotnet-fundamentals
description: >
  Core C# and .NET language fundamentals. Navigation skill covering coding
  standards, language patterns, async/await, dependency injection,
  configuration, and foundational runtime concepts. Entry point for developers
  building .NET applications.

  Keywords: csharp, dotnet, fundamentals, coding standards, async, patterns,
  dependency injection, configuration, basics
---
# .NET Fundamentals

Core C# and .NET language fundamentals for building modern applications. This meta-skill provides navigation to ~25 foundational skills covering coding standards, language patterns, async programming, dependency injection, and runtime concepts.

## When to Use This Skill

Load this skill when:
- Starting a new .NET project and need foundational guidance
- Reviewing code for standards compliance
- Learning C# modern patterns and best practices
- Setting up dependency injection and configuration
- Understanding async/await patterns and cancellation

## Quick Navigation

### Coding Standards & Style

| Need | Load Skill | Level |
|------|------------|-------|
| Naming conventions, file layout | `dotnet-csharp-coding-standards` | Beginner |
| Nullable reference types | `dotnet-csharp-nullable-reference-types` | Intermediate |
| EditorConfig rules | `dotnet-editorconfig` | Intermediate |

### Language Patterns

| Need | Load Skill | Level |
|------|------------|-------|
| Records, pattern matching | `dotnet-csharp-modern-patterns` | Intermediate |
| Async/await, Task patterns | `dotnet-csharp-async-patterns` | Intermediate |
| Concurrency, locking | `dotnet-csharp-concurrency-patterns` | Advanced |
| Source generators | `dotnet-csharp-source-generators` | Advanced |

### Design & Architecture

| Need | Load Skill | Level |
|------|------------|-------|
| SOLID principles | `dotnet-solid-principles` | Intermediate |
| Dependency injection | `dotnet-csharp-dependency-injection` | Intermediate |
| Configuration/Options | `dotnet-csharp-configuration` | Intermediate |
| Validation patterns | `dotnet-validation-patterns` | Intermediate |

### Data & Runtime

| Need | Load Skill | Level |
|------|------------|-------|
| File I/O operations | `dotnet-file-io` | Intermediate |
| Serialization | `dotnet-serialization` | Intermediate |
| Channels/messaging | `dotnet-channels` | Advanced |
| Memory/GC | `dotnet-gc-memory` | Advanced |
| Pipelines | `dotnet-io-pipelines` | Advanced |

### Specialized

| Need | Load Skill | Level |
|------|------------|-------|
| Interop with native code | `dotnet-native-interop` | Advanced |
| AOT architecture | `dotnet-aot-architecture` | Advanced |
| Modernization | `dotnet-modernize` | Intermediate |
| Project structure | `dotnet-project-structure` | Beginner |

## Learning Paths

### Beginner Path (Week 1-2)

1. `dotnet-csharp-coding-standards` - Establish naming and style foundations
2. `dotnet-csharp-modern-patterns` - Learn modern C# features
3. `dotnet-csharp-dependency-injection` - Understand DI basics
4. `dotnet-solid-principles` - Apply SOLID principles

### Intermediate Path (Week 3-4)

1. `dotnet-csharp-async-patterns` - Master async/await
2. `dotnet-csharp-concurrency-patterns` - Handle threading
3. `dotnet-validation-patterns` - Implement validation
4. `dotnet-csharp-configuration` - Configure applications

### Advanced Path (Week 5+)

1. `dotnet-csharp-source-generators` - Build source generators
2. `dotnet-gc-memory` - Optimize memory usage
3. `dotnet-channels` - Implement producer/consumer patterns
4. `dotnet-aot-architecture` - Design for Native AOT

## Complete Skill List

### Coding Standards (3 skills)
- `dotnet-csharp-coding-standards` - Naming, file layout, style
- `dotnet-csharp-nullable-reference-types` - NRT annotations
- `dotnet-editorconfig` - Style enforcement

### Language Patterns (5 skills)
- `dotnet-csharp-modern-patterns` - Records, pattern matching
- `dotnet-csharp-async-patterns` - Async/await, cancellation
- `dotnet-csharp-concurrency-patterns` - Locking, concurrent collections
- `dotnet-csharp-source-generators` - Roslyn generators
- `dotnet-file-based-apps` - .NET 10 file-based apps

### Design & Principles (4 skills)
- `dotnet-solid-principles` - SOLID/DRY principles
- `dotnet-csharp-dependency-injection` - DI patterns
- `dotnet-csharp-configuration` - Options pattern
- `dotnet-validation-patterns` - Validation strategies

### Data & I/O (5 skills)
- `dotnet-file-io` - File operations
- `dotnet-serialization` - JSON, Protobuf
- `dotnet-channels` - Producer/consumer
- `dotnet-gc-memory` - Memory management
- `dotnet-io-pipelines` - High-perf I/O

### Diagnostics (2 skills)
- `dotnet-csharp-code-smells` - Anti-patterns
- `dotnet-modernize` - Modernization guidance

### Tooling (6 skills)
- `dotnet-solution-navigation` - Solution structure
- `dotnet-version-detection` - TFM detection
- `dotnet-version-upgrade` - Upgrade paths
- `dotnet-csproj-reading` - Project file reading
- `dotnet-native-interop` - Native interop
- `dotnet-aot-architecture` - AOT patterns

## Cross-References

- **Web Development** → `dotnet-web` meta-skill
- **Testing** → `dotnet-testing` meta-skill
- **Performance** → `dotnet-performance` meta-skill
- **Security** → `dotnet-security` meta-skill

## Version Assumptions

- .NET 8.0+ baseline for most skills
- .NET 10/C# 14 features noted where applicable
- Language features match TFM capabilities

## See Also

- [INDEX.md](/.rulesync/skills/INDEX.md) - Complete skill index
- [TAXONOMY.md](/.rulesync/skills/TAXONOMY.md) - Taxonomy schema
