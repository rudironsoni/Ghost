# Plan: Fix LinkedIn Job Search

## Objective
Restore functionality to the LinkedIn Job Search feature, which is currently failing with "No jobs found".

## Root Cause Analysis
1.  **Configuration Mismatch:** The application is running in `Production` environment (default), loading `appsettings.json`. This file lacks the `ScrapingStrategy: BrowserPage` setting, causing the system to default to `GuestApi`.
2.  **Guest API Failure:** The `GuestApi` strategy appears to be throttled or broken (returning 0 results), and the fallback logic isn't triggering effectively or is also failing.
3.  **Outdated Selectors:** The `BrowserPage` implementation in `LinkedInJobClient.cs` uses older CSS selectors (`.jobs-search-results__list-item`) which may not match the current LinkedIn DOM, especially for guest/public views (`.base-card`, `.jobs-search__results-list`).

## Remediation Plan

### 1. Enforce Browser Strategy
*   **File:** `src/Ghost.WebApi/appsettings.json`
*   **Action:** Explicitly set `"ScrapingStrategy": "BrowserPage"` for the LinkedIn extension. This forces the use of the authenticated/browser-based engine which is more reliable.

### 2. Robust Selectors Upgrade
*   **File:** `src/Platforms/Ghost.Platform.LinkedIn/LinkedInJobClient.cs`
*   **Action:** Update `SearchJobsAsync` to use a multi-selector strategy that covers:
    *   Authenticated List View: `.jobs-search-results__list-item`
    *   Guest/Public List View: `.jobs-search__results-list li`
    *   Generic Cards: `.base-card`
*   **Detail:** Update extraction logic for Title, Company, and Location to support both "base-card" (public) and "job-card" (private) class naming conventions.

### 3. Verification
*   **Script:** `scripts/tests/linkedin/test_jobs.sh`
*   **Success Criteria:** The script returns valid JSON with Job IDs, Titles, and Companies instead of "No jobs found".

## Execution
Proceed immediately with these file modifications and verification steps.
