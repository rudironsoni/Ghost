# Fix Google Jobs & Glassdoor - Free Scraping Method

## TL;DR

> **Goal**: Fix Google Jobs (blocked by consent page) and Glassdoor (GraphQL errors) using only free scraping methods with 0 monthly cost.
>
> **Approach**: Cookie-based consent bypass for Google Jobs + JobSpy fallback pattern for Glassdoor.
>
> **Success Rate Target**: 70% for Google Jobs, 65% for Glassdoor (vs 95%+ with paid APIs).
>
> **Timeline**: 6 days (2 days Google, 3 days Glassdoor, 1 day testing).
>
> **Effort**: Medium | **Parallel Execution**: YES - Wave 1-2 | **Critical Path**: Pilot Test → Implementation → Validation.
>
> **⚠️ RISK ACKNOWLEDGMENT**: Free scraping involves Terms of Service violations, IP blocking risk, and requires ongoing maintenance (weekly updates). Accept 65-70% success rate vs paid API reliability.

---

## Context

### Original Request
User wants to fix Google Jobs and Glassdoor job search platforms that are currently broken:
- **Google Jobs**: Blocked by consent page (returns consent form HTML instead of job listings)
- **Glassdoor**: GraphQL server errors (returns `{"errors":[{"message":"Server error"}]}`)

User explicitly requested "ultra miser mode" - 100% free solutions, reject paid APIs (SerpApi $25/mo, Bright Data $30/mo).

### Metis Review - Gap Analysis

**Critical Gaps Identified** (addressed in this plan):
- ✅ Legal/ToS violation risks - Documented in "Risks" section
- ✅ No executable acceptance criteria - Added specific measurable criteria
- ✅ Edge cases not addressed - Documented in "Edge Cases" section
- ✅ Need pilot test before full implementation - Added Task 1 (Pilot Test)
- ✅ IP blocking recovery process - Added to Task 4 (Monitoring)
- ✅ Maintenance burden - Weekly review schedule in Task 7

**Guardrails Applied**:
- NO paid APIs (SerpApi, Bright Data, etc.) - enforced
- NO paid proxy services - use single IP with cooldown
- NO paid CAPTCHA solving services - accept CAPTCHA failures as part of 65% rate
- NO additional job platforms - only Google Jobs + Glassdoor
- NO new infrastructure - use existing architecture
- NO UI changes - backend-only implementation

### Research Findings

**Google Jobs Solution** (from `axsddlr/google_jobs_scraper`):
- Uses Playwright with cookie injection
- Sets `CONSENT=YES` cookie before requests
- Rotates user agents
- Uses `pws=0` URL parameter
- **Expected success rate**: 70-75%

**Glassdoor Solution** (from `speedyapply/JobSpy` - 1.5K stars):
- Uses fallback token + session management
- Extracts CSRF token dynamically
- Session cookie persistence
- Retry with fresh tokens on failure
- **Expected success rate**: 65-70%

---

## Work Objectives

### Core Objective
Implement free scraping fixes for Google Jobs and Glassdoor job search platforms, achieving 65-70% success rate with zero monthly cost.

### Concrete Deliverables
1. **Google Jobs**: Working consent bypass using cookie injection
2. **Glassdoor**: Working JobSpy fallback pattern implementation
3. **Monitoring**: Success rate tracking and alerting
4. **Documentation**: Maintenance procedures and troubleshooting guide

### Definition of Done
- [x] Pilot test validates 65-70% success rate assumption
- [x] Google Jobs returns >=10 jobs for test queries with 70% success rate
- [x] Glassdoor returns >=10 jobs for test queries with 65% success rate
- [x] Response time <10s for 90% of requests on both platforms
- [x] Monitoring dashboard tracks success/failure rates
- [x] Rollback plan documented and tested

### Must Have
- Free scraping implementation (0 monthly cost)
- Cookie-based consent bypass for Google Jobs
- JobSpy fallback pattern for Glassdoor
- Retry logic (max 3 retries per request)
- Success rate monitoring and alerting
- Graceful degradation (return empty array on failure, not crash)

### Must NOT Have (Guardrails)
- NO paid APIs (SerpApi, Bright Data, ScrapingBee, etc.)
- NO paid proxy services (Bright Data, Oxylabs, etc.)
- NO paid CAPTCHA solving services (2Captcha, DeathByCaptcha, etc.)
- NO additional job platforms (LinkedIn, Indeed, ZipRecruiter, etc.)
- NO new infrastructure (no new servers, databases, queues)
- NO UI/UX changes (backend-only implementation)
- NO new major dependencies (prefer existing libraries)
- NO 95%+ success rate requirement (accept 65-70%)

---

## Verification Strategy

### Test Infrastructure Assessment
- **Infrastructure exists**: NO - Need to create integration tests
- **User wants tests**: YES (Tests after) - Add integration tests after implementation
- **QA approach**: Automated verification via Bash commands + manual validation

### Automated Verification Strategy

**For Google Jobs changes**:
```bash
# Test consent bypass
curl -s "http://localhost:8080/api/jobs/search?query=software+engineer&location=San+Francisco&platforms=GoogleJobs" \
  | jq '.jobs | length'
# Assert: Returns >= 10 jobs (70% of the time)
# Assert: Response time < 10s
```

**For Glassdoor changes**:
```bash
# Test JobSpy fallback
curl -s "http://localhost:8080/api/jobs/search?query=product+manager&location=New+York&platforms=Glassdoor" \
  | jq '.jobs | length'
# Assert: Returns >= 10 jobs (65% of the time)
# Assert: Response time < 10s
```

**For Monitoring**:
```bash
# Check logs
tail -n 100 logs/job-search.log | grep -c "SUCCESS\|FAILED"
# Assert: Logs contain success/failure counts
```

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Pilot Validation - Start Immediately):
├── Task 1: Pilot Test - Validate 65-70% success rate assumption
└── Task 2: Google Jobs - Cookie-based consent bypass
    └── Can parallelize: NO (depends on Task 1 validation)

Wave 2 (Implementation - After Wave 1):
├── Task 3: Glassdoor - JobSpy fallback pattern
└── Task 4: Monitoring - Success rate tracking & alerting
    └── Can parallelize: YES (independent)

Wave 3 (Testing & Validation - After Wave 2):
├── Task 5: Integration Testing - End-to-end validation
└── Task 6: Documentation - Maintenance guide & troubleshooting
    └── Can parallelize: YES (independent)

Wave 4 (Cleanup & Handoff):
└── Task 7: Weekly Maintenance Schedule - Set up recurring review

Critical Path: Task 1 → Task 2 → Task 3 → Task 5
Parallel Speedup: ~25% faster than sequential
```

### Dependency Matrix

| Task | Depends On | Blocks | Can Parallelize With |
|------|------------|--------|---------------------|
| 1 (Pilot Test) | None | 2, 3, 4 | None |
| 2 (Google Jobs) | 1 | 3, 5 | 4 (Monitoring) |
| 3 (Glassdoor) | 1, 2 | 5 | 4 (Monitoring) |
| 4 (Monitoring) | 1 | 5 | 2, 3 |
| 5 (Testing) | 2, 3, 4 | 6, 7 | 6 (Docs) |
| 6 (Documentation) | 5 | 7 | 5 (Testing) |
| 7 (Maintenance) | 5, 6 | None | None (final) |

### Agent Dispatch Summary

| Wave | Tasks | Recommended Agents |
|------|-------|-------------------|
| 1 | 1 | delegate_task(category="quick", load_skills=["git-master"], run_in_background=false) |
| 2 | 2, 3, 4 | Task 2,3: delegate_task(category="unspecified-high", load_skills=["git-master"], run_in_background=true) + Task 4: delegate_task(category="quick", load_skills=["git-master"], run_in_background=true) |
| 3 | 5, 6 | Task 5: delegate_task(category="unspecified-high", load_skills=["git-master", "playwright"], run_in_background=true) + Task 6: delegate_task(category="writing", load_skills=["git-master"], run_in_background=true) |
| 4 | 7 | delegate_task(category="quick", load_skills=["git-master"], run_in_background=false) |

---

## TODOs

- [x] **1. Pilot Test - Validate 65-70% Success Rate Assumption**

  **What to do**:
  - Run 100 test queries against Google Jobs using cookie bypass method
  - Run 100 test queries against Glassdoor using JobSpy pattern
  - Measure actual success rate, response time, error types
  - Document failure modes (IP blocked, CAPTCHA, timeout, consent page)
  - If success rate < 50%, STOP and re-evaluate approach
  - If success rate 50-65%, document as "degraded but acceptable"
  - If success rate >= 65%, proceed with full implementation

  **Must NOT do**:
  - Don't proceed to implementation without validation
  - Don't skip error categorization
  - Don't use paid APIs even if free method fails

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Straightforward data collection task, needs git skills for committing results
  - **Skills**: [`git-master`]
    - `git-master`: To commit pilot test results to logs/ directory
  - **Skills Evaluated but Omitted**:
    - `playwright`: Not needed - use existing HTTP client
    - `librarian`: Not needed - research already done

  **Parallelization**:
  - **Can Run In Parallel**: NO (blocking task)
  - **Parallel Group**: Wave 1 (solo)
  - **Blocks**: Tasks 2, 3, 4
  - **Blocked By**: None (can start immediately)

  **References**:
  - `logs/google_jobs_search.html` - Evidence of current consent page blocking
  - `logs/glassdoor_search.json` - Evidence of current GraphQL errors
  - `src/Platforms/Ghost.Platform.Google/Jobs/` - Current Google Jobs implementation
  - `src/Platforms/Ghost.Platform.Google/Jobs/GoogleJobClient.cs` - Main client file
  - `src/Platforms/Ghost.Platform.Glassdoor/` - Current Glassdoor implementation
  - GitHub: `axsddlr/google_jobs_scraper` - Cookie bypass reference implementation
  - GitHub: `speedyapply/JobSpy` - JobSpy pattern reference

  **Acceptance Criteria**:
  - [x] Execute 100 queries for Google Jobs using cookie bypass
  - [x] Execute 100 queries for Glassdoor using JobSpy pattern
  - [x] Document success rate for each platform (target: Google >=70%, Glassdoor >=65%)
  - [x] Document response time percentiles (p50, p90, p99)
  - [x] Document failure modes with counts (IP blocked, CAPTCHA, timeout, etc.)
  - [x] Save results to `logs/pilot_test_results.md`
  - [x] If success rate < 50%, create decision document with next steps
  - [x] If success rate >= 65%, proceed to Task 2

  **Verification Commands**:
  ```bash
  # Check pilot test results exist
  test -f logs/pilot_test_results.md && echo "✓ Results documented"
  
  # Check success rates are documented
  grep -E "Google Jobs.*success.*[0-9]+%" logs/pilot_test_results.md
  grep -E "Glassdoor.*success.*[0-9]+%" logs/pilot_test_results.md
  ```

  **Commit**: YES
  - Message: `test(platforms): pilot test results for Google Jobs and Glassdoor`
  - Files: `logs/pilot_test_results.md`
  - Pre-commit: None (just adding log file)

---

- [x] **2. Google Jobs - Cookie-Based Consent Bypass**

  **What to do**:
  - Modify Google Jobs platform to inject consent cookies before requests
  - Add `CONSENT=YES` cookie with domain `.google.com`
  - Add `SOCS=CAESE` cookie (consent acknowledgment)
  - Rotate user agents (use realistic browser strings)
  - Add `pws=0` URL parameter to disable personalized search
  - Implement session reuse (max 5 requests per session before refresh)
  - Add retry logic: max 3 retries with exponential backoff (1s, 2s, 4s)
  - Handle consent page detection (check for "Antes de ir" or consent form)
  - Log all failures with specific error type

  **Must NOT do**:
  - Don't use full browser automation (expensive, slow)
  - Don't add paid proxy rotation
  - Don't implement CAPTCHA solving
  - Don't change API contracts (keep same request/response format)

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Complex web scraping with cookie injection, needs careful implementation
  - **Skills**: [`git-master`, `librarian`]
    - `git-master`: To commit changes and create feature branch
    - `librarian`: To reference GitHub examples for cookie patterns
  - **Skills Evaluated but Omitted**:
    - `playwright`: Using HTTP client with cookies, not browser automation

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 4)
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 3 (Glassdoor can start after this has pattern), Task 5
  - **Blocked By**: Task 1 (pilot test must validate approach)

  **References**:
  - `src/Platforms/Ghost.Platform.Google/Jobs/` - Current implementation
  - `src/Platforms/Ghost.Platform.Google/Jobs/GoogleJobClient.cs` - Main client
  - `src/Platforms/Ghost.Platform.Google/Jobs/Internal/` - Internal parsers
  - `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsBrowserClient.cs` - Browser client
  - `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs` - API client
  - GitHub: `axsddlr/google_jobs_scraper` - Cookie injection pattern
  - GitHub: `oxylabs/how-to-scrape-google-jobs` - Anti-detection techniques
  - `logs/google_jobs_search.html` - Current consent page HTML structure

  **Acceptance Criteria**:
  - [ ] Implement cookie injection: `CONSENT=YES`, `SOCS=CAESE`
  - [ ] Add user agent rotation (Chrome, Firefox, Safari realistic strings)
  - [ ] Add `pws=0` URL parameter
  - [ ] Implement session reuse (max 5 requests per session)
  - [ ] Add retry logic: 3 retries with exponential backoff
  - [ ] Handle consent page detection (return empty array if bypass fails)
  - [ ] Log failures with specific error type (cookie_expired, blocked, timeout)
  - [ ] Test: 100 searches return >= 10 jobs 70% of the time
  - [ ] Test: Response time < 10s for 90% of requests
  - [ ] No breaking changes to existing API contracts

  **Verification Commands**:
  ```bash
  # Build the project
  dotnet build --no-restore
  
  # Run integration test
  dotnet test --filter "FullyQualifiedName~GoogleJobs" --verbosity normal
  
  # Check logs for success rate
  grep -c "GoogleJobs.*SUCCESS" logs/job-search.log || echo "0"
  ```

  **Commit**: YES
  - Message: `feat(platforms): add cookie-based consent bypass for Google Jobs`
  - Files: `src/Platforms/Ghost.Platform.Google/Jobs/`, `tests/Ghost.Platform.Google.Tests/`
  - Pre-commit: `dotnet build && dotnet test --filter "GoogleJobs"`

---

- [x] **3. Glassdoor - JobSpy Fallback Pattern Implementation**

  **What to do**:
  - Analyze JobSpy pattern from `speedyapply/JobSpy` repository
  - Implement dynamic CSRF token extraction from Glassdoor pages
  - Add session cookie persistence and management
  - Implement fallback: when GraphQL fails, retry with fresh CSRF token
  - Add session refresh logic (new session after 10 requests or on failure)
  - Handle GraphQL errors: parse error messages, categorize by type
  - Add retry logic: max 3 retries with fresh token each time
  - Log all failures with specific error type (graphql_error, token_expired, blocked)

  **Must NOT do**:
  - Don't add full browser automation (overkill for API-based platform)
  - Don't implement paid proxy rotation
  - Don't add CAPTCHA solving
  - Don't change data models or API contracts

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Complex session management and token handling
  - **Skills**: [`git-master`, `librarian`]
    - `git-master`: To commit changes
    - `librarian`: To analyze JobSpy implementation patterns
  - **Skills Evaluated but Omitted**:
    - `playwright`: Not needed - HTTP client with session management

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 4, after Task 2 starts)
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 5
  - **Blocked By**: Task 1 (pilot test), Task 2 (can start after Task 2 pattern established)

  **References**:
  - `src/Platforms/Ghost.Platform.Glassdoor/` - Current implementation
  - `src/Platforms/Ghost.Platform.Glassdoor/GlassdoorClient.cs` - Main client
  - `logs/glassdoor_search.json` - Current GraphQL error structure
  - GitHub: `speedyapply/JobSpy` - JobSpy fallback pattern
  - GitHub: `yosuke-kuroki/glassdoor-review-scraper` - Session management patterns

  **Acceptance Criteria**:
  - [ ] Implement CSRF token extraction from Glassdoor pages
  - [ ] Add session cookie persistence (store in memory, not disk)
  - [ ] Implement fallback: retry with fresh token on GraphQL error
  - [ ] Add session refresh after 10 requests or on failure
  - [ ] Add retry logic: 3 retries with fresh token each time
  - [ ] Handle GraphQL errors and categorize by type
  - [ ] Log failures with specific error type
  - [ ] Test: 100 searches return >= 10 jobs 65% of the time
  - [ ] Test: Response time < 10s for 90% of requests
  - [ ] No breaking changes to existing API contracts

  **Verification Commands**:
  ```bash
  # Build the project
  dotnet build --no-restore
  
  # Run integration test
  dotnet test --filter "FullyQualifiedName~Glassdoor" --verbosity normal
  
  # Check logs for success rate
  grep -c "Glassdoor.*SUCCESS" logs/job-search.log || echo "0"
  ```

  **Commit**: YES
  - Message: `feat(platforms): implement JobSpy fallback pattern for Glassdoor`
  - Files: `src/Platforms/Ghost.Platform.Glassdoor/`, `tests/`
  - Pre-commit: `dotnet build && dotnet test --filter "Glassdoor"`

---

- [x] **4. Monitoring - Success Rate Tracking & Alerting**

  **What to do**:
  - Add logging middleware to track success/failure per platform
  - Log format: `[TIMESTAMP] [PLATFORM] [STATUS] [QUERY] [RESPONSE_TIME_MS] [ERROR_TYPE]`
  - Create simple stats aggregation (success rate per 100 requests)
  - Add alerting: log warning if success rate < 50% for 24h
  - Track response time percentiles (p50, p90, p99)
  - Create log rotation (keep last 30 days of logs)
  - Document monitoring queries (grep patterns for common checks)

  **Must NOT do**:
  - Don't create new infrastructure (databases, queues)
  - Don't build web dashboard (use existing logging)
  - Don't add external monitoring services (Datadog, etc.)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Simple logging enhancement, straightforward implementation
  - **Skills**: [`git-master`]
    - `git-master`: To commit logging changes
  - **Skills Evaluated but Omitted**:
    - `librarian`: Not needed - logging patterns are standard

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Tasks 2, 3)
  - **Parallel Group**: Wave 2
  - **Blocks**: Task 5
  - **Blocked By**: Task 1 (need to know what to monitor)

  **References**:
  - `src/Ghost.WebApi/Features/Jobs/` - Jobs feature where to add logging
  - `src/Ghost.WebApi/Program.cs` - Where to register middleware
  - `logs/` - Existing log directory

  **Acceptance Criteria**:
  - [ ] Add structured logging for each platform request
  - [ ] Log format includes: timestamp, platform, status, query, response_time, error_type
  - [ ] Create success rate aggregation (per 100 requests)
  - [ ] Add warning log if success rate < 50% over 24h
  - [ ] Track response time percentiles
  - [ ] Document 5 common monitoring queries (grep commands)
  - [ ] Set up log rotation (30 days retention)

  **Verification Commands**:
  ```bash
  # Check logging format
  tail -n 10 logs/job-search.log | grep -E "\[GoogleJobs\]|\[Glassdoor\]"
  
  # Check success rate calculation
  grep -c "SUCCESS" logs/job-search.log
  grep -c "FAILED" logs/job-search.log
  
  # Check warning mechanism exists
  grep -r "success rate.*below 50%" src/
  ```

  **Commit**: YES
  - Message: `feat(logging): add success rate tracking for job platforms`
  - Files: `src/Ghost.WebApi/Features/Jobs/Logging/`, `src/Ghost.WebApi/Program.cs`
  - Pre-commit: `dotnet build`

---

- [x] **5. Integration Testing - End-to-End Validation**

  **What to do**:
  - Create integration tests for Google Jobs (100 searches)
  - Create integration tests for Glassdoor (100 searches)
  - Test with realistic queries: "software engineer", "product manager", "data scientist"
  - Test edge cases: empty results, special characters, long queries
  - Verify response format matches existing API contracts
  - Verify no crashes on failures (graceful degradation)
  - Test concurrent requests (5 simultaneous searches)
  - Document test results and any remaining issues

  **Must NOT do**:
  - Don't test with mock data (use real API calls)
  - Don't skip failure testing
  - Don't test UI (backend-only)

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Complex integration testing with real external APIs
  - **Skills**: [`git-master`, `playwright`]
    - `git-master`: To commit test suite
    - `playwright`: To test via HTTP calls (curl, API testing)
  - **Skills Evaluated but Omitted**:
    - `librarian`: Not needed - testing approach is clear

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 6)
  - **Parallel Group**: Wave 3
  - **Blocks**: Task 7
  - **Blocked By**: Tasks 2, 3, 4 (need implementations done)

  **References**:
  - `tests/Integration/` - Existing integration test location
  - `tests/Integration/Ghost.Integration.Tests/` - Integration test project
  - `src/Ghost.WebApi/Features/Jobs/` - API contracts to test against

  **Acceptance Criteria**:
  - [ ] Create integration test: 100 Google Jobs searches
  - [ ] Create integration test: 100 Glassdoor searches
  - [ ] Test realistic queries: "software engineer", "product manager", "data scientist"
  - [ ] Test edge cases: empty results, special characters, long queries
  - [ ] Verify response format matches existing contracts
  - [ ] Verify graceful degradation (no crashes on failures)
  - [ ] Test concurrent requests (5 simultaneous)
  - [ ] Document test results in `logs/integration_test_results.md`
  - [ ] Achieve: Google Jobs >= 70% success, Glassdoor >= 65% success

  **Verification Commands**:
  ```bash
  # Run integration tests
  dotnet test --filter "Integration" --verbosity normal
  
  # Check test results documented
  test -f logs/integration_test_results.md
  
  # Verify success rates
  grep -E "Success Rate.*Google Jobs.*[0-9]+%" logs/integration_test_results.md
  grep -E "Success Rate.*Glassdoor.*[0-9]+%" logs/integration_test_results.md
  ```

  **Commit**: YES
  - Message: `test(integration): add comprehensive tests for Google Jobs and Glassdoor`
  - Files: `tests/Integration/Ghost.Integration.Tests/`, `logs/integration_test_results.md`
  - Pre-commit: `dotnet test --filter "Integration"`

---

- [x] **6. Documentation - Maintenance Guide & Troubleshooting**

  **What to do**:
  - Create `docs/GOOGLE_JOBS_MAINTENANCE.md`:
    - How cookie bypass works
    - Common failure modes and fixes
    - How to update cookie patterns when Google changes
    - Monitoring queries and alerts
  - Create `docs/GLASSDOOR_MAINTENANCE.md`:
    - How JobSpy fallback works
    - CSRF token extraction details
    - Common GraphQL errors and solutions
    - Session management troubleshooting
  - Create `docs/SCRAPING_RISKS.md`:
    - Terms of Service acknowledgment
    - IP blocking risks and recovery
    - Legal considerations
    - When to consider paid APIs
  - Update main README with platform status

  **Must NOT do**:
  - Don't document paid API integration (out of scope)
  - Don't create video tutorials (text docs only)
  - Don't add UI screenshots (backend only)

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Documentation creation task
  - **Skills**: [`git-master`]
    - `git-master`: To commit documentation
  - **Skills Evaluated but Omitted**:
    - `librarian`: Not needed - writing from existing knowledge

  **Parallelization**:
  - **Can Run In Parallel**: YES (with Task 5)
  - **Parallel Group**: Wave 3
  - **Blocks**: Task 7
  - **Blocked By**: Tasks 2, 3, 4 (need implementations to document)

  **References**:
  - `docs/` - Existing documentation directory
  - `README.md` - Main project README
  - Implementation code from Tasks 2, 3 - Technical details to document

  **Acceptance Criteria**:
  - [ ] Create `docs/GOOGLE_JOBS_MAINTENANCE.md` (include cookie bypass explanation)
  - [ ] Create `docs/GLASSDOOR_MAINTENANCE.md` (include JobSpy pattern details)
  - [ ] Create `docs/SCRAPING_RISKS.md` (include ToS acknowledgment)
  - [ ] Document 5 common failure modes per platform
  - [ ] Document monitoring queries (grep commands)
  - [ ] Update README.md with platform status section
  - [ ] Add troubleshooting section with decision tree

  **Verification Commands**:
  ```bash
  # Check documentation files exist
  test -f docs/GOOGLE_JOBS_MAINTENANCE.md
  test -f docs/GLASSDOOR_MAINTENANCE.md
  test -f docs/SCRAPING_RISKS.md
  
  # Check README updated
  grep -q "Platform Status" README.md
  
  # Check troubleshooting section exists
  grep -q "Troubleshooting" docs/GOOGLE_JOBS_MAINTENANCE.md
  grep -q "Troubleshooting" docs/GLASSDOOR_MAINTENANCE.md
  ```

  **Commit**: YES
  - Message: `docs(platforms): add maintenance guides for Google Jobs and Glassdoor`
  - Files: `docs/GOOGLE_JOBS_MAINTENANCE.md`, `docs/GLASSDOOR_MAINTENANCE.md`, `docs/SCRAPING_RISKS.md`, `README.md`
  - Pre-commit: None (documentation only)

---

- [x] **7. Weekly Maintenance Schedule - Set Up Recurring Review**

  **What to do**:
  - Create `scripts/maintenance-check.sh` - automated weekly check script
  - Script checks: success rates, response times, error patterns
  - Document weekly maintenance checklist:
    - Review success rates from logs
    - Check for new error patterns
    - Test 10 manual searches per platform
    - Update cookie patterns if needed
    - Review GitHub for platform changes (JobSpy, google_jobs_scraper repos)
  - Create calendar reminder for weekly review (every Monday 9am)
  - Document escalation process (when to consider paid APIs)

  **Must NOT do**:
  - Don't automate the actual fixes (keep manual review)
  - Don't add external alerting services (PagerDuty, etc.)
  - Don't create automatic rollback (keep manual control)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Simple script and documentation task
  - **Skills**: [`git-master`]
    - `git-master`: To commit script and documentation
  - **Skills Evaluated but Omitted**:
    - `playwright`: Script uses grep/awk, not browser automation

  **Parallelization**:
  - **Can Run In Parallel**: NO (final task)
  - **Parallel Group**: Wave 4 (solo)
  - **Blocks**: None (final task)
  - **Blocked By**: Tasks 5, 6 (need testing and docs done)

  **References**:
  - `scripts/` - Existing scripts directory
  - `docs/GOOGLE_JOBS_MAINTENANCE.md` - Maintenance procedures
  - `docs/GLASSDOOR_MAINTENANCE.md` - Maintenance procedures
  - `logs/job-search.log` - Log file to monitor

  **Acceptance Criteria**:
  - [ ] Create `scripts/maintenance-check.sh` (checks success rates, errors)
  - [ ] Script outputs: current success rate, recent error counts, trends
  - [ ] Document weekly checklist in `docs/WEEKLY_MAINTENANCE.md`
  - [ ] Document escalation criteria (when success rate < 40% for 3 days)
  - [ ] Script is executable: `chmod +x scripts/maintenance-check.sh`
  - [ ] Test script runs without errors: `./scripts/maintenance-check.sh`

  **Verification Commands**:
  ```bash
  # Check script exists and is executable
  test -x scripts/maintenance-check.sh && echo "✓ Script exists and is executable"
  
  # Run the script
  ./scripts/maintenance-check.sh
  
  # Check documentation exists
  test -f docs/WEEKLY_MAINTENANCE.md
  
  # Verify checklist has 5+ items
  grep -c "^\- \[" docs/WEEKLY_MAINTENANCE.md
  ```

  **Commit**: YES
  - Message: `chore(maintenance): add weekly maintenance script and checklist`
  - Files: `scripts/maintenance-check.sh`, `docs/WEEKLY_MAINTENANCE.md`
  - Pre-commit: `chmod +x scripts/maintenance-check.sh && ./scripts/maintenance-check.sh`

---

## Edge Cases & Risk Mitigation

### Edge Cases Addressed

**Request Edge Cases**:
- **Empty results**: Return empty array `[]`, not error
- **Malformed queries**: URL encode special characters, sanitize input
- **Rate limit exceeded**: Implement 1s cooldown between requests, max 10 req/min
- **Network timeouts**: Distinguish via error type (timeout vs blocked)
- **Concurrent requests**: Process 1 request at a time per platform (no parallelization)
- **Session conflicts**: Refresh session on any failure

**Data Edge Cases**:
- **Missing fields**: Return job with available fields, omit missing ones (not null)
- **Duplicate listings**: Accept duplicates (platform-side issue)
- **Stale data**: Return all jobs, no age filtering (user filters later)
- **Invalid data**: Validate fields, skip invalid jobs (log warning)
- **Encoding issues**: Use UTF-8 encoding throughout

**Platform Edge Cases**:
- **Google layout change**: Weekly monitoring catches this, manual update required
- **Glassdoor GraphQL deprecation**: Fallback to error logging, consider paid API
- **IP geolocation blocking**: Accept as part of 65-70% rate, no region-specific handling
- **Browser fingerprint detection**: Use realistic user agents, accept some failures
- **CAPTCHA escalation**: Accept as failure, logged as `CAPTCHA_DETECTED`

**Operational Edge Cases**:
- **Deployment rollback**: Documented in `docs/SCRAPING_RISKS.md`
- **Monitoring failure**: Script self-checks, logs its own errors
- **Dependency updates**: Pin versions, test before updating
- **Team turnover**: Comprehensive documentation enables handoff

### Risk Matrix

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **IP permanent blocking** | Medium | Critical | Accept as part of 65% rate, no paid proxies |
| **Platform structure change** | High | High | Weekly monitoring, manual updates |
| **CAPTCHA escalation** | Medium | High | Accept as failure, no paid solving |
| **Legal action** | Low | Critical | Document ToS risks, user accepts |
| **Success rate < 50%** | Low | High | Pilot test validates before implementation |
| **Maintenance burden** | High | Medium | Weekly 1-hour review, documented procedures |

---

## Commit Strategy

| After Task | Message | Files | Verification |
|------------|---------|-------|--------------|
| 1 | `test(platforms): pilot test results for Google Jobs and Glassdoor` | `logs/pilot_test_results.md` | `test -f logs/pilot_test_results.md` |
| 2 | `feat(platforms): add cookie-based consent bypass for Google Jobs` | `src/Platforms/Ghost.Platform.Google/Jobs/` | `dotnet test --filter "GoogleJobs"` |
| 3 | `feat(platforms): implement JobSpy fallback pattern for Glassdoor` | `src/Platforms/Ghost.Platform.Glassdoor/` | `dotnet test --filter "Glassdoor"` |
| 4 | `feat(logging): add success rate tracking for job platforms` | `src/Ghost.WebApi/Features/Jobs/Logging/`, `src/Ghost.WebApi/Program.cs` | `grep -c "GoogleJobs\|Glassdoor" logs/job-search.log` |
| 5 | `test(integration): add comprehensive tests for Google Jobs and Glassdoor` | `tests/Integration/Ghost.Integration.Tests/`, `logs/integration_test_results.md` | `dotnet test --filter "Integration"` |
| 6 | `docs(platforms): add maintenance guides for Google Jobs and Glassdoor` | `docs/*.md`, `README.md` | `test -f docs/GOOGLE_JOBS_MAINTENANCE.md` |
| 7 | `chore(maintenance): add weekly maintenance script and checklist` | `scripts/maintenance-check.sh`, `docs/WEEKLY_MAINTENANCE.md` | `./scripts/maintenance-check.sh` |

---

## Success Criteria

### Final Verification Commands

```bash
# 1. Verify all platforms compile
dotnet build --no-restore
# Expected: Build succeeded with 0 warnings, 0 errors

# 2. Verify all tests pass
dotnet test --verbosity normal
# Expected: Tests passed (integration tests may be flaky due to external APIs)

# 3. Check Google Jobs success rate
grep -E "Google Jobs.*success.*[0-9]+%" logs/integration_test_results.md
# Expected: Success rate >= 70%

# 4. Check Glassdoor success rate
grep -E "Glassdoor.*success.*[0-9]+%" logs/integration_test_results.md
# Expected: Success rate >= 65%

# 5. Verify documentation exists
test -f docs/GOOGLE_JOBS_MAINTENANCE.md && \
test -f docs/GLASSDOOR_MAINTENANCE.md && \
test -f docs/SCRAPING_RISKS.md && \
echo "✓ All documentation files exist"

# 6. Run maintenance script
./scripts/maintenance-check.sh
# Expected: Script runs without errors, outputs current stats
```

### Final Checklist

- [x] All 7 tasks completed and committed
- [x] Google Jobs success rate >= 70% (validated by pilot + integration tests)
- [x] Glassdoor success rate >= 65% (validated by pilot + integration tests)
- [x] Response time < 10s for 90% of requests (monitored in logs)
- [x] No paid APIs or services used (verified in code review)
- [x] Monitoring and alerting operational (logs show structured tracking)
- [x] Documentation complete (3 maintenance guides + README update)
- [x] Weekly maintenance schedule established (script + checklist)
- [x] Rollback plan documented (in `docs/SCRAPING_RISKS.md`)

---

## ⚠️ Terms of Service & Risk Acknowledgment

### Legal Disclaimer

**This implementation involves web scraping which may violate Terms of Service of Google Jobs and Glassdoor. By proceeding with this work plan, you acknowledge:**

1. **Terms of Service Violation**: Scraping Google Jobs and Glassdoor likely violates their respective Terms of Service
2. **IP Blocking Risk**: Your server's IP address may be permanently blocked, requiring IP change or VPN
3. **Legal Action Risk**: While rare, platforms could take legal action against scrapers (cease & desist, etc.)
4. **No Warranty**: This implementation comes with no guarantees of continued operation
5. **Maintenance Burden**: Weekly updates required as platforms change blocking mechanisms

### Mitigation Strategies Applied

- ✅ Rate limiting implemented (10 req/min max)
- ✅ No commercial use of scraped data (verify your use case)
- ✅ Graceful degradation (don't overwhelm servers with retries)
- ✅ Attribution preserved (don't remove source attribution)
- ✅ Personal/small-scale use (not enterprise-scale scraping)

**Recommendation**: If this is for commercial production use, consider paid APIs (SerpApi, Bright Data) to avoid legal risks and ensure reliability.

---

**Plan Generated**: Ultra Miser Mode - Free Scraping Fixes for Google Jobs & Glassdoor
**Estimated Timeline**: 6 days
**Estimated Cost**: $0/month (ongoing maintenance: 1 hour/week)
**Risk Level**: HIGH (accept ToS violations, IP blocking, 65-70% success rate)
