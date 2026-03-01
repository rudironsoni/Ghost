# Plan 13: Integration & Aggregation
**Date:** 2026-01-29
**Status:** Completed

## 1. Context
Plan 12 delivered the *capabilities* (libraries), but they are not yet wired into the application.
- `Ghost.WebApi` lacks references to the new platforms.
- `IndeedExtension` is incompatible with the dynamic loader.
- There is no central point to query *all* scrapers (Aggregation).
- Docker configuration is missing the new toggles.

## 2. Implementation Steps

### Step 1: Standardize Extensions
- **Indeed:** Refactor `IndeedExtension` from a static helper to a class implementing `IExtension` (matching LinkedIn/Google pattern).
- **Glassdoor:** Ensure `GlassdoorExtension` is correct (it is, just needs reference).

### Step 2: WebApi Integration
- Add `ProjectReference`s to `src/Ghost.WebApi/Ghost.WebApi.csproj`:
    - `Ghost.Platform.Indeed`
    - `Ghost.Platform.Glassdoor`
    - (`Ghost.Platform.Google` is already there).

### Step 3: Aggregation Strategy (The "Scraper" Interface)
To avoid DI circular dependencies (Aggregator depending on `IEnumerable<IJobClient>` which includes itself):
1.  Define `interface IJobScraper : IJobClient` in `Ghost.Core`.
2.  Update all platforms (`Indeed`, `Google`, `Glassdoor`, `LinkedIn`) to implement `IJobScraper`.
3.  Update Platform Extensions to register their clients as `IJobScraper`.
4.  Implement `AggregatedJobClient` in `Ghost.Core`:
    - Implements `IJobClient`.
    - Injects `IEnumerable<IJobScraper>`.
    - `SearchJobsAsync`: Calls all scrapers in parallel, merges results, deduplicates using `IDeduplicationService`.
5.  Register `AggregatedJobClient` as the primary `IJobClient` in `Ghost.WebApi` (or a Core Extension).

### Step 4: Configuration
- Update `docker-compose.yml`:
    - `Ghost__Extensions__Indeed__Enabled=true`
    - `Ghost__Extensions__Indeed__Country=ES`
    - `Ghost__Extensions__Glassdoor__Enabled=true`
    - `Ghost__Extensions__Glassdoor__Country=ES`

## 3. Verification
- Run `Ghost.WebApi` locally or via test.
- Verify `AggregatedJobClient` is resolved when `IJobClient` is requested.
- Verify it calls multiple scrapers.
