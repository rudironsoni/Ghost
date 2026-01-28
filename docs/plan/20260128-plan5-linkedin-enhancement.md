# Plan 5: LinkedIn Platform Enhancement

**Date:** 2026-01-28
**Status:** Implemented
**Goal:** Upgrade `Ghost.Platform.LinkedIn` with advanced scraping capabilities (Authentication, Experience, Education) derived from `joeyism/linkedin_scraper`.

## Phase 1: Contracts & Data Models
Extend the Social contracts to support detailed profile information.

- [x] **Create DTOs**:
    - `src/Contracts/Ghost.Contracts.Social/DTOs/SocialExperience.cs`:
        - Title, Company, Location, Description, StartDate, EndDate, Duration, IsCurrent.
    - `src/Contracts/Ghost.Contracts.Social/DTOs/SocialEducation.cs`:
        - School, Degree, FieldOfStudy, StartDate, EndDate, Grade, Description.
- [x] **Update `SocialProfile.cs`**:
    - Add `List<SocialExperience> Experience`.
    - Add `List<SocialEducation> Education`.

## Phase 2: Authentication Service
Implement a robust authentication service to handle login state, cookies, and "warm-up" to reduce detection risk.

- [x] **Create `LinkedInAuthenticator`**:
    - **WarmUp**: Logic to visit safe sites (Google, GitHub) before login to prime the browser fingerprint.
    - **LoginWithCookie**: Logic to set the `li_at` cookie and verify session validity.
    - **IsLoggedIn**: Robust check using multiple selectors (Nav elements, Feed URL).
    - **RateLimit Detection**: Check for `checkpoint`, `challenge`, or `authwall` URLs.

## Phase 3: Advanced Profile Scraping
Port the sophisticated parsing logic from `joeyism/linkedin_scraper` into `LinkedInSocialClient`.

- [x] **Experience Parsing**:
    - Implement `ParseExperience` using `h2:has-text("Experience")` and ancestor traversal.
    - Support both "Main Page" (list items) and "Details Page" scraping.
    - **Text Extraction**: Implement the `span[aria-hidden="true"]` filtering logic to avoid duplicate screen-reader text.
    - **Nested Roles**: Handle companies with multiple roles (nested `pvs-list` items).
    - **Date Parsing**: Robust parser for "Jan 2020 - Present · 2 yrs" formats.
- [x] **Education Parsing**:
    - Implement `ParseEducation` using `h2:has-text("Education")`.
    - Extract School, Degree, and Dates using the same text cleaning strategies.

## Phase 4: Integration & Testing
- [x] **Update `LinkedInExtension`**: Register the `LinkedInAuthenticator` and updated `LinkedInSocialClient`.
- [x] **Unit Tests**:
    - Test Date Parsing logic with various LinkedIn formats.
    - Test Text Extraction logic (deduplication).
- [x] **Integration Test**:
    - Verify `SocialClient.GetProfileAsync` retrieves full Experience/Education data (requires valid session).
