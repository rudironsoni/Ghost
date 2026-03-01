# Ghost Platform Extensions

Layer 3 (Platform) - Extensions subdirectory containing extension methods and utilities.

## Purpose

This directory contains extension methods, helper utilities, and shared functionality used across the Platform layer. These utilities provide common operations that don't warrant their own service but are used throughout Platform components.

## Contents

- Extension methods for common types
- Utility classes for cross-cutting concerns
- Helper methods for string manipulation, collections, etc.
- Async utilities and task helpers

## Architecture

The Extensions subdirectory is part of Layer 3 (Platform) in the Ghost architecture:

```
Layer 0: Kernel (Core engine)
Layer 1: Contracts (Public interfaces, DTOs)
Layer 2: Plugins (Platform-specific plugins)
Layer 3: Platform (Shared infrastructure)
  - Abstractions/  (Interfaces, pure abstractions)
  - Contracts/     (Platform-specific contracts)
  - Extensions/    <- This directory (Extension methods, utilities)
  - Hosting/       (Hosting infrastructure)
  - Observability/ (Telemetry, logging, metrics)
  - Storage/       (Persistence layer)
Layer 4: Engine (Scraper engines)
Layer 5: Apps (Deployable entrypoints)
Layer 6: Sdk (Framework for building scrapers)
```

## Guidelines

- Extension methods should be in static classes with descriptive names
- Use the `this` modifier on the first parameter
- Keep extension methods focused and well-documented
- Prefer extension methods over utility classes when operating on existing types
- Follow .NET naming conventions (e.g., `GhostStringExtensions`, `GhostServiceCollectionExtensions`)

## Common Extension Categories

- `StringExtensions`: String manipulation helpers
- `CollectionExtensions`: LINQ enhancements, collection operations
- `HttpExtensions`: HTTP request/response helpers
- `ServiceCollectionExtensions`: DI registration helpers
- `ConfigurationExtensions`: Configuration binding helpers

## Dependencies

- May reference: Layer 1 (Contracts), Platform Abstractions
- Should NOT reference: Plugins (Layer 2), Engine (Layer 4), Apps (Layer 5)
