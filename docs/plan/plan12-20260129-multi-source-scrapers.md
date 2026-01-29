# Plan 12: Multi-Source Job Scraper Implementation
**Date:** 2026-01-29
**Status:** Approved
**Goal:** Implement a robust, multi-source job scraper (Indeed, Glassdoor, Google Jobs) within the .NET ecosystem, refactoring shared utilities into Ghost.Core.

## 1. Architecture & Strategy

### Principles
1.  **Platform Isolation:** Platform-specific scraping logic (selectors, API keys, constants) remains in `src/Platforms/Ghost.Platform.{Name}/`.
2.  **Shared Core:** Truly generic utilities (Date parsing, JSON-LD extraction, HTTP retry policies) move to `src/Core/Ghost/`.
3.  **Subplatform Pattern:** Google platform structured as subplatforms (Gemini, Jobs, AIStudio) with dynamic registration.
4.  **Multi-Country:** First-class support for `CountryCode` (ES, US, UK, etc.) from day one.
5.  **Quality:** 80%+ code coverage, production-ready reliability (polly retries, jitter).

### Directory Structure Updates
```text
src/
├── Core/Ghost/
│   ├── Abstractions/       # New Interfaces (IDateParser, IJsonLdExtractor...)
│   ├── Utilities/          # Generic implementations
│   ├── Http/               # StealthHttpClient, RetryPolicies
│   └── Models/             # CountryCode enum
├── Platforms/
│   ├── Ghost.Platform.Google/
│   │   ├── Jobs/           # Google Jobs Scraper
│   │   ├── Gemini/         # Existing AI Client
│   │   └── GoogleExtension.cs (Dynamic Registration)
│   ├── Ghost.Platform.Indeed/ (NEW)
│   └── Ghost.Platform.Glassdoor/ (NEW)
```

## 2. Implementation Phases

### Phase 1: Ghost.Core Enhancements
**Goal:** Establish the shared foundation.

*   **Interfaces:**
    *   `IJsonLdExtractor`: Generic generic JSON-LD extraction.
    *   `IDateParser`: "3 days ago", "Jan 2024 - Present".
    *   `ITextExtractor`: DOM text extraction.
    *   `IDeduplicationService`: SHA256(Title + Company).
    *   `ICountryDomainProvider`: Platform-specific domain mapping.
*   **Utilities:**
    *   `JsonLdExtractor` (Regex-based).
    *   `DateParser` (Generic).
    *   `DeduplicationService`.
    *   `SalaryFormatter`.
*   **HTTP Infrastructure:**
    *   `StealthHttpClient`: Wrapper for `HttpClient` handling jitter, headers, and proxy rotation.
    *   `RetryPolicy`: Polly-based exponential backoff (429, 5xx).
    *   `RateLimitOptions`: Configuration model.
*   **Models:** `CountryCode` enum.
*   **Testing:** Unit tests for all utilities.

### Phase 2: LinkedIn Refactoring
**Goal:** Migrate LinkedIn to use new Core interfaces without breaking functionality.

*   Refactor `Ghost.Platform.LinkedIn` to use `IJsonLdExtractor` and `IDateParser`.
*   Implement `LinkedInTextExtractor` (keeping `aria-hidden` logic).
*   Implement `LinkedInCountryProvider`.
*   Clean up `Internal/` folder (delete generic parsers).

### Phase 3: Google Platform Reorganization
**Goal:** Support multiple Google services cleanly.

*   **Structure:** Split into `Gemini/`, `Jobs/`, `AIStudio/`.
*   **Extension:** Update `GoogleExtension.cs` to register services dynamically based on `Enabled` flags.
*   **Options:** Hierarchical `GoogleOptions` (Root -> Subplatform).
*   **Google Jobs Implementation:**
    *   `GoogleJobClient`: Implements `IJobClient`.
    *   `GoogleJobsApiClient`: Handles Search + Async pagination (JobSpy logic).
    *   `GoogleJobsParser`: Extracts jobs from embedded JSON (widget key `520084652`).

### Phase 4: Indeed Platform (NEW)
**Goal:** High-volume scraping via GraphQL API (JobSpy logic).

*   **Project:** `Ghost.Platform.Indeed`.
*   **Components:**
    *   `IndeedApiClient`: Uses `https://apis.indeed.com/graphql`.
    *   `IndeedConstants`: Stores API Key, Query Template.
    *   `IndeedJobParser`: Maps GraphQL response to `JobListing`.
    *   `IndeedCountryProvider`: Maps `CountryCode` to `indeed-co` header.

### Phase 5: Glassdoor Platform (NEW)
**Goal:** Salary data extraction.

*   **Project:** `Ghost.Platform.Glassdoor`.
*   **Components:**
    *   `GlassdoorApiClient`: GraphQL (`/graph`) + CSRF Token scraping.
    *   `GlassdoorSalaryParser`: `p10`/`p90` extraction.
    *   `GlassdoorCountryProvider`.

## 3. Testing & Verification
*   **Unit Tests:** xUnit tests for all parsers, extractors, and API clients (mocking Http).
*   **Integration:** Verify DI container resolution in `Ghost.WebApi`.
*   **Coverage:** Minimum 80% coverage on new code.
