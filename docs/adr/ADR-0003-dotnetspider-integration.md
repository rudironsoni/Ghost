# ADR-0003: DotnetSpider Integration

## Status
Accepted (2026-02-01)

## Context
Need for structured HTML parsing with fallback strategies. Raw HTML parsing is fragile and breaks when sites change structure.

## Decision
Integrate DotnetSpider framework with:
- `ISessionOrchestrator` interface for session management
- `DotnetSpiderGhostAdapter` for platform integration
- Multi-strategy parsing with DotnetSpider as primary
- Fallback to JSON-LD and regex parsing

## Alternatives Considered
1. Custom HTML parsers - Rejected: too much maintenance
2. Headless browser only - Rejected: too resource intensive
3. API-only approach - Rejected: not all platforms have APIs

## Consequences
- Positive: Structured parsing reduces fragility
- Positive: Community-maintained selectors
- Positive: Multi-strategy fallback
- Negative: Additional dependency
- Negative: Learning curve for DotnetSpider

## Evidence
- **Documents:**
  - docs/archive/2026/02/02/sisyphus_backup/notepads/job-scraper-reliability-with-dotnetspider/decisions.md
  - docs/archive/2026/02/02/sisyphus_backup/plans/job-scraper-reliability-enhancement.md
