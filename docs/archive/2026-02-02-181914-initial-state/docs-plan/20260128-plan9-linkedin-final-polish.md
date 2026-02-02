# Plan 9: Final Polish of LinkedIn Platform

## Objective
Address the final functional gaps identified in the deep analysis: missing "See More" content expansion in profiles and missing content search functionality in the news client.

## 1. Social Profile Completeness (`LinkedInSocialClient`)
**Gap:** Long text in "About", "Experience", and "Education" sections is truncated with "See more" buttons, leading to incomplete data extraction.
**Fix:** Implement `ExpandSeeMoreAsync` to click these buttons before parsing.

*   **File:** `src/Platforms/Ghost.Platform.LinkedIn/LinkedInSocialClient.cs`
*   **Changes:**
    *   Add helper method `ExpandSeeMoreAsync(IPage page, IElement? container, CancellationToken ct)`.
    *   Selectors: `.inline-show-more-text__button`, `button[aria-label*='see more']`, `.pv-profile-section__see-more-inline`.
    *   Invoke this helper:
        *   In `GetProfileAsync` before extracting Name/Bio/About.
        *   In `ParseExperienceAsync` for each experience item.
        *   In `ParseEducationAsync` for each education item.

## 2. News Search Functionality (`LinkedInNewsClient`)
**Gap:** `SearchAsync` currently falls back to the feed and ignores the query.
**Fix:** Implement true content search.

*   **File:** `src/Platforms/Ghost.Platform.LinkedIn/LinkedInNewsClient.cs`
*   **Changes:**
    *   Update `SearchAsync` to navigate to `https://www.linkedin.com/search/results/content/?keywords={encodedQuery}`.
    *   Implement parsing logic for search result cards (likely sharing structure with feed updates but verify selectors).

## 3. Testing
*   **Project:** `tests/Platforms/Ghost.Platform.LinkedIn.Tests`
*   **Actions:**
    *   Update `LinkedInSocialClientTests` to verify `ExpandSeeMoreAsync` logic (mocking the button presence).
    *   Update `LinkedInNewsClientTests` (or add if missing) to verify `SearchAsync` navigation and parsing.

## 4. Verification
*   Run `dotnet test` with coverage to ensure >80%.
*   Run `dotnet run` to verify startup.
