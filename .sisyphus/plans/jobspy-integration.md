# JobSpy Integration and Platform Enhancement Plan

## TL;DR

> **Quick Summary**: Integrate JobSpy's proven scraping patterns to fix Ghost's broken Glassdoor, Google Jobs, and Indeed implementations while adding European job platform support, focusing on Spanish markets.
> 
> **Deliverables**: 
> - Robust session management with proxy rotation and TLS fingerprinting
> - Fixed implementations for Glassdoor, Google Jobs, and Indeed
> - Support for tech-focused European platforms (InfoJobs, Tecnoempleo)
> - Comprehensive test coverage with mocked HTTP tests
> 
> **Estimated Effort**: Large
> **Parallel Execution**: YES - 3 waves (Foundation → Platform Fixes → EU Expansion)
> **Critical Path**: Session Patterns → Glassdoor Fix → Indeed Fix → Google Fix → EU Platforms

---

## Context

### Original Request
Analyze JobSpy https://github.com/speedyapply/JobSpy/tree/main/jobspy and use its rationale and implementation logic to enhance Ghost's @src/Platforms/Ghost.Platform.Glassdoor/, @src/Platforms/Ghost.Platform.Google/Jobs/, and @src/Platforms/Ghost.Platform.Indeed/ implementations. Focus on fixing flaky/broken scrapers and adding European job platform support.

### Interview Summary
**Key Discussions**:
- Current implementations suffer from brittle reverse-engineering approaches
- Glassdoor: CSRF token extraction failures, incomplete GraphQL payloads
- Google Jobs: Fragile JSON discovery, hard-coded bootstrap strings
- Indeed: Parser/test mismatch, broken pagination, security risks
- Need tech-focused European job market focus (Spain priority)

**Research Findings**:
- JobSpy has production-ready framework with proven patterns
- Rotating proxy sessions, TLS fingerprinting, concurrent execution
- Comprehensive error handling and logging strategies
- Support for multiple regions and platforms

### Metis Review
**Identified Gaps** (addressed):
- **Legal compliance**: Added explicit guardrails for platform terms of service compliance
- **Performance monitoring**: Added success rate and response time metrics
- **Risk mitigation**: Added fallback strategies and monitoring recommendations
- **Scope boundaries**: Explicitly defined EU platform priorities

---

## Work Objectives

### Core Objective
Transform Ghost's job scraping capabilities from brittle implementations to production-ready, scalable solutions leveraging JobSpy's proven patterns while expanding into strategic European markets.

### Concrete Deliverables
- Enhanced session management infrastructure
- Fixed Glassdoor GraphQL client with proper CSRF handling
- Fixed Google Jobs scraper with robust JSON extraction
- Fixed Indeed API client with working pagination
- New tech-focused European platform implementations (InfoJobs, Tecnoempleo)
- Comprehensive test suite with mocked HTTP tests

### Definition of Done
- [ ] All platforms return >90% success rate in test environment
- [ ] Comprehensive test coverage (>80% for core components)
- [ ] European platform support validated with Spanish job data
- [ ] Performance metrics show <5 second response times

### Must Have
- Robust error handling and retry mechanisms
- Proxy rotation and TLS fingerprinting capabilities
- Comprehensive test coverage
- Tech-focused European platform support (Spain focus)

### Must NOT Have (Guardrails)
- No hard-coded API keys or secrets
- No brittle regex-based parsing without fallbacks
- No infinite loops or unhandled exceptions
- No legal compliance violations

---

## Verification Strategy

### Test Decision
- **Infrastructure exists**: YES
- **User wants tests**: TDD (Recommended)
- **Framework**: xUnit with mocked HttpMessageHandler

### TDD Approach
Each TODO follows RED-GREEN-REFACTOR:

**Task Structure:**
1. **RED**: Write failing test first
   - Test file: `tests/Platforms/{Platform}.Tests/{Component}Tests.cs`
   - Test command: `dotnet test tests/Platforms/{Platform}.Tests/`
   - Expected: FAIL (test exists, implementation doesn't)
2. **GREEN**: Implement minimum code to pass
   - Command: `dotnet test tests/Platforms/{Platform}.Tests/`
   - Expected: PASS
3. **REFACTOR**: Clean up while keeping green
   - Command: `dotnet test tests/Platforms/{Platform}.Tests/`
   - Expected: PASS (still)

**Automated Verification (Agent-Executable):**

**For API/HTTP changes** (using Bash curl):
```bash
# Agent runs:
curl -s -X GET http://localhost:8080/api/jobs/search?platform=glassdoor\&query=developer
# Assert: Returns JSON with job listings
# Assert: HTTP status 200
# Assert: Response contains valid job data structure
```

**For Library/Module changes** (using Bash dotnet):
```bash
# Agent runs:
dotnet test tests/Platforms/Ghost.Platform.Glassdoor.Tests/
# Assert: All tests pass
# Assert: Output contains "Passed: X, Failed: 0"
```

**Evidence to Capture:**
- [ ] Terminal output from test commands (actual test results)
- [ ] JSON response bodies for API verification
- [ ] Screenshots of test execution results

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately - Foundation):
├── Task 1: Implement JobSpy session patterns
└── Task 2: Add comprehensive test infrastructure

Wave 2 (After Wave 1 - Platform Fixes):
├── Task 3: Fix Glassdoor implementation
├── Task 4: Fix Indeed implementation
└── Task 5: Fix Google Jobs implementation

Wave 3 (After Wave 2 - EU Expansion):
├── Task 6: Add InfoJobs support
├── Task 7: Add Tecnoempleo support
└── Task 8: Performance optimization and monitoring

Critical Path: Task 1 → Task 3 → Task 4 → Task 5 → Task 6
```

### Dependency Matrix

| Task | Depends On | Blocks | Can Parallelize With |
|------|------------|--------|---------------------|
| 1 | None | 3, 4, 5 | 2 |
| 2 | None | 3, 4, 5 | 1 |
| 3 | 1, 2 | 6, 7 | 4, 5 |
| 4 | 1, 2 | 6, 7 | 3, 5 |
| 5 | 1, 2 | 6, 7 | 3, 4 |
| 6 | 3, 4, 5 | 8 | 7 |
| 7 | 3, 4, 5 | 8 | 6 |
| 8 | 6, 7 | None | None |

### Agent Dispatch Summary

| Wave | Tasks | Recommended Agents |
|------|-------|-------------------|
| 1 | 1, 2 | delegate_task(category="ultrabrain", load_skills=["git-master"], run_in_background=true) |
| 2 | 3, 4, 5 | delegate_task(category="visual-engineering", load_skills=["frontend-ui-ux"], run_in_background=true) |
| 3 | 6, 7, 8 | delegate_task(category="writing", load_skills=["dev-browser"], run_in_background=true) |

---

## TODOs

- [x] 1. Implement JobSpy Session Patterns

  **What to do**:
  - Create RotatingProxySession implementation with proxy rotation
  - Implement TLS fingerprinting using tls_client patterns
  - Add exponential backoff retry mechanisms
  - Create session factory for consistent session creation

  **Must NOT do**:
  - Hard-code proxy endpoints
  - Skip error handling for network failures
  - Create sessions without proper cleanup

  **Recommended Agent Profile**:
  - **Category**: `ultrabrain`
    - Reason: Requires deep understanding of HTTP protocols and security patterns
  - **Skills**: [`git-master`, `dev-browser`]
    - `git-master`: For atomic commits of complex session logic
    - `dev-browser`: For understanding browser-like TLS patterns

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Task 2)
  - **Blocks**: Tasks 3, 4, 5
  - **Blocked By**: None (can start immediately)

  **References**:
  - **Pattern References**: JobSpy `/jobspy/util.py:RotatingProxySession` - Proxy rotation and session management
  - **API/Type References**: `src/Platforms/Ghost.Platform.Glassdoor/GlassdoorApiClient.cs` - Current session usage patterns
  - **Test References**: `tests/Platforms/Ghost.Platform.Glassdoor.Tests/` - Existing test patterns to extend
  - **External References**: JobSpy documentation on TLS fingerprinting and proxy rotation

  **Acceptance Criteria**:
  - [ ] Session factory creates sessions with proxy rotation
  - [ ] TLS fingerprinting implemented and tested
  - [ ] Retry mechanism handles 429/5xx responses with exponential backoff
  - [ ] Unit tests cover session creation and error scenarios

  **Automated Verification**:
  ```bash
  # Agent executes:
  dotnet test tests/Platforms/Ghost.Platform.Common.Tests/SessionTests.cs
  # Assert: All session tests pass
  # Assert: Output contains "Passed: X, Failed: 0"
  ```

  **Evidence to Capture**:
  - [ ] Terminal output from test execution
  - [ ] Session creation logs showing proxy rotation

  **Commit**: YES
  - Message: `feat(session): implement JobSpy session patterns with proxy rotation`
  - Files: `src/Platforms/Ghost.Platform.Common/Session/`
  - Pre-commit: `dotnet test tests/Platforms/Ghost.Platform.Common.Tests/`

- [x] 2. Add Comprehensive Test Infrastructure

  **What to do**:
  - Create HttpMessageHandler mock framework for API testing
  - Add test fixtures with recorded platform responses
  - Implement test base classes for consistent testing patterns
  - Add integration test setup for end-to-end validation

  **Must NOT do**:
  - Use live API calls in unit tests
  - Skip error scenario testing
  - Create tests without proper isolation

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Requires careful documentation and test case design
  - **Skills**: [`git-master`]
    - `git-master`: For organizing test files and fixtures

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Task 1)
  - **Blocks**: Tasks 3, 4, 5
  - **Blocked By**: None (can start immediately)

  **References**:
  - **Pattern References**: Existing test patterns in `tests/Platforms/Ghost.Platform.Glassdoor.Tests/`
  - **API/Type References**: Microsoft's HttpMessageHandler mocking patterns
  - **Test References**: JobSpy test structure for inspiration

  **Acceptance Criteria**:
  - [ ] Mock framework handles HTTP requests/responses
  - [ ] Test fixtures include success and failure scenarios
  - [ ] Base test classes provide consistent setup/teardown
  - [ ] Integration tests validate end-to-end flows

  **Automated Verification**:
  ```bash
  # Agent executes:
  dotnet test tests/Platforms/Ghost.Platform.Common.Tests/
  # Assert: All infrastructure tests pass
  # Assert: Mock framework correctly intercepts HTTP calls
  ```

  **Evidence to Capture**:
  - [ ] Test execution results
  - [ ] Mock framework validation output

  **Commit**: YES
  - Message: `test(infrastructure): add comprehensive HTTP mocking framework`
  - Files: `tests/Platforms/Ghost.Platform.Common.Tests/`
  - Pre-commit: `dotnet test tests/Platforms/Ghost.Platform.Common.Tests/`

- [ ] 3. Fix Glassdoor Implementation

  **What to do**:
  - Fix CSRF token extraction with multiple pattern matching
  - Complete GraphQL payload with proper persisted queries
  - Add consent handling and anti-bot measure detection
  - Implement proper error detection and retry logic

  **Must NOT do**:
  - Use brittle regex patterns without fallbacks
  - Skip consent page handling
  - Log sensitive API keys

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: Requires understanding of web authentication flows
  - **Skills**: [`dev-browser`, `frontend-ui-ux`]
    - `dev-browser`: For understanding browser authentication patterns
    - `frontend-ui-ux`: For GraphQL payload structure design

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 4, 5)
  - **Blocks**: Tasks 6, 7
  - **Blocked By**: Tasks 1, 2

  **References**:
  - **Pattern References**: JobSpy `/jobspy/glassdoor/` - Glassdoor scraping patterns
  - **API/Type References**: `src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs` - Current implementation
  - **Test References**: `tests/Platforms/Ghost.Platform.Glassdoor.Tests/` - Extend existing tests

  **Acceptance Criteria**:
  - [ ] CSRF token extraction works with multiple patterns
  - [ ] GraphQL requests succeed with proper payload
  - [ ] Consent pages are handled gracefully
  - [ ] Success rate >90% in test environment

  **Automated Verification**:
  ```bash
  # Agent executes:
  dotnet test tests/Platforms/Ghost.Platform.Glassdoor.Tests/
  # Assert: All Glassdoor tests pass
  # Assert: Mocked responses are properly parsed
  ```

  **Evidence to Capture**:
  - [ ] Test execution results
  - [ ] GraphQL request/response validation

  **Commit**: YES
  - Message: `fix(glassdoor): implement robust CSRF and GraphQL handling`
  - Files: `src/Platforms/Ghost.Platform.Glassdoor/`
  - Pre-commit: `dotnet test tests/Platforms/Ghost.Platform.Glassdoor.Tests/`

- [ ] 4. Fix Indeed Implementation

  **What to do**:
  - Fix parser/test mismatch (support both JSON shapes)
  - Implement proper cursor-based pagination
  - Remove hard-coded API key and add configuration
  - Add comprehensive retry and backoff mechanisms

  **Must NOT do**:
  - Log API keys in debug output
  - Skip pagination implementation
  - Use fixed delays for rate limiting

  **Recommended Agent Profile**:
  - **Category**: `ultrabrain`
    - Reason: Requires complex data structure handling
  - **Skills**: [`git-master`]
    - `git-master`: For careful API key handling and security

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 3, 5)
  - **Blocks**: Tasks 6, 7
  - **Blocked By**: Tasks 1, 2

  **References**:
  - **Pattern References**: JobSpy `/jobspy/indeed/` - Indeed scraping patterns
  - **API/Type References**: `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs` - Current implementation
  - **Test References**: `tests/Platforms/Ghost.Platform.Indeed.Tests/` - Fix existing tests

  **Acceptance Criteria**:
  - [ ] Parser handles both JSON shapes correctly
  - [ ] Pagination works with cursor-based requests
  - [ ] API keys are properly configured and secured
  - [ ] Retry mechanisms handle rate limiting gracefully

  **Automated Verification**:
  ```bash
  # Agent executes:
  dotnet test tests/Platforms/Ghost.Platform.Indeed.Tests/
  # Assert: All Indeed tests pass
  # Assert: Pagination produces unique results
  ```

  **Evidence to Capture**:
  - [ ] Test execution results
  - [ ] Pagination validation output

  **Commit**: YES
  - Message: `fix(indeed): implement proper pagination and API key security`
  - Files: `src/Platforms/Ghost.Platform.Indeed/`
  - Pre-commit: `dotnet test tests/Platforms/Ghost.Platform.Indeed.Tests/`

- [ ] 5. Fix Google Jobs Implementation

  **What to do**:
  - Implement robust JSON extraction with XSSI prefix handling
  - Make AsyncBootstrapString configurable
  - Improve consent detection and handling
  - Add cookie management for session persistence

  **Must NOT do**:
  - Use hard-coded widget keys
  - Skip consent page handling
  - Create fragile index-based field extraction

  **Recommended Agent Profile**:
  - **Category**: `visual-engineering`
    - Reason: Requires understanding of Google's JS-heavy interfaces
  - **Skills**: [`dev-browser`, `frontend-ui-ux`]
    - `dev-browser`: For understanding Google's consent flows
    - `frontend-ui-ux`: For JSON extraction pattern design

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 3, 4)
  - **Blocks**: Tasks 6, 7
  - **Blocked By**: Tasks 1, 2

  **References**:
  - **Pattern References**: JobSpy `/jobspy/google/` - Google Jobs scraping patterns
  - **API/Type References**: `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs` - Current implementation
  - **Test References**: `tests/Platforms/Ghost.Platform.Google.Tests/` - Extend existing tests

  **Acceptance Criteria**:
  - [ ] JSON extraction handles XSSI prefixes and JS wrappers
  - [ ] Bootstrap string is configurable and updatable
  - [ ] Consent pages are properly detected and handled
  - [ ] Session cookies persist across requests

  **Automated Verification**:
  ```bash
  # Agent executes:
  dotnet test tests/Platforms/Ghost.Platform.Google.Tests/
  # Assert: All Google Jobs tests pass
  # Assert: Consent handling works correctly
  ```

  **Evidence to Capture**:
  - [ ] Test execution results
  - [ ] Consent handling validation

  **Commit**: YES
  - Message: `fix(google-jobs): implement robust JSON extraction and consent handling`
  - Files: `src/Platforms/Ghost.Platform.Google/Jobs/`
  - Pre-commit: `dotnet test tests/Platforms/Ghost.Platform.Google.Tests/`

- [ ] 6. Add InfoJobs Support (Spain)

  **What to do**:
  - Research InfoJobs API structure and authentication
  - Implement InfoJobs scraper using JobSpy patterns
  - Add Spanish locale handling and salary parsing
  - Create comprehensive test coverage

  **Must NOT do**:
  - Use brittle scraping without proper error handling
  - Skip Spanish language and currency handling
  - Create implementation without legal compliance checks

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Requires research and documentation of Spanish platform
  - **Skills**: [`dev-browser`]
    - `dev-browser`: For understanding Spanish web interfaces

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Task 7)
  - **Blocks**: Task 8
  - **Blocked By**: Tasks 3, 4, 5

  **References**:
  - **Pattern References**: JobSpy platform implementations for inspiration
  - **API/Type References**: InfoJobs official documentation (research required)
  - **Test References**: Existing platform test patterns

  **Acceptance Criteria**:
  - [ ] InfoJobs scraper returns Spanish job listings
  - [ ] Spanish salary parsing handles EUR currency
  - [ ] Success rate >90% for Spanish job searches
  - [ ] Legal compliance verified

  **Automated Verification**:
  ```bash
  # Agent executes:
  dotnet test tests/Platforms/Ghost.Platform.InfoJobs.Tests/
  # Assert: All InfoJobs tests pass
  # Assert: Spanish job data is properly parsed
  ```

  **Evidence to Capture**:
  - [ ] Test execution results
  - [ ] Spanish job data validation

  **Commit**: YES
  - Message: `feat(infojobs): add Spanish job platform support`
  - Files: `src/Platforms/Ghost.Platform.InfoJobs/`
  - Pre-commit: `dotnet test tests/Platforms/Ghost.Platform.InfoJobs.Tests/`

- [ ] 7. Add Tecnoempleo Support (Spain)

  **What to do**:
  - Research Tecnoempleo platform structure
  - Implement technology-focused job scraping
  - Add specialized parsing for tech job fields
  - Create comprehensive test coverage

  **Must NOT do**:
  - Use generic scraping without tech field specialization
  - Skip Spanish technology terminology handling
  - Create implementation without proper error handling

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Requires research of Spanish tech job market
  - **Skills**: [`dev-browser`]
    - `dev-browser`: For understanding Spanish tech platforms

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Task 6)
  - **Blocks**: Task 8
  - **Blocked By**: Tasks 3, 4, 5

  **References**:
  - **Pattern References**: JobSpy technology-focused platform patterns
  - **API/Type References**: Tecnoempleo platform documentation (research required)
  - **Test References**: Existing platform test patterns

  **Acceptance Criteria**:
  - [ ] Tecnoempleo scraper returns Spanish tech job listings
  - [ ] Technology field parsing handles specialized terminology
  - [ ] Success rate >90% for Spanish tech job searches
  - [ ] Legal compliance verified

  **Automated Verification**:
  ```bash
  # Agent executes:
  dotnet test tests/Platforms/Ghost.Platform.Tecnoempleo.Tests/
  # Assert: All Tecnoempleo tests pass
  # Assert: Spanish tech job data is properly parsed
  ```

  **Evidence to Capture**:
  - [ ] Test execution results
  - [ ] Spanish tech job data validation

  **Commit**: YES
  - Message: `feat(tecnoempleo): add Spanish tech job platform support`
  - Files: `src/Platforms/Ghost.Platform.Tecnoempleo/`
  - Pre-commit: `dotnet test tests/Platforms/Ghost.Platform.Tecnoempleo.Tests/`

- [ ] 8. Performance Optimization and Monitoring

  **What to do**:
  - Implement performance metrics collection
  - Add success rate monitoring per platform
  - Optimize concurrent execution patterns
  - Create dashboard for platform performance

  **Must NOT do**:
  - Add monitoring without proper performance baselines
  - Skip error rate tracking
  - Create monitoring that impacts performance

  **Recommended Agent Profile**:
  - **Category**: `ultrabrain`
    - Reason: Requires performance analysis and optimization
  - **Skills**: [`git-master`]
    - `git-master`: For performance optimization commits

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3 (sequential)
  - **Blocks**: None
  - **Blocked By**: Tasks 6, 7

  **References**:
  - **Pattern References**: JobSpy performance monitoring patterns
  - **API/Type References**: Existing Ghost performance metrics
  - **Test References**: Performance testing patterns

  **Acceptance Criteria**:
  - [ ] Performance metrics collected for all platforms
  - [ ] Success rates monitored and reported
  - [ ] Response times optimized to <5 seconds
  - [ ] Performance dashboard operational

  **Automated Verification**:
  ```bash
  # Agent executes:
  dotnet test tests/Performance/Ghost.Platform.Performance.Tests/
  # Assert: Performance tests pass within thresholds
  # Assert: Monitoring metrics are properly collected
  ```

  **Evidence to Capture**:
  - [ ] Performance test results
  - [ ] Monitoring dashboard screenshots

  **Commit**: YES
  - Message: `feat(monitoring): add platform performance monitoring`
  - Files: `src/Platforms/Ghost.Platform.Common/Monitoring/`
  - Pre-commit: `dotnet test tests/Performance/Ghost.Platform.Performance.Tests/`

---

## Commit Strategy

| After Task | Message | Files | Verification |
|------------|---------|-------|--------------|
| 1 | `feat(session): implement JobSpy session patterns with proxy rotation` | `src/Platforms/Ghost.Platform.Common/Session/` | `dotnet test tests/Platforms/Ghost.Platform.Common.Tests/` |
| 2 | `test(infrastructure): add comprehensive HTTP mocking framework` | `tests/Platforms/Ghost.Platform.Common.Tests/` | `dotnet test tests/Platforms/Ghost.Platform.Common.Tests/` |
| 3 | `fix(glassdoor): implement robust CSRF and GraphQL handling` | `src/Platforms/Ghost.Platform.Glassdoor/` | `dotnet test tests/Platforms/Ghost.Platform.Glassdoor.Tests/` |
| 4 | `fix(indeed): implement proper pagination and API key security` | `src/Platforms/Ghost.Platform.Indeed/` | `dotnet test tests/Platforms/Ghost.Platform.Indeed.Tests/` |
| 5 | `fix(google-jobs): implement robust JSON extraction and consent handling` | `src/Platforms/Ghost.Platform.Google/Jobs/` | `dotnet test tests/Platforms/Ghost.Platform.Google.Tests/` |
| 6 | `feat(infojobs): add Spanish job platform support` | `src/Platforms/Ghost.Platform.InfoJobs/` | `dotnet test tests/Platforms/Ghost.Platform.InfoJobs.Tests/` |
| 7 | `feat(tecnoempleo): add Spanish tech job platform support` | `src/Platforms/Ghost.Platform.Tecnoempleo/` | `dotnet test tests/Platforms/Ghost.Platform.Tecnoempleo.Tests/` |
| 8 | `feat(monitoring): add platform performance monitoring` | `src/Platforms/Ghost.Platform.Common/Monitoring/` | `dotnet test tests/Performance/Ghost.Platform.Performance.Tests/` |

---

## Success Criteria

### Verification Commands
```bash
dotnet test tests/Platforms/  # Expected: All platform tests pass
curl -s http://localhost:8080/api/jobs/search?platform=glassdoor  # Expected: JSON with job listings
```

### Final Checklist
- [ ] All "Must Have" present (error handling, proxy rotation, tests, EU support)
- [ ] All "Must NOT Have" absent (no hard-coded keys, no brittle parsing)
- [ ] All tests pass (>80% coverage)
- [ ] Performance metrics show <5 second response times
- [ ] European platform support validated with Spanish job data