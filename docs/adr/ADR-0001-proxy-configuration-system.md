# ADR-0001: Proxy Configuration System

## Status
Accepted (2026-01-28)

## Context
Need for flexible proxy management across job platforms with geographic targeting, health checking, and automatic rotation. Original proxy configuration was tightly coupled to specific implementations.

## Decision
Create a new `Ghost.ProxyConfiguration` namespace with:
- Abstract `IProxySource` interface
- `RotatingProxyProvider` for automatic rotation
- `ApiProxySource` for API-based proxies (ProxyScrape, NordVPN)
- `StaticProxySource` for configured proxies
- Health checking and geographic targeting support

## Alternatives Considered
1. Extend existing `Ghost.Core.ProxySourceConfig` - Rejected due to tight coupling with existing code
2. External proxy service (paid) - Rejected due to cost and operational complexity
3. Single static proxy configuration - Rejected due to lack of flexibility

## Consequences
- Positive: Clean abstraction enabling multiple proxy sources
- Positive: Health checking prevents using failed proxies
- Positive: Geographic targeting allows region-specific scraping
- Negative: Additional abstraction layer to maintain
- Negative: More complex configuration

## Evidence
- **Documents:**
  - docs/archive/2026/02/02/sisyphus_backup/plans/ultimate-ghost-job-platforms-comprehensive-plan.md
  - docs/archive/2026/01/28/docs_plan/plan2-proxy-pool.md
- **Commits:**
  - 079d2e3 - feat: implement proxy pool system with rotating proxy provider
  - 0666354 - feat(proxy): add configuration to enable/disable proxy for LinkedIn sessions
- **Implementation:**
  - src/Core/Ghost/Abstractions/IProxySource.cs
  - src/Core/Ghost/Services/RotatingProxyProvider.cs
