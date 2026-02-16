# Ghost Platform Observability

Layer 3 (Platform) - Observability subdirectory containing telemetry, logging, and metrics infrastructure.

## Purpose

This directory contains the observability infrastructure for the Ghost platform, including structured logging, metrics collection, distributed tracing, and health checks. It provides the necessary tools to monitor, debug, and understand the behavior of the system in production.

## Contents

- Structured logging infrastructure
- Metrics collection and exporters
- Distributed tracing configuration
- Health checks and probes
- Correlation ID management
- Telemetry middleware

## Architecture

The Observability subdirectory is part of Layer 3 (Platform) in the Ghost architecture:

```
Layer 0: Kernel (Core engine)
Layer 1: Contracts (Public interfaces, DTOs)
Layer 2: Plugins (Platform-specific plugins)
Layer 3: Platform (Shared infrastructure)
  - Abstractions/  (Interfaces, pure abstractions)
  - Contracts/     (Platform-specific contracts)
  - Extensions/    (Extension methods, utilities)
  - Hosting/       (Hosting infrastructure)
  - Observability/ <- This directory (Telemetry, logging, metrics)
  - Storage/       (Persistence layer)
Layer 4: Engine (Scraper engines)
Layer 5: Apps (Deployable entrypoints)
Layer 6: Sdk (Framework for building scrapers)
```

## Components

### Logging

- Structured logging with Serilog or similar
- Log levels: Debug, Information, Warning, Error, Critical
- Context enrichment (correlation IDs, request context)
- Sinks: Console, File, External aggregators

### Metrics

- Application performance counters
- Business metrics (job completion rates, error rates)
- Infrastructure metrics (memory, CPU, connections)
- Export formats: Prometheus, OpenTelemetry

### Tracing

- Distributed tracing for cross-service calls
- Span context propagation
- Integration with external APM tools (e.g., Jaeger, Zipkin)

### Health Checks

- Liveness probes (is the application running?)
- Readiness probes (is the application ready to serve?)
- Dependency health checks (database, external APIs)

## Guidelines

- All public methods must include appropriate logging
- Use structured logging (key-value pairs) over string interpolation
- Include correlation IDs in all log entries
- Metrics should use consistent naming conventions (snake_case recommended)
- Health checks should be lightweight and not impact performance

## Configuration

Observability components are typically configured via:

- `appsettings.json` for log levels and sinks
- Environment variables for feature flags
- Code configuration for custom enrichers and exporters

## Dependencies

- May reference: Layer 1 (Contracts), Platform Abstractions, Platform Contracts
- Should NOT reference: Plugins (Layer 2), Engine (Layer 4), Apps (Layer 5)
