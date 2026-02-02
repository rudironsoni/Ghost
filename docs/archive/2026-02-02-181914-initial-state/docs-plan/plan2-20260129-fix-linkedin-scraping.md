# Plan: Fix LinkedIn Job Scraping Issues

**Date:** 2026-01-29
**Status:** In Progress
**Goal:** Fix null/empty fields (Description, Salary, JobType, ExperienceLevel) in LinkedIn job scraping and resolve Guest API failures.

## Root Cause Analysis
- **Missing Data:** Likely due to changed DOM structure on LinkedIn job pages, causing CSS selectors to fail.
- **Guest API Failure:** "No jobs found" response suggests IP rate limiting or endpoint changes, forcing fallback to Browser/Hybrid strategy which is also failing to parse details.
- **Null Fields:** `JsonLdParser` may be missing fields if LinkedIn stopped injecting full JSON-LD, and fallback DOM selectors are outdated.

## Implementation Steps

### 1. Instrumentation & Diagnosis
- Add temporary HTML file dumping to `LinkedInJobClient.cs` and `GuestJobSearch.cs` to capture the exact page content during failures.
- Run `scripts/tests/linkedin/test_single_job.sh` to generate debug artifacts.
- **Action:** Inspect HTML to find correct selectors for:
    - Description
    - Company
    - Location
    - Job Criteria (Job Type, Experience)
    - Salary (if available)

### 2. Code Fixes
- **`LinkedInJobClient.cs` & `GuestJobSearch.cs`:**
    - Update CSS selectors based on HTML analysis.
    - Add robust fallback logic (Regex) for critical fields.
    - Implement `ParseSalary` if DOM elements exist.
- **`JsonLdParser.cs`:**
    - Verify JSON-LD structure matches current LinkedIn output.
    - Improve parsing resilience.

### 3. Unit Testing (Target: 80% Coverage)
- **New Test Files:**
    - `tests/Platforms/Ghost.Platform.LinkedIn.Tests/LinkedInJobClientTests.cs`
    - `tests/Platforms/Ghost.Platform.LinkedIn.Tests/Internal/GuestJobSearchTests.cs`
    - `tests/Platforms/Ghost.Platform.LinkedIn.Tests/Internal/JsonLdParserTests.cs`
- **Methodology:**
    - Use `NSubstitute` for `IBrowserSession`, `IPage`, `IProxyProvider`.
    - Mock HTTP responses/HTML content using `Playwright` mocks or internal logic.
    - Verify parsing logic with sample HTML files (from artifacts).

### 4. Verification
- Build solution.
- Run unit tests to ensure coverage and regression safety.
- Run `scripts/tests/linkedin/test_jobs.sh` and `test_single_job.sh`.
- Verify CLI output shows populated fields.

## Deliverables
- Verified `LinkedInJobClient` and `GuestJobSearch` with updated selectors.
- Comprehensive Unit Tests (80%+ coverage).
- Plan document.
