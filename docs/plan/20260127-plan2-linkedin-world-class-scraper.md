# Plan: World-Class LinkedIn Scraper Upgrade

**Date:** 2026-01-27
**Status:** Implemented
**Objective:** Upgrade `Ghostwright.Platform.LinkedIn` to parity with the legacy SDK and beyond, implementing Guest API strategies, JSON-LD parsing, and stealth enhancements.

---

## 1. Gap Analysis & Requirements

### Missing Features (Critical)
| Feature      | Legacy SDK (`Ghostwright.LinkedIn`)                               | Current Platform (`Ghostwright.Platform.LinkedIn`)       | Impact                  |
| ------------ | --------------------------------------------------------------- | ------------------------------------------------------ | ----------------------- |
| **Guest API**    | ✅ Uses `jobs-guest` endpoints for high-speed, auth-free scraping | ❌ Browser-only scraping (slow, prone to detection)    | **Critical**: Speed & Scale |
| **Data Quality** | ✅ `JsonLdParser` extracts precise salary, dates, and schema data | ❌ CSS Selectors only (brittle, missing hidden fields) | **High**: Data richness     |
| **Stealth**      | ✅ Intelligent proxy rotation & geolocation matching            | ❌ No proxy support in Kernel yet                      | **Critical**: Ban rate      |
| **Pagination**   | ✅ "Human" infinite scroll & load detection                     | ❌ First page (25 results) only                        | **High**: Bulk data         |

---

## 2. Technical Architecture Changes

### Phase 1: Core Kernel Upgrades (Prerequisite)
The `Ghostwright` kernel must support network stealth features to enable the platform upgrades.

**Tasks:**
1.  **Update `SessionOptions`**: Add properties for `Proxy` (server, auth) and `Geolocation` (lat, long, accuracy).
2.  **Update `BrowserSessionWrapper`**: Pass these options to the underlying Patchright context creation.
    *   *Note:* Using `IPage` for API calls ensures we keep the browser's TLS fingerprint, which is safer than using `HttpClient`.

### Phase 2: Intelligence Layer (Internal)
Port the robust parsing logic to the new platform.

**Tasks:**
1.  **Port `JsonLdParser`**: Create `src/Platforms/Ghostwright.Platform.LinkedIn/Internal/JsonLdParser.cs`.
    *   Use Regex to extract `<script type="application/ld+json">`.
    *   Deserialize to internal DTOs.
    *   Map to `JobListing` contract.
2.  **Create `JobScrapingStrategy` Enum**: `GuestApi` (Fast), `Browser` (Deep), `Hybrid` (Resilient).

### Phase 3: The Guest API Engine
Implement the high-performance scraping strategy.

**Tasks:**
1.  **Implement `SearchViaGuestApiAsync`**:
    *   Target: `https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search`
    *   Logic: Iterate `start` parameter (0, 25, 50...).
    *   Parsing: Extract Job ID from HTML list items.
2.  **Implement `FetchJobDetailsGuestAsync`**:
    *   Target: `https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{id}`
    *   Parsing: Use `JsonLdParser` first, fall back to CSS.

### Phase 4: Browser Scraper Hardening
Improve the fallback browser strategy for when the API is blocked.

**Tasks:**
1.  **Human Scrolling**: Replace `window.scrollTo` with a "smooth scroll" loop that varies speed to mimic human behavior.
2.  **Lazy Load Wait**: Implement logic to verify the DOM height has changed or new `.job-card-container` elements have appeared before stopping.

---

## 3. Implementation Steps

### Step 1: Core Kernel
- [x] Modify `src/Core/Ghostwright/Core/SessionOptions.cs`
- [x] Modify `src/Core/Ghostwright/Internal/BrowserSessionWrapper.cs` (or creation logic in Kernel)

### Step 2: LinkedIn Platform Internals
- [x] Create `Internal/JsonLdParser.cs`
- [x] Update `LinkedInOptions.cs` (Add Strategy, Proxy configs)

### Step 3: Job Client Logic
- [x] Refactor `LinkedInJobClient.cs`:
    -   Add `Switch` statement for strategy.
    -   Implement `SearchViaGuestApiAsync`.
    -   Implement `SearchViaBrowserAsync` (improved).
- [x] Update `SearchJobsAsync` to support pagination/infinite scroll.

### Step 4: Verification
- [x] Create test: `LinkedInJobClientTests.GuestApi_Parses_JsonLd`
- [x] Create test: `LinkedInJobClientTests.Browser_Scrolls_Infinite`

---

## Implementation Notes

- Added `GuestJobSearch` internal engine.
- Added `JsonLdParser` for robust data extraction.
- Added `ScrapingStrategy` (GuestApi, BrowserPage, Hybrid) to `LinkedInOptions`.
- Upgraded `GhostwriterKernel` to support Proxy/Geolocation in session options.
