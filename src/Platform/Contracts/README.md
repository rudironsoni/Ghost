# Ghost Platform Contracts

Layer 3 (Platform) - Contracts subdirectory containing platform-specific contracts and DTOs.

## Purpose

This directory contains data transfer objects (DTOs), request/response models, and platform-specific contracts used within the Platform layer. These contracts define the data structures exchanged between Platform services and between Platform and other layers.

## Contents

- Data Transfer Objects (DTOs) for Platform services
- Request and response models
- Platform-specific contract definitions
- Validation attributes and data annotations

## Architecture

The Contracts subdirectory is part of Layer 3 (Platform) in the Ghost architecture:

```
Layer 0: Kernel (Core engine)
Layer 1: Contracts (Public interfaces, DTOs - cross-layer contracts)
Layer 2: Plugins (Platform-specific plugins)
Layer 3: Platform (Shared infrastructure)
  - Abstractions/  (Interfaces, pure abstractions)
  - Contracts/     <- This directory (Platform-specific DTOs)
  - Extensions/    (Extension methods, utilities)
  - Hosting/       (Hosting infrastructure)
  - Observability/ (Telemetry, logging, metrics)
  - Storage/       (Persistence layer)
Layer 4: Engine (Scraper engines)
Layer 5: Apps (Deployable entrypoints)
Layer 6: Sdk (Framework for building scrapers)
```

## Guidelines

- Contracts should be serializable (JSON)
- DTOs should be immutable where possible (init-only properties, records)
- Include XML documentation for all public properties
- Use nullable reference types appropriately
- Keep contracts focused on specific use cases

## Relationship to Layer 1 Contracts

- Layer 1 (src/Contracts/): Cross-layer contracts shared with Plugins and other consumers
- Layer 3 Platform Contracts: Internal Platform layer contracts not exposed to Plugins

## Dependencies

- May reference: Layer 1 (Contracts)
- Should NOT reference: Plugins (Layer 2), Engine (Layer 4), Apps (Layer 5)
