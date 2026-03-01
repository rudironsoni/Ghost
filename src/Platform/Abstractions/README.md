# Ghost Platform Abstractions

Layer 3 (Platform) - Abstractions subdirectory containing interfaces and pure abstractions.

## Purpose

This directory contains interface definitions and pure abstractions that define the contracts for Platform layer services. These abstractions serve as the foundational contracts that implementations in other Platform subdirectories (Hosting, Storage, Observability) must fulfill.

## Contents

- Interface definitions for Platform services
- Abstract base classes for shared behavior
- Type definitions and enums for Platform layer

## Architecture

The Abstractions subdirectory is part of Layer 3 (Platform) in the Ghost architecture:

```
Layer 0: Kernel (Core engine)
Layer 1: Contracts (Public interfaces, DTOs)
Layer 2: Plugins (Platform-specific plugins)
Layer 3: Platform (Shared infrastructure)
  - Abstractions/  <- This directory (Interfaces, pure abstractions)
  - Contracts/     (Platform-specific contracts)
  - Extensions/    (Extension methods, utilities)
  - Hosting/       (Hosting infrastructure)
  - Observability/ (Telemetry, logging, metrics)
  - Storage/       (Persistence layer)
Layer 4: Engine (Scraper engines)
Layer 5: Apps (Deployable entrypoints)
Layer 6: Sdk (Framework for building scrapers)
```

## Guidelines

- Abstractions should have no dependencies on concrete implementations
- Interfaces should define clear contracts with XML documentation
- Abstract classes should provide common base functionality when appropriate
- Keep abstractions minimal and focused

## Dependencies

- May reference: Layer 1 (Contracts)
- Should NOT reference: Plugins (Layer 2), Engine (Layer 4), Apps (Layer 5)
