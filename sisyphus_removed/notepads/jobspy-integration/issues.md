# Issues - JobSpy Integration

## Task 5: Google Jobs Implementation Fixes

### Issues Encountered

1. **Notepad Directory Missing**:
   - The `.
   - Created directory and initialized notepad files

2. **Context7 Library Search**:
   - JobSpy is not available in Context7
   - Had to use webfetch to get JobSpy source code directly from GitHub

3. **Test Infrastructure**:
   - Existing tests are for Gemini, not Google Jobs
   - Need to create new tests for Google Jobs functionality

### Known Issues in Current Implementation

1. **Hard-coded Widget Key**:
   - Widget key "520084652" is hard-coded in GoogleJobsConstants
   - Should be configurable to handle changes

2. **Fragile JSON Extraction**:
   - Parser relies on index-based field extraction
   - Field positions can change between Google updates
   - Need more robust pattern matching

3. **Cookie Container Not Used**:
   - CookieContainer field exists but not integrated with HttpClient
   - Cookies don't persist across requests
   - Need to use HttpClientHandler with CookieContainer

4. **Bootstrap String Duplication**:
   - AsyncBootstrapString is in both GoogleJobsConstants and GoogleJobsOptions
   - Code uses constant instead of options value
   - Should use options for configurability

5. **Consent Detection Scattered**:
   - Consent detection logic is repeated in multiple places
   - Should be centralized for better maintainability
   - Alternative URL attempts could be more organized

### Potential Future Issues

1. **Google May Change Widget Key**:
   - Widget key "520084652" may change in the future
   - Need fallback patterns or auto-discovery

2. **Bootstrap String May Expire**:
   - The async bootstrap string may change over time
   - Need mechanism to update it without code changes

3. **Rate Limiting**:
   - Google may implement stricter rate limiting
   - Need better backoff and retry mechanisms

4. **Consent Page Variations**:
   - Google may change consent page patterns
   - Need to keep consent detection patterns updated
