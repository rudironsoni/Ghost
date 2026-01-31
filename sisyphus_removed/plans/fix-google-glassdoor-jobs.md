# Fix Google Jobs & Glassdoor Job Search Platforms

## Executive Summary

Both Google Jobs and Glassdoor platforms are failing due to **fragile web scraping implementations** that rely on hardcoded patterns, outdated selectors, and structure-dependent parsers. Google's widget key and JSON array structure, along with Glassdoor's CSRF token patterns and GraphQL schema, have likely changed since the original implementation.

**Critical Context**: Neither platform offers an official public API for job search aggregation:
- **Google Jobs**: Official API was discontinued in 2021; only Cloud Talent Solution (enterprise API for job boards) remains
- **Glassdoor**: API closed to new signups since February 2020; only available to existing partners

This means scraping is the only viable approach, but current implementations use brittle techniques that break when sites update their structure.

---

## External API Research Findings

### Google Jobs API Status (2024-2025 Research)

**Critical Finding: NO Official Public API Exists**

- The Google Jobs API for scraping search results was **discontinued in 2021**
- Only **Cloud Talent Solution** remains, which is an enterprise API for job boards to power their own search (NOT for scraping Google Jobs)
- Current version: v4 (v3 deprecated as of December 2024)
- Requires Google Cloud project with billing, OAuth 2.0 authentication

**Alternative Options**:
1. **Third-party scraping APIs**: SerpApi, ScraperAPI, Scrapingdog, SearchApi.io (paid services that handle anti-bot detection)
2. **DIY Scraping**: Technically possible but challenging due to anti-bot measures

**Legal Considerations**:
- Scraping public data is generally legal in most jurisdictions
- However, Google's Terms of Service likely prohibit automated scraping
- Recent court rulings (hiQ vs LinkedIn) support scraping public data

### Glassdoor API Status (2024-2025 Research)

**Critical Finding: API Closed to New Partners Since 2020**

- Glassdoor's API partner program **closed February 21, 2020** and has not reopened
- Existing partners can still use the API, but no new registrations accepted
- API endpoints still exist but require Partner ID (t.p) and Partner Key (t.k)

**Alternative Options**:
1. **Third-party APIs**: Apify ($30/month), RapidAPI, Mantiks, Piloterr
2. **DIY Scraping**: Common approach but requires handling anti-bot measures

**Recent Developments**:
- July 2025: Glassdoor/Indeed cut 1,300 jobs amid AI integration focus
- August 2025: Glassdoor Scraper tools updated to handle interface changes
- No evidence of API reopening

---

## Root Cause Analysis

### Google Jobs Failures

**Primary Issues:**
1. **Outdated Widget Key**: Uses hardcoded widget key `520084652` which may have changed
2. **HTML Parser Fragility**: `GoogleJobsParser` relies on finding specific JSON array markers (`htl;jobs`, widget key) that may no longer exist in Google's current HTML structure
3. **Dead Proxy List**: All 9 public proxies in `ProxyList` are likely dead or blocked by Google
4. **Outdated Headers**: Chrome 130 headers may need updating to current versions
5. **Static Async Bootstrap**: The `AsyncBootstrapString` is hardcoded and may be invalid
6. **Consent Bypass Limitations**: Multiple strategies exist but may not handle current Google anti-bot measures

**Key Failure Points (from code analysis):**
- Line 85-207 in `GoogleJobsApiClient.cs`: If consent page detected, tries alternative URLs then proxies - all may fail
- Line 219: `cursorMatch` regex may not find cursor for pagination
- Line 223: `GoogleJobsParser.ParseFromHtml` returns empty if structure doesn't match
- Line 81-113 in `GoogleJobsParser.cs`: Parser returns empty if widget key, htl;jobs marker, or '[' not found

### Glassdoor Failures

**Primary Issues:**
1. **CSRF Token Extraction Failure**: Regex patterns in `ExtractCsrfTokenWithMultiplePatterns` may not match current Glassdoor HTML
2. **Outdated GraphQL Schema**: The `JobSearchQuery` structure may have changed
3. **Invalid Fallback Token**: Hardcoded `FallbackToken` may be expired/invalid
4. **Static Location Parameters**: Always uses `locationId = 11047` and `locationType = "STATE"` regardless of actual location
5. **Insufficient Error Logging**: Silent failures when CSRF extraction or API calls fail

**Key Failure Points (from code analysis):**
- Line 76 in `GlassdoorApiClient.cs`: `ExtractCsrfTokenWithMultiplePatterns` returns null if patterns don't match
- Line 83: Falls back to hardcoded token which may be invalid
- Line 172-173: Search uses location ID 11047 (appears to be US/remote) for ALL searches regardless of criteria
- Line 222: `ParseGraphQLErrors` may categorize errors incorrectly
- Line 35 in `GlassdoorJobParser.cs`: Parser expects specific property names that may have changed

---

## Implementation Roadmap

### Phase 1: Immediate Diagnostics (High Priority)

**Goal**: Understand exactly what's failing by capturing and analyzing actual responses.

#### Task 1.1: Enhance Logging for Google Jobs
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs`

**Changes**:
- Add detailed logging at each consent bypass attempt
- Log the actual HTML structure received (first 500 chars) when parser returns empty
- Log proxy success/failure with timing
- Add logging for cursor extraction success/failure
- Log when widget key not found in HTML

**Rationale**: Currently can't tell if failing at consent page, parser, or elsewhere. Need visibility.

#### Task 1.2: Enhance Logging for Glassdoor
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Glassdoor/Internal/GlassdoorApiClient.cs`

**Changes**:
- Log CSRF token extraction attempt results
- Log actual HTML content when CSRF extraction fails
- Log GraphQL response structure when errors detected
- Log the actual location parameters being sent
- Add timing logs for each retry attempt

**Rationale**: Silent failures make debugging impossible. Need to see actual API responses.

#### Task 1.3: Create Debug Output Files
**Files**: Both platforms

**Changes**:
- Ensure debug HTML/JSON files are written to `logs/` directory (code exists but verify permissions)
- Add timestamp to filenames for correlation
- Add request/response headers to debug output

**Rationale**: Need to inspect actual responses to understand structural changes.

---

### Phase 2: Fix Google Jobs Parser (Critical)

**Goal**: Make the parser resilient to Google's HTML structure changes.

#### Task 2.1: Update Parser Heuristics
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsParser.cs`

**Changes**:
1. **Dynamic Widget Key Detection**: Instead of hardcoded `520084652`, search for any 9+ digit number in data attributes near job listings
2. **Multiple JSON Search Strategies**:
   - Strategy 1: Look for widget key pattern (dynamic)
   - Strategy 2: Search for `"jobs"` or `"job"` in script tags with type="application/json"
   - Strategy 3: Look for `data-ved` attributes which Google commonly uses
   - Strategy 4: Extract from `AF_initDataCallback` patterns (Google's data initialization)
3. **JSON-LD Support**: Parse JSON-LD structured data if present
4. **Better Error Messages**: Log which strategy succeeded/failed

**Rationale**: Google's HTML structure changes frequently. Parser needs multiple fallback strategies.

#### Task 2.2: Implement Browser-First Strategy
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.Google/Jobs/GoogleJobClient.cs`

**Changes**:
- Reverse the order: Try browser first (more reliable), then HTTP API as fallback
- Or add configuration option to control strategy order
- LinkedIn uses browser-first and works better - follow that pattern

**Rationale**: Browser automation handles dynamic content and consent pages more reliably than HTTP scraping.

#### Task 2.3: Update Headers and User Agent
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsConstants.cs`

**Changes**:
- Update to current Chrome version (133+ as of early 2025)
- Add rotating User-Agent capability
- Consider using mobile User-Agent (sometimes less strict blocking)

**Rationale**: Outdated browser signatures trigger bot detection.

#### Task 2.4: Remove Dead Proxies
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs`

**Changes**:
- Remove the hardcoded `ProxyList` (lines 17-28)
- Instead, rely on `IProxyProvider` if configured, or direct connection
- If proxy is needed, it should come from configuration, not hardcoded dead proxies

**Rationale**: Dead proxies waste time and cause unnecessary failures.

---

### Phase 3: Fix Glassdoor API Integration (Critical)

**Goal**: Make Glassdoor API calls work with current GraphQL schema and authentication.

#### Task 3.1: Fix Location Parameter Handling
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Glassdoor/Internal/GlassdoorApiClient.cs`

**Changes**:
1. **Implement Location Resolution**: 
   - Search query "Remote" should use appropriate location ID
   - Search query "Spain" should resolve to Spain's location ID
   - Add location cache/mapping for common locations
2. **Dynamic Location Type**:
   - Use "COUNTRY" for country searches
   - Use "CITY" for city searches
   - Use "STATE" for state searches
   - Not always "STATE"

**Code Changes** (around line 89-93):
```csharp
// Instead of hardcoded:
var locationId = 11047;
var locationType = "STATE";

// Should be:
var (locationId, locationType) = await ResolveLocationAsync(location);
```

**Rationale**: Current code ignores location parameter entirely - always searches US/remote.

#### Task 3.2: Improve CSRF Token Extraction
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Glassdoor/Internal/GlassdoorApiClient.cs`

**Changes**:
1. **Add More Patterns**: Look for CSRF in:
   - `window.__INITIAL_STATE__`
   - `window.__DATA__`
   - `<script id="__INITIAL_STATE__" type="application/json">`
   - Any script tag containing `"csrf"` or `"token"`
2. **JSON-Based Extraction**: Parse all JSON script tags and search recursively for token
3. **API Endpoint Validation**: Test if token works before using it

**Rationale**: Glassdoor likely changed how they embed CSRF tokens.

#### Task 3.3: Update GraphQL Query
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Glassdoor/Internal/GlassdoorConstants.cs`

**Changes**:
1. **Simplify Query**: Start with minimal query that just gets essential fields
2. **Add Query Validation**: Test query structure against actual endpoint
3. **Make Query Dynamic**: Only request fields that are needed
4. **Update Apollo Headers**: Ensure Apollo client version is current

**Rationale**: Complex queries are more likely to break. Start minimal and expand.

#### Task 3.4: Implement Browser Fallback Strategy
**Files**: `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Glassdoor/Internal/GlassdoorBrowserClient.cs`

**Changes**:
1. **Primary Strategy**: Use browser-first approach (like LinkedIn)
2. **Search URL Generation**: Build proper search URLs with location parameters
3. **DOM Extraction**: Use multiple selector strategies:
   - Look for `data-test` attributes
   - Look for semantic HTML (article, job-listing classes)
   - Extract from JSON-LD if present
4. **Consent Handling**: More robust consent button detection

**Rationale**: Browser automation is more resilient to API changes.

---

### Phase 4: Add Resilience Features (Medium Priority)

**Goal**: Make both platforms more resilient to future changes.

#### Task 4.1: Add Retry with Exponential Backoff
**Files**: Both platforms' API clients

**Changes**:
- Standardize retry logic across both platforms
- Add jitter to prevent thundering herd
- Different retry strategies for different error types:
  - 429 (rate limit): Longer backoff
  - 5xx: Standard backoff
  - Parser failure: No retry (structural issue)

#### Task 4.2: Add Health Check Endpoint
**Files**: New feature

**Changes**:
- Create `/api/jobs/health` endpoint that tests each platform
- Returns status: healthy, degraded, or failing
- Includes last successful search timestamp
- Useful for monitoring

#### Task 4.3: Add Structured Error Reporting
**Files**: Both platforms

**Changes**:
- Instead of returning empty list, return error information
- Include error category: Auth, Network, Parse, RateLimit
- Include suggestion: "Try browser fallback", "Check credentials", etc.

---

### Phase 5: Configuration & Testing (Medium Priority)

**Goal**: Ensure fixes work and can be configured appropriately.

#### Task 5.1: Update Configuration Schema
**Files**: 
- `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Ghost.Platform.Google/Jobs/GoogleJobsOptions.cs`
- `/home/rrj/src/github/rudironsoni/Ghost/src/Platforms/Glassdoor/GlassdoorOptions.cs`

**Changes**:
- Add `Strategy` option: HttpFirst, BrowserFirst, HttpOnly, BrowserOnly
- Add `Timeout` configuration
- Add `MaxRetries` configuration
- Add `DebugMode` to always save HTML/JSON responses

#### Task 5.2: Create Integration Tests
**Files**: Test projects

**Changes**:
- Create tests that verify each platform can return jobs
- Mock HTML responses to test parser resilience
- Test consent page handling
- Test rate limiting behavior

#### Task 5.3: Update Documentation
**Files**: README.md

**Changes**:
- Document known limitations of scraping-based platforms
- Document configuration options
- Document troubleshooting steps

---

## Recommended Execution Order

### Week 1: Diagnostics & Quick Fixes
1. **Day 1-2**: Add comprehensive logging (Tasks 1.1, 1.2)
2. **Day 3**: Run searches, collect debug output, analyze actual responses
3. **Day 4-5**: Implement quick fixes based on findings:
   - Remove dead proxies (Task 2.4)
   - Fix location handling in Glassdoor (Task 3.1)
   - Update headers (Task 2.3)

### Week 2: Core Fixes
1. **Day 1-3**: Rewrite Google Jobs parser with multiple strategies (Task 2.1)
2. **Day 4-5**: Fix Glassdoor CSRF extraction and GraphQL (Tasks 3.2, 3.3)

### Week 3: Resilience & Testing
1. **Day 1-2**: Implement browser-first strategies (Tasks 2.2, 3.4)
2. **Day 3-4**: Add health checks and error reporting (Tasks 4.2, 4.3)
3. **Day 5**: Integration testing and documentation (Tasks 5.2, 5.3)

---

## Key Files to Modify

### Google Jobs
1. `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs`
2. `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsParser.cs`
3. `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsConstants.cs`
4. `src/Platforms/Ghost.Platform.Google/Jobs/GoogleJobClient.cs`

### Glassdoor
1. `src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs`
2. `src/Platforms/Glassdoor/Internal/GlassdoorBrowserClient.cs`
3. `src/Platforms/Glassdoor/Internal/GlassdoorConstants.cs`
4. `src/Platforms/Glassdoor/Internal/GlassdoorJobParser.cs`

---

## Success Criteria

### Minimum Viable Fix
- [ ] Google Jobs returns at least 3 jobs for "Software Engineer" in "Remote"
- [ ] Glassdoor returns at least 3 jobs for "Data Engineer" in "Remote"
- [ ] Both platforms log detailed diagnostics when run in debug mode

### Full Implementation
- [ ] Both platforms use browser-first strategy by default
- [ ] Parsers handle multiple HTML/JSON structures
- [ ] Location parameters are correctly passed and used
- [ ] Health check endpoint reports platform status
- [ ] Integration tests pass consistently

---

## Risk Mitigation

### Technical Risks
1. **Google/Glassdoor block scraping entirely**: 
   - Mitigation: Browser automation is harder to block than HTTP scraping
   - Mitigation: Add proxy support through configuration

2. **Structure changes again after fixes**:
   - Mitigation: Design parsers with multiple fallback strategies
   - Mitigation: Add monitoring to detect when platform returns 0 jobs consistently

3. **Browser automation too slow/unreliable**:
   - Mitigation: Make it configurable (HTTP vs Browser)
   - Mitigation: Add timeouts and circuit breakers

### Legal/Ethical Considerations
1. **Terms of Service**:
   - **Google**: Scraping Google Jobs violates Google's ToS; they actively block scrapers
   - **Glassdoor**: Scraping likely violates ToS; API closed to new partners since 2020
   - **Mitigation**: Document this risk in README and legal notices
   - **Mitigation**: Use third-party APIs (SerpApi, Apify) that handle legal compliance
   - **Mitigation**: Rate limiting and respectful usage (max 1 request per 3 seconds)
   - **Mitigation**: Consider focusing on platforms with official APIs (Indeed, LinkedIn)

2. **Data Rights**:
   - Job posting data may be copyrighted by employers
   - Recent court cases (Jobiak vs Aspen 2023-2024) around job data scraping
   - **Mitigation**: Only extract publicly visible data
   - **Mitigation**: Don't store or redistribute full job descriptions
   - **Mitigation**: Link back to original source

2. **Rate Limiting**:
   - Current implementations have some rate limiting
   - Ensure it's conservative enough to avoid IP bans

---

## Comparison with Working Implementation (LinkedIn)

**What LinkedIn Does Better**:
1. **Browser-first strategy**: Uses browser automation as primary method
2. **Multiple selector fallbacks**: Tries many different CSS selectors
3. **Cookie clearing**: Clears cookies between attempts
4. **Better error handling**: Detailed logging at each step
5. **Deep fetching**: Gets full job details for each listing

**What We Should Adopt**:
1. Browser-first approach for both platforms
2. Multiple CSS selector strategies
3. Clear cookies between attempts
4. Detailed step-by-step logging
5. Separate shallow search from deep detail fetching

---

## Immediate Next Steps

### Step 1: Run Diagnostics (Do This First)
```bash
# Run the application
dotnet run --project src/Ghost.WebApi

# In another terminal, run the test scripts
./examples/scripts/job-search/search_google.sh
./examples/scripts/job-search/search_glassdoor.sh

# Check for debug output files
ls -la logs/
cat logs/google_jobs_search.html | head -100
cat logs/glassdoor_search_0.json | head -50
```

**What to Look For**:
- Google: Is it a consent page? Does HTML contain job data? Is widget key present?
- Glassdoor: Is CSRF token extracted? What does the GraphQL response say?

### Step 2: Quick Decision Matrix

Based on diagnostics, choose path:

**If parsers are broken (HTML structure changed)**:
→ Implement Phase 2 & 3 (Parser fixes + Browser-first)

**If consent/anti-bot blocking (403/429 errors)**:
→ Implement Phase 3.4 (Browser automation) OR evaluate third-party APIs

**If both are severely broken**:
→ Consider **Option A** (third-party APIs) or **Option B** (focus on other platforms)

### Step 3: Implement Phase 1 (Logging)

Add logging immediately so you can see what's happening:
- Task 1.1: Enhanced Google Jobs logging
- Task 1.2: Enhanced Glassdoor logging
- Task 1.3: Debug output files

**Estimated Time**: 2-4 hours
**Impact**: High visibility into failures

### Step 4: Quick Wins

While researching full fixes, implement these immediately:
1. **Remove dead proxies** from GoogleJobsApiClient.cs (lines 17-28)
2. **Fix location bug** in Glassdoor (always uses locationId 11047)
3. **Update User-Agent** to current Chrome version

**Estimated Time**: 1-2 hours
**Impact**: May fix some issues immediately

### Step 5: Evaluate Third-Party APIs

Before investing 2-3 weeks in scraping fixes:
1. Sign up for SerpApi trial (free)
2. Test Google Jobs API endpoint
3. Evaluate data quality vs. current implementation
4. Calculate cost vs. development time

**SerpApi Example**:
```bash
curl "https://serpapi.com/search?engine=google_jobs&q=Software+Engineer&location=Remote&api_key=YOUR_KEY"
```

If third-party APIs provide good data, consider **Option A** instead of maintaining scrapers.

---

## Alternative Approaches (Strategic Options)

Given that both platforms lack official public APIs, consider these strategic alternatives:

### Option A: Third-Party Scraping APIs (Recommended for Production)

Instead of maintaining fragile scrapers, integrate with commercial scraping services:

**For Google Jobs**:
- **SerpApi** (~$50/month): `https://serpapi.com/google-jobs-api`
- **ScraperAPI** (~$49/month): Handles proxy rotation, CAPTCHAs
- **SearchApi.io** (~$40/month): Google Jobs-specific endpoint

**For Glassdoor**:
- **Apify** ($30/month): Pre-built Glassdoor scraper
- **RapidAPI** (pay-per-use): Real-time Glassdoor data
- **Mantiks**: Job postings API with Glassdoor data

**Pros**: 
- No maintenance burden
- Handle anti-bot detection automatically
- Structured JSON output
- Legal compliance handled by provider

**Cons**: 
- Monthly costs
- Rate limits
- Data freshness may vary

### Option B: Focus on Platforms with Official APIs

Redirect development effort to job platforms with official APIs:
- **LinkedIn** (already working)
- **Indeed** (has official API)
- **InfoJobs** (already implemented)
- **ZipRecruiter** (has API)
- **Monster** (has API)

### Option C: Hybrid Approach (Recommended Implementation)

Keep current scraping implementations as fallback, but prioritize:
1. **LinkedIn** (most reliable via browser automation)
2. **Indeed** (if official API available)
3. **Google Jobs** (browser-first scraping)
4. **Glassdoor** (browser-first scraping, or third-party API)

Add feature flags to disable failing platforms gracefully.

---

## Conclusion

Both platforms are failing due to **brittle scraping implementations** that depend on specific, hardcoded patterns. The solution is to:

1. **Add comprehensive diagnostics** to understand exactly what's failing
2. **Implement multiple parsing strategies** to handle structural changes
3. **Switch to browser-first approach** for better resilience
4. **Fix Glassdoor location handling** which currently ignores user input
5. **Add monitoring and health checks** to detect future failures quickly

**Estimated Effort**: 2-3 weeks for full implementation, 3-5 days for minimal viable fix.

**Priority**: High - These are core features of the application.
