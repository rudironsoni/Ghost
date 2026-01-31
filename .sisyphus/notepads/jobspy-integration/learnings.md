# Learnings - JobSpy Integration

## Task 5: Google Jobs Implementation Fixes

### JobSpy Patterns for Google Jobs

1. **JSON Extraction Pattern**:
   - JobSpy uses regex pattern: `520084652":([.*?]\s*])\s*}\s*]\s*]\s*]\s*]`
   - This pattern finds the widget key followed by nested JSON arrays
   - The pattern is more specific than just looking for `[` brackets

2. **Async Bootstrap String**:
   - The async_param is a long basejs/xjs bootstrap string
   - It's used in pagination requests to the async callback endpoint
   - JobSpy keeps it as a constant in `constant.py`

3. **Session Management**:
   - JobSpy uses `create_session()` which handles cookies automatically
   - Sessions are created with proxy rotation and retry capabilities
   - TLS fingerprinting is available but not always used

4. **Consent Handling**:
   - JobSpy doesn't explicitly handle consent pages in the code reviewed
   - They rely on proper headers and session management to avoid consent pages
   - The headers include extensive sec-ch-ua values to appear as a real browser

5. **Pagination**:
   - Uses `data-async-fc` attribute for cursor-based pagination
   - Pattern: `r'<div jsname="Yust4d"[^>]+data-async-fc="([^"]+)"'`
   - Pagination requests go to `https://www.google.com/async/callback:550`

### Current Ghost Implementation Issues

1. **XSSI Prefix Handling**:
   - Current implementation has basic XSSI prefix handling
   - Only checks for `)]}` prefix at the start
   - Could be more robust to handle various XSSI patterns

2. **Bootstrap String**:
   - Already configurable in GoogleJobsOptions
   - But code uses GoogleJobsConstants.AsyncBootstrapString instead
   - Need to use the options value

3. **Consent Detection**:
   - Extensive consent detection with multiple patterns
   - Could be centralized for better maintainability
   - Alternative URL attempts and proxy rotation are good

4. **Cookie Management**:
   - CookieContainer field exists but not used properly
   - Need to integrate with HttpClient for session persistence

5. **JSON Extraction**:
   - Parser uses fragile index-based field extraction
   - Could use more robust pattern matching like JobSpy

### Implementation Approach

1. **XSSI Prefix Handling**:
   - Add support for multiple XSSI prefix patterns
   - Handle `)]}`, `)]}'`, and other common prefixes
   - Strip whitespace before checking

2. **Configurable Bootstrap String**:
   - Use `_options.AsyncBootstrapString` instead of constant
   - Keep constant as default value in options

3. **Consent Detection**:
   - Create centralized consent detection method
   - Use regex patterns for better matching
   - Keep alternative URL and proxy rotation logic

4. **Cookie Management**:
   - Use HttpClientHandler with CookieContainer
   - Ensure cookies persist across requests
   - Consider using session factory for consistency

5. **JSON Extraction**:
   - Implement JobSpy-like regex pattern for widget key
   - Add fallback patterns for different JSON structures
   - Improve error handling for malformed JSON

---

## Session Summary - 2026-01-31

### Tasks Completed

#### Wave 1 - Foundation ✅
- **Task 1**: JobSpy Session Patterns - IMPLEMENTED
- **Task 2**: Test Infrastructure - IMPLEMENTED

#### Wave 2 - Platform Fixes ✅
- **Task 3**: Glassdoor - FIXED (CSRF, GraphQL, consent handling)
- **Task 4**: Indeed - FIXED (pagination, API key security)
- **Task 5**: Google Jobs - FIXED (JSON extraction, consent handling)

#### Wave 3 - EU Expansion ✅
- **Task 6**: InfoJobs (Spain) - IMPLEMENTED

### Platform Status
- **Active Platforms**: 8 total
  - Ghost.Platform.Anthropic
  - Ghost.Platform.Glassdoor ✅
  - Ghost.Platform.Google ✅
  - Ghost.Platform.Indeed ✅
  - Ghost.Platform.InfoJobs ✅
  - Ghost.Platform.LinkedIn
  - Ghost.Platform.OpenAI
  - Ghost.Platform.Common (shared)

- **Removed**: Tecnoempleo (no public API)

### Build Status
- **Result**: ✅ SUCCEEDS
- **Errors**: 0
- **Warnings**: 0

### Remaining Task
- **Task 7**: Performance Optimization (OPTIONAL/LOW PRIORITY)
  - Performance metrics collection
  - Success rate monitoring
  - Concurrent execution optimization
  - Performance dashboard

### Recommendation
Core functionality is complete. Task 7 is an optional enhancement that can be deferred or implemented based on user needs.
