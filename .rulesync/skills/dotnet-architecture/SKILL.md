---
name: dotnet-architecture
description: |
  Design patterns and architectural guidance for .NET applications. Navigation skill covering vertical slices, domain modeling, messaging patterns, resilience, caching, and system design. For building scalable, maintainable applications.
  Keywords: architecture, patterns, vertical slices, domain modeling, messaging, resilience, caching, design patterns
license: MIT
targets: ['*']
category: architecture
subcategory: overview
tags:
  - dotnet
  - architecture
  - patterns
  - overview
version: '1.0.0'
author: 'dotnet-agent-harness'
related_skills:
  - dotnet-architecture-patterns
  - dotnet-domain-modeling
  - dotnet-messaging-patterns
  - dotnet-resilience
  - dotnet-solid-principles
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
codexcli:
  short-description: '.NET architecture - patterns, domain modeling, messaging'
opencode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash', 'Write', 'Edit']
copilot: {}
geminicli: {}
antigravity: {}
---

# .NET Architecture

Design patterns and architectural guidance for building scalable, maintainable .NET applications. This meta-skill provides navigation to ~15 architecture-focused skills covering vertical slices, domain modeling, messaging, resilience, and system design.

## When to Use This Skill

Load this skill when:
- Designing application architecture and structure
- Implementing domain-driven design patterns
- Setting up messaging and event-driven systems
- Adding resilience patterns (retry, circuit breaker)
- Choosing between architectural approaches

## Quick Navigation

### Core Patterns

| Need | Load Skill | Level |
|------|------------|-------|
| Vertical slices, minimal APIs | `dotnet-architecture-patterns` | Intermediate |
| Domain modeling, DDD | `dotnet-domain-modeling` | Advanced |
| SOLID principles | `dotnet-solid-principles` | Intermediate |

### Messaging & Events

| Need | Load Skill | Level |
|------|------------|-------|
| Pub/sub, competing consumers | `dotnet-messaging-patterns` | Advanced |
| Producer/consumer queues | `dotnet-channels` | Advanced |
| Real-time communication | `dotnet-realtime-communication` | Advanced |

### Resilience & Reliability

| Need | Load Skill | Level |
|------|------------|-------|
| Retry, circuit breaker | `dotnet-resilience` | Intermediate |
| Background services | `dotnet-background-services` | Intermediate |
| Service communication choice | `dotnet-service-communication` | Intermediate |

### Infrastructure

| Need | Load Skill | Level |
|------|------------|-------|
| Caching strategies | `dotnet-architecture-patterns` | Intermediate |
| Observability | `dotnet-observability` | Intermediate |
| Structured logging | `dotnet-structured-logging` | Intermediate |

### API Design

| Need | Load Skill | Level |
|------|------------|-------|
| API versioning | `dotnet-api-versioning` | Intermediate |
| Middleware patterns | `dotnet-middleware-patterns` | Intermediate |
| Minimal APIs | `dotnet-minimal-apis` | Intermediate |

### Modern Platforms

| Need | Load Skill | Level |
|------|------------|-------|
| Aspire patterns | `dotnet-aspire-patterns` | Advanced |
| gRPC services | `dotnet-grpc` | Intermediate |
| Real-time features | `dotnet-realtime-communication` | Advanced |

## Architectural Decision Trees

### Which Architecture Pattern?

```
Simple CRUD API → Minimal APIs (dotnet-minimal-apis)
        ↓
Complex Business Logic → Vertical Slices (dotnet-architecture-patterns)
        ↓
Distributed System → Aspire (dotnet-aspire-patterns)
        ↓
Event-Driven → Messaging Patterns (dotnet-messaging-patterns)
```

### Which Resilience Pattern?

| Scenario | Pattern | Skill |
|----------|---------|-------|
| Transient failures | Retry | `dotnet-resilience` |
| Cascading failures | Circuit Breaker | `dotnet-resilience` |
| Slow dependencies | Timeout | `dotnet-resilience` |
| Bulkhead isolation | Bulkhead | `dotnet-resilience` |

### Which Communication Style?

| Scenario | Pattern | Skill |
|----------|---------|-------|
| Synchronous request/response | REST/gRPC | `dotnet-service-communication` |
| Asynchronous events | Pub/Sub | `dotnet-messaging-patterns` |
| Real-time updates | SignalR/SSE | `dotnet-realtime-communication` |
| Background jobs | Channels | `dotnet-channels` |

## Complete Skill List

### Core Architecture (5 skills)
- `dotnet-architecture-patterns` - Vertical slices, pipelines, caching
- `dotnet-domain-modeling` - DDD, aggregates, value objects
- `dotnet-solid-principles` - SOLID/DRY principles
- `dotnet-middleware-patterns` - ASP.NET Core middleware
- `dotnet-minimal-apis` - Minimal API patterns

### Messaging & Events (4 skills)
- `dotnet-messaging-patterns` - Pub/sub, competing consumers
- `dotnet-channels` - Channel<T>, backpressure
- `dotnet-realtime-communication` - SignalR, SSE, gRPC streaming
- `dotnet-service-communication` - Protocol decisions

### Resilience (2 skills)
- `dotnet-resilience` - Polly v8, patterns
- `dotnet-background-services` - IHostedService

### Observability (3 skills)
- `dotnet-observability` - OpenTelemetry
- `dotnet-structured-logging` - Log pipelines
- `dotnet-api-versioning` - API versioning

### Modern Platforms (3 skills)
- `dotnet-aspire-patterns` - Distributed apps
- `dotnet-grpc` - gRPC services
- `dotnet-resilience` - Fault tolerance

## Cross-References

- **Web Development** → `dotnet-web` meta-skill
- **Data Access** → `dotnet-data` meta-skill
- **Performance** → `dotnet-performance` meta-skill
- **Security** → `dotnet-security` meta-skill
- **Fundamentals** → `dotnet-fundamentals` meta-skill

## Architectural Styles Covered

### Vertical Slice Architecture
- Feature-based organization
- Self-contained slices
- Reduced cross-feature coupling

### Domain-Driven Design
- Aggregate boundaries
- Value objects
- Domain events
- Repository patterns

### Event-Driven Architecture
- Pub/sub messaging
- Competing consumers
- Dead letter queues
- Saga patterns

### Resilience Patterns
- Retry with exponential backoff
- Circuit breaker
- Timeout and bulkhead
- Idempotency keys

## Version Assumptions

- .NET 8.0+ for most patterns
- Aspire requires .NET 8.0+
- gRPC requires .NET 6.0+
- HybridCache requires .NET 8.0+

## See Also

- [INDEX.md](/.rulesync/skills/INDEX.md) - Complete skill index
- [TAXONOMY.md](/.rulesync/skills/TAXONOMY.md) - Taxonomy schema
