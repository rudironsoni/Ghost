---
name: dotnet-performance
description: >
  Performance optimization and measurement for .NET applications. Navigation
  skill covering Span, ArrayPool, memory management, benchmarking, profiling,
  Native AOT, and optimization patterns. For building high-performance
  applications.

  Keywords: performance, optimization, span, arraypool, benchmarking, profiling,
  memory, gc, aot, native-aot
---
# .NET Performance

Performance optimization and measurement for .NET applications. This meta-skill provides navigation to ~10 performance-focused skills covering zero-allocation coding, benchmarking, profiling, Native AOT, and optimization patterns.

## When to Use This Skill

Load this skill when:
- Optimizing application performance and allocations
- Measuring performance with BenchmarkDotNet
- Profiling memory or CPU usage
- Building Native AOT applications
- Tuning garbage collection
- Reducing memory allocations

## Quick Navigation

### Optimization Patterns

| Need | Load Skill | Level |
|------|------------|-------|
| Span, ArrayPool, ref struct | `dotnet-performance-patterns` | Advanced |
| Type design for performance | `dotnet-csharp-type-design-performance` | Advanced |
| LINQ optimization | `dotnet-linq-optimization` | Intermediate |

### Memory Management

| Need | Load Skill | Level |
|------|------------|-------|
| GC tuning, LOH/POH | `dotnet-gc-memory` | Advanced |
| Struct vs class decisions | `dotnet-csharp-type-design-performance` | Advanced |

### Measurement

| Need | Load Skill | Level |
|------|------------|-------|
| BenchmarkDotNet setup | `dotnet-benchmarkdotnet` | Intermediate |
| Profiling tools | `dotnet-profiling` | Advanced |
| CI performance gates | `dotnet-ci-benchmarking` | Advanced |

### AOT & Compilation

| Need | Load Skill | Level |
|------|------------|-------|
| Native AOT publishing | `dotnet-native-aot` | Advanced |
| AOT architecture patterns | `dotnet-aot-architecture` | Advanced |
| WASM AOT | `dotnet-aot-wasm` | Advanced |
| Trimming | `dotnet-trimming` | Intermediate |
| Multi-targeting | `dotnet-multi-targeting` | Intermediate |

### Platform-Specific

| Need | Load Skill | Level |
|------|------------|-------|
| MAUI iOS/Catalyst AOT | `dotnet-maui-aot` | Advanced |

## Performance Decision Trees

### Which Optimization Technique?

```
Reduce allocations → Span<T>, ArrayPool<T> (dotnet-performance-patterns)
        ↓
Improve throughput → Struct optimization (dotnet-csharp-type-design-performance)
        ↓
Reduce startup time → Native AOT (dotnet-native-aot)
        ↓
Optimize queries → LINQ optimization (dotnet-linq-optimization)
```

### Which Measurement Tool?

| Goal | Tool | Skill |
|------|------|-------|
| Microbenchmarks | BenchmarkDotNet | `dotnet-benchmarkdotnet` |
| Live profiling | dotnet-trace | `dotnet-profiling` |
| Memory analysis | dotnet-counters | `dotnet-profiling` |
| Crash dumps | dotnet-dump | `dotnet-profiling` |
| CI gating | Automated benchmarks | `dotnet-ci-benchmarking` |

### When to Use Native AOT?

| Scenario | Recommendation |
|----------|----------------|
| CLI tools | Strongly recommended |
| Microservices | Consider for fast startup |
| Containers | Good for size reduction |
| Web APIs | Evaluate trade-offs |
| Libraries | Use IsTrimmable |

## Complete Skill List

### Optimization Patterns (3 skills)
- `dotnet-performance-patterns` - Span, ArrayPool, ref struct
- `dotnet-csharp-type-design-performance` - Struct vs class design
- `dotnet-linq-optimization` - IQueryable vs IEnumerable

### Memory & GC (2 skills)
- `dotnet-gc-memory` - GC tuning, LOH/POH
- `dotnet-performance-patterns` - Memory patterns

### Benchmarking & Profiling (3 skills)
- `dotnet-benchmarkdotnet` - BenchmarkDotNet
- `dotnet-profiling` - dotnet-counters, trace, dump
- `dotnet-ci-benchmarking` - CI performance gating

### AOT & Compilation (5 skills)
- `dotnet-native-aot` - PublishAot, descriptors
- `dotnet-aot-architecture` - AOT-first design
- `dotnet-aot-wasm` - Blazor/Uno WASM
- `dotnet-trimming` - Trimming annotations
- `dotnet-multi-targeting` - Polyfills, multi-TFM

### Platform-Specific (1 skill)
- `dotnet-maui-aot` - MAUI iOS/Catalyst optimization

### Build Optimization (2 skills)
- `dotnet-build-optimization` - Slow build diagnosis
- `dotnet-artifacts-output` - UseArtifactsOutput

## Performance Patterns

### Zero-Allocation Patterns

```csharp
// Use Span<T> instead of arrays
public void Process(Span<byte> data) { }

// Use ArrayPool<T> for temporary buffers
var buffer = ArrayPool<byte>.Shared.Rent(1024);
try { /* use buffer */ }
finally { ArrayPool<byte>.Shared.Return(buffer); }

// Use stackalloc for small fixed buffers
Span<int> stack = stackalloc int[100];
```

### Struct Design

```csharp
// Mark readonly for immutability
public readonly struct Point(double x, double y);

// Use ref readonly for large structs
public ref readonly Point GetOrigin();

// Seal classes for devirtualization
public sealed class Calculator { }
```

### GC Optimization

- Use `GC.TryStartNoGCRegion` for critical sections
- Pool objects to reduce Gen 0 collections
- Avoid LOH fragmentation with ArrayPool
- Consider GC configs for server apps

## Cross-References

- **Fundamentals** → `dotnet-fundamentals`
- **Architecture** → `dotnet-architecture`
- **Security** → `dotnet-security`
- **Web** → `dotnet-web`

## Version Assumptions

- .NET 8.0+ for modern patterns
- Native AOT requires .NET 7.0+
- HybridCache requires .NET 8.0+
- BenchmarkDotNet works on all versions

## Anti-Patterns to Avoid

1. **Premature optimization** - Measure first
2. **Ignoring GC pressure** - Monitor allocations
3. **Using ValueTask incorrectly** - Don't await twice
4. **Async void methods** - Always use Task
5. **Ignoring async state machine overhead** - Consider sync for hot paths

## See Also

- [INDEX.md](/.rulesync/skills/INDEX.md) - Complete skill index
- [TAXONOMY.md](/.rulesync/skills/TAXONOMY.md) - Taxonomy schema
