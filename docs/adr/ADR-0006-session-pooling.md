# ADR-0006: Session Pooling with Tiered Browsers

## Status
Accepted (2026-01-28)

## Context
Browser startup is expensive (seconds). Creating new browser for each request is too slow. Need to reuse browser instances.

## Decision
Implement tiered browser pool (Hot/Warm/Cold):
- Hot pool: Pre-warmed browsers ready for immediate use
- Warm pool: Recently used browsers, quick to reactivate
- Cold pool: Available but need initialization
- Session rotation for anti-detection
- Proxy binding per session

## Alternatives Considered
1. New browser per request - Rejected: too slow
2. Single long-lived browser - Rejected: detection risk, memory leaks
3. Simple pool without tiers - Rejected: inefficient resource usage

## Consequences
- Positive: Sub-second response times
- Positive: Lower resource usage
- Positive: Rotating sessions evade detection
- Negative: Complex pool management
- Negative: Memory overhead from pooled browsers

## Evidence
- **Documents:**
  - docs/archive/2026/01/27/docs_plan/plan2-linkedin-world-class-scraper.md
- **Implementation:**
  - src/Core/Ghost/Pool/TieredBrowserPool.cs
  - src/Core/Ghost/Pool/TieredBrowserPoolOptions.cs
