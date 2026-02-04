# ADR-0005: Multi-Strategy Parsing

## Status
Accepted (2026-02-01)

## Context
Website structures change frequently. Single parsing strategy leads to frequent breakages and maintenance burden.

## Decision
Implement three-tier fallback parsing:
1. Primary: DotnetSpider structured selectors
2. Secondary: JSON-LD schema extraction
3. Tertiary: Regex-based extraction

Apply to all platforms: Indeed, Glassdoor, Google Jobs

## Alternatives Considered
1. Single robust parser - Rejected: impossible to maintain across platforms
2. Browser-only extraction - Rejected: too slow
3. ML-based extraction - Rejected: overkill for structured data

## Consequences
- Positive: Graceful degradation when one strategy fails
- Positive: Reduced urgent maintenance
- Positive: Better uptime
- Negative: More code to maintain
- Negative: Slightly slower (tries multiple strategies)

## Evidence
- **Documents:**
  - docs/archive/2026/02/02/sisyphus_backup/plans/job-scraper-reliability-enhancement.md
- **Implementation:**
  - src/Platforms/Ghost.Platform.Indeed/Internal/IndeedMultiStrategyParser.cs
  - src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorMultiStrategyParser.cs
  - src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsMultiStrategyParser.cs
