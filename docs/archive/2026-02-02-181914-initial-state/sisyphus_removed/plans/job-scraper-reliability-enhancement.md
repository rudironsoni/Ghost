# Job Scraper Reliability Enhancement - Work Plan

## TL;DR

**Objective**: Fix broken job scrapers for Indeed, Glassdoor, and Google Jobs to achieve near 100% reliability

**Deliverables**:
- Unified session management using RotatingProxySession across all platforms
- Advanced anti-bot evasion with residential proxies and browser fingerprinting
- Robust parsing engines with fallback strategies
- Intelligent error handling with consent/CAPTCHA management
- Comprehensive monitoring and observability

**Estimated Effort**: Large (5-6 weeks)
**Parallel Execution**: YES - Platform teams can work in parallel
**Critical Path**: Shared infrastructure → Platform-specific implementations → Integration testing

---

## Context

### Original Request
Deep investigation into broken job scrapers that return zero, null, or empty results. Need root cause analysis and solutions for:
- Indeed (@src/Platforms/Ghost.Platform.Indeed/)
- Glassdoor (@src/Platforms/Ghost.Platform.Glassdoor/)
- Google Jobs (@src/Platforms/Ghost.Platform.Google/Jobs/)

### Key Findings from Analysis

**Root Causes Identified**:
1. **Indeed**: Per-request HttpClient creation breaks session continuity; static headers easily fingerprinted; rigid GraphQL parsing
2. **Glassdoor**: Hardcoded fallback CSRF token may be invalid; duplicate CSRF extraction logic; limited consent handling
3. **Google Jobs**: Manual cookie header injection; unused CookieContainer; inconsistent UA/Sec-CH headers; brittle parser with fixed index positions

**Common Issues Across All Platforms**:
- Inadequate anti-bot protections (no residential proxy rotation)
- Poor session management (no connection pooling)
- Brittle parsing logic (no fallback strategies)
- Insufficient error handling (hardcoded tokens, silent failures)

### Architecture Decisions
- Use existing RotatingProxySession infrastructure (currently unused by scrapers)
- Implement multi-strategy parsing with fallback
- Add residential proxy integration for production reliability
- Maintain backward compatibility with existing options

---

## Work Objectives

### Core Objective
Transform unreliable job scrapers into production-grade, near-100% reliable data collection systems through systematic improvements to anti-bot evasion, session management, and error recovery.

### Concrete Deliverables
1. **Unified Session Management Framework**
   - All platforms use RotatingProxySession
   - Persistent CookieContainer handling
   - Connection pooling and reuse

2. **Advanced Anti-Bot Protection**
   - Residential proxy rotation
   - User-Agent rotation with header consistency
   - Browser fingerprinting evasion
   - CAPTCHA detection and handling

3. **Robust Parsing Engine**
   - Multi-strategy parsing (structured → DOM → heuristic → regex)
   - Schema evolution handling
   - Structured logging of parsing decisions

4. **Intelligent Error Recovery**
   - Advanced consent page handling
   - Circuit breaker patterns
   - Browser escalation paths
   - Third-party API fallbacks

5. **Monitoring & Observability**
   - Metrics collection (success rate, error categories)
   - Health checks and alerting
   - Debug logging with request/response capture

### Definition of Done
- [ ] All three platforms achieve >95% success rate in integration tests
- [ ] <1% error rate from anti-bot detection (blocking, rate limiting)
- [ ] Consent page handling succeeds >90% of the time
- [ ] All scrapers use unified RotatingProxySession infrastructure
- [ ] Comprehensive test coverage for new features
- [ ] Monitoring dashboard operational

### Must Have
- Residential proxy integration (Bright Data, Oxylabs, or equivalent)
- Proper cookie handling with CookieContainer
- User-Agent rotation pools for each platform
- Multi-strategy parsers with fallback
- Consent page detection and handling
- Structured error reporting

### Must NOT Have (Guardrails)
- No breaking changes to existing public APIs
- No hardcoded tokens or credentials in code
- No reliance on single proxy provider
- No exceptions that crash the application
- No excessive resource consumption (memory leaks)

---

## Verification Strategy

### Test Decision
- **Infrastructure exists**: YES (test projects exist for all platforms)
- **User wants tests**: TDD (Write tests first, then implementation)
- **Framework**: xUnit with Moq for mocking
- **New tests required**: YES (for proxy integration, consent handling, multi-strategy parsers)

### TDD Structure
Each TODO follows RED-GREEN-REFACTOR:
1. **RED**: Write failing test
2. **GREEN**: Implement minimum code to pass
3. **REFACTOR**: Clean up while keeping tests green

### Test Categories
1. **Unit Tests**: Individual components (parsers, retry logic)
2. **Integration Tests**: Full scraping workflows
3. **Anti-Bot Tests**: Proxy rotation, header consistency
4. **Error Recovery Tests**: Consent handling, fallback strategies

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation - Week 1-2):
├── Task 1: Integrate RotatingProxySession into Indeed
├── Task 2: Integrate RotatingProxySession into Glassdoor
├── Task 3: Integrate RotatingProxySession into Google Jobs
└── Task 4: Implement User-Agent rotation system

Wave 2 (Anti-Bot - Week 2-3):
├── Task 5: Add residential proxy integration
├── Task 6: Implement browser fingerprinting evasion
├── Task 7: Enhance consent page handling
└── Task 8: Add CAPTCHA detection

Wave 3 (Parsing - Week 3-4):
├── Task 9: Implement multi-strategy parsers for Indeed
├── Task 10: Implement multi-strategy parsers for Glassdoor
├── Task 11: Implement multi-strategy parsers for Google Jobs
└── Task 12: Add structured logging

Wave 4 (Integration - Week 4-5):
├── Task 13: Implement session token synchronization
├── Task 14: Add third-party API fallbacks
├── Task 15: Implement circuit breaker patterns
└── Task 16: Add monitoring and alerting

Wave 5 (Testing & Deployment - Week 5-6):
├── Task 17: Comprehensive integration testing
├── Task 18: Performance benchmarking
├── Task 19: Documentation and runbooks
└── Task 20: Production deployment with canary

Critical Path: Task 4 → Task 5 → Task 13 → Task 17 → Task 20
Parallel Speedup: ~40% faster than sequential
```

### Dependency Matrix

| Task | Depends On | Blocks | Can Parallelize With |
|------|------------|--------|---------------------|
| 1-3 (Platform Sessions) | None | 9-11 (Parsers) | Each other (platform-specific) |
| 4 (UA Rotation) | None | 5, 6, 13 | 1-3 |
| 5 (Proxy Integration) | 4 | 13, 14 | 6, 7, 8 |
| 9-11 (Parsers) | 1-3 | 17 | Each other |
| 13 (Token Sync) | 1-3, 5 | 17 | 9-12 |
| 17 (Integration Tests) | 9-13 | 20 | None |
| 20 (Deployment) | 17 | None | None |

---

## TODOs

### Phase 1: Foundation (Week 1-2)

#### Task 1: Integrate RotatingProxySession into Indeed

**What to do**:
- [ ] Modify `IndeedApiClient` constructor to accept `RotatingProxySession` or `SessionFactory` via DI
- [ ] Replace per-request HttpClient creation with `session.ExecuteAsync(() => request)`
- [ ] Remove manual cookie header injection, use CookieContainer
- [ ] Wire `IndeedOptions.DelayMinMs`/`DelayMaxMs` into rate limiting
- [ ] Add User-Agent rotation from pool

**Must NOT do**:
- Do NOT remove existing constructor (maintain backward compatibility)
- Do NOT change public API surface of `IndeedJobClient`
- Do NOT hardcode proxy credentials

**Recommended Agent Profile**:
- **Category**: `unspecified-high` (requires careful refactoring)
- **Skills**: `git-master`, `frontend-ui-ux` (for testing)
- **Reason**: Need to modify existing HTTP client patterns while maintaining compatibility

**Parallelization**:
- **Can Run In Parallel**: YES
- **Parallel Group**: Wave 1
- **Blocks**: Task 9 (Indeed Parser)
- **Blocked By**: None

**References**:
- Pattern: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySession.cs:15-45` - Session management with proxy rotation
- Pattern: `/src/Platforms/Ghost.Platform.Common/Session/SessionFactory.cs:20-50` - Platform-specific session creation
- API: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs:76-142` - Current HttpClient creation
- Options: `/src/Platforms/Ghost.Platform.Indeed/IndeedOptions.cs:30-50` - Delay settings (currently unused)

**Acceptance Criteria**:
- [ ] `IndeedApiClient` can be constructed with `RotatingProxySession`
- [ ] All HTTP requests go through session (verified via mock)
- [ ] Cookie persistence works across pagination (verified in test)
- [ ] Delay settings from options are applied with jitter
- [ ] Existing tests pass without modification

**Test Plan**:
```csharp
// Test: RotatingProxySession integration
[Fact]
public async Task SearchAsync_UsesRotatingProxySession()
{
    var mockSession = new Mock<IRotatingProxySession>();
    var client = new IndeedApiClient(mockSession.Object, options, logger);
    await client.SearchAsync("developer", "NYC", 10);
    mockSession.Verify(s => s.ExecuteAsync(It.IsAny<Func<HttpRequestMessage>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
}
```

**Commit**: YES - Individual commit after each platform
- Message: `feat(indeed): integrate RotatingProxySession for session management`
- Files: `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs`
- Pre-commit: `dotnet test tests/Ghost.Platform.Indeed.Tests`

---

#### Task 2: Integrate RotatingProxySession into Glassdoor

**What to do**:
- [ ] Modify `GlassdoorApiClient` to use `RotatingProxySession`
- [ ] Update `GlassdoorBrowserClient` to use session factory for proxy selection
- [ ] Unify CSRF token extraction between API and browser clients
- [ ] Remove hardcoded fallback token usage
- [ ] Add proper cookie persistence

**Must NOT do**:
- Do NOT delete the fallback token constant yet (mark as obsolete)
- Do NOT change `GlassdoorJobClient` public API
- Do NOT break existing browser fallback logic

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`, `playwright`
- **Reason**: Need to coordinate changes between API and browser clients

**Parallelization**:
- **Can Run In Parallel**: YES (with Task 1)
- **Parallel Group**: Wave 1
- **Blocks**: Task 10 (Glassdoor Parser)
- **Blocked By**: None

**References**:
- Shared: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySession.cs` - Session management
- API Client: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs:60-95` - CSRF extraction
- Browser Client: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorBrowserClient.cs:194-206` - Session creation
- Constants: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorConstants.cs:113` - Fallback token

**Acceptance Criteria**:
- [ ] `GlassdoorApiClient` uses RotatingProxySession
- [ ] CSRF token extraction unified (single source of truth)
- [ ] Hardcoded fallback token marked obsolete with warning
- [ ] Cookie container persists across API and browser
- [ ] Existing tests pass

**Commit**:
- Message: `feat(glassdoor): integrate RotatingProxySession and unify CSRF extraction`
- Files: `src/Platforms/Ghost.Platform.Glassdoor/Internal/*.cs`

---

#### Task 3: Integrate RotatingProxySession into Google Jobs

**What to do**:
- [ ] Modify `GoogleJobsApiClient` to accept RotatingProxySession
- [ ] Remove manual Cookie header injection
- [ ] Ensure User-Agent consistency with Sec-CH-UA headers
- [ ] Add browser escalation path when consent detected
- [ ] Fix async pagination to use session

**Must NOT do**:
- Do NOT remove async pagination logic
- Do NOT change default strategy (BrowserFirst)
- Do NOT break existing consent detection

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`, `playwright`
- **Reason**: Complex coordination between API and browser clients

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 1-2)
- **Parallel Group**: Wave 1
- **Blocks**: Task 11 (Google Parser)
- **Blocked By**: None

**References**:
- API Client: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs:16` - Unused CookieContainer
- Cookie Issue: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs:111-112` - Manual cookie header
- Headers: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsConstants.cs:48-77` - Search headers with static UA
- Browser: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsBrowserClient.cs:145-159` - Consent handling

**Acceptance Criteria**:
- [ ] CookieContainer used instead of manual headers
- [ ] User-Agent rotation updates all Sec-CH headers
- [ ] Browser escalation works when consent detected
- [ ] Async pagination uses session
- [ ] Existing tests pass

**Commit**:
- Message: `feat(google): integrate RotatingProxySession and fix cookie handling`
- Files: `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs`

---

#### Task 4: Implement User-Agent Rotation System

**What to do**:
- [ ] Create `UserAgentRotator` class with platform-specific pools
- [ ] Generate consistent Sec-CH-UA headers for each UA
- [ ] Integrate into RotatingProxySessionOptions
- [ ] Add configuration options for UA rotation
- [ ] Update all constants files to use rotator

**Must NOT do**:
- Do NOT use random UAs without platform matching
- Do NOT ignore mobile vs desktop UAs
- Do NOT hardcode UA pools in each platform

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Standalone component that can be developed independently

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 1-3)
- **Parallel Group**: Wave 1
- **Blocks**: Task 5 (Proxy Integration), Task 6 (Fingerprinting)
- **Blocked By**: None

**References**:
- Constants: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsConstants.cs:13-35` - UA pool example
- Options: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySessionOptions.cs` - Session options
- Headers: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedConstants.cs:88` - Static UA usage

**Acceptance Criteria**:
- [ ] UA rotator provides platform-specific pools
- [ ] Sec-CH-UA headers match selected UA
- [ ] Rotation is deterministic per session
- [ ] All platforms use rotator instead of static UAs

**Commit**:
- Message: `feat(common): implement UserAgentRotator with platform-specific pools`
- Files: `src/Platforms/Ghost.Platform.Common/Session/UserAgentRotator.cs`

---

### Phase 2: Anti-Bot (Week 2-3)

#### Task 5: Add Residential Proxy Integration

**What to do**:
- [ ] Implement `IProxyProvider` for Bright Data API
- [ ] Add proxy health checking and rotation
- [ ] Configure proxy fallback chain (primary → backup → direct)
- [ ] Add geographic targeting support
- [ ] Integrate with RotatingProxySession.RefreshProxyPoolAsync

**Must NOT do**:
- Do NOT use datacenter proxies as primary (easily blocked)
- Do NOT hardcode proxy credentials in config
- Do NOT rotate proxies mid-session

**Recommended Agent Profile**:
- **Category**: `ultrabrain` (complex integration)
- **Skills**: `git-master`
- **Reason**: Requires understanding of proxy APIs and rotation strategies

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 6-8)
- **Parallel Group**: Wave 2
- **Blocks**: Task 13 (Token Sync), Task 17 (Integration Tests)
- **Blocked By**: Task 4

**References**:
- Interface: `/src/Platforms/Ghost.Platform.Common/Abstractions/IProxyProvider.cs`
- Session: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySession.cs:139-171` - Proxy refresh
- Options: `/src/Platforms/Ghost.Platform.Common/Session/RotatingProxySessionOptions.cs` - Proxy options

**Acceptance Criteria**:
- [ ] Bright Data integration working (or mock for testing)
- [ ] Proxy health checks implemented
- [ ] Fallback chain works when primary fails
- [ ] Geographic targeting configurable
- [ ] Pool refreshes maintain healthy proxies

**Test Plan**:
```csharp
[Fact]
public async Task ProxyRotation_UsesHealthyProxiesOnly()
{
    var provider = new BrightDataProxyProvider(config);
    var session = new RotatingProxySession(provider, options);
    var response = await session.ExecuteAsync(() => request);
    Assert.True(response.IsSuccessStatusCode);
}
```

**Commit**:
- Message: `feat(common): add residential proxy integration with health checks`
- Files: `src/Platforms/Ghost.Platform.Common/Proxies/`

---

#### Task 6: Implement Browser Fingerprinting Evasion

**What to do**:
- [ ] Add Playwright stealth plugins
- [ ] Implement canvas fingerprint randomization
- [ ] Add WebGL fingerprint spoofing
- [ ] Configure timezone and locale spoofing
- [ ] Add viewport and screen size rotation

**Must NOT do**:
- Do NOT use headless=true without stealth (easily detected)
- Do NOT ignore plugin evasion
- Do NOT use predictable random values

**Recommended Agent Profile**:
- **Category**: `artistry` (creative solutions needed)
- **Skills**: `playwright`, `git-master`
- **Reason**: Requires creative bypass techniques

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5, 7-8)
- **Parallel Group**: Wave 2
- **Blocks**: None (enhancement)
- **Blocked By**: Task 4

**References**:
- Browser Client: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsBrowserClient.cs:68-83` - Cookie injection
- Stealth: Research findings on Playwright stealth plugins
- Options: Extend `SessionOptions` with fingerprinting flags

**Acceptance Criteria**:
- [ ] Stealth plugins integrated with GhostKernel
- [ ] Canvas fingerprint randomized per session
- [ ] WebGL spoofed to match real browsers
- [ ] Timezone/locale match proxy location
- [ ] Viewport rotation implemented

**Commit**:
- Message: `feat(common): implement browser fingerprinting evasion`
- Files: `src/Platforms/Ghost.Platform.Common/Session/BrowserFingerprinting.cs`

---

#### Task 7: Enhance Consent Page Handling

**What to do**:
- [ ] Add iframe detection and handling for consent dialogs
- [ ] Implement multi-step consent flows (Customize → Confirm)
- [ ] Add region-specific consent handling (GDPR, CCPA)
- [ ] Implement cookie banner interaction strategies
- [ ] Add consent state persistence

**Must NOT do**:
- Do NOT rely on single button click strategy
- Do NOT ignore shadow DOM consent elements
- Do NOT fail silently on consent errors

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `playwright`, `git-master`
- **Reason**: Complex DOM interactions

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5-6, 8)
- **Parallel Group**: Wave 2
- **Blocks**: Task 17 (Integration Tests)
- **Blocked By**: None

**References**:
- Google Consent: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsBrowserClient.cs:184-210` - Consent detection
- Glassdoor Consent: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorBrowserClient.cs:62-94` - HandleConsentPageAsync
- Strategies: Research findings on consent flow handling

**Acceptance Criteria**:
- [ ] iframe consent dialogs handled
- [ ] Multi-step flows work (customize → confirm)
- [ ] Region-specific handling (EU, US, etc.)
- [ ] Consent state cached and reused
- [ ] Structured logging of consent actions

**Commit**:
- Message: `feat(common): enhance consent page handling with iframe support`
- Files: `src/Platforms/Ghost.Platform.Common/Browser/ConsentHandler.cs`

---

#### Task 8: Add CAPTCHA Detection and Handling

**What to do**:
- [ ] Implement CAPTCHA detection (reCAPTCHA, hCaptcha)
- [ ] Add CAPTCHA solving service integration (2Captcha, Anti-CAPTCHA)
- [ ] Implement human-in-the-loop queue for unsolvable CAPTCHAs
- [ ] Add CAPTCHA avoidance strategies (reduce detection triggers)
- [ ] Implement CAPTCHA occurrence metrics

**Must NOT do**:
- Do NOT attempt to solve CAPTCHAs programmatically without service
- Do NOT ignore legal/ethical implications
- Do NOT proceed without user notification on CAPTCHA

**Recommended Agent Profile**:
- **Category**: `ultrabrain`
- **Skills**: `playwright`, `git-master`
- **Reason**: Complex integration with external services

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 5-7)
- **Parallel Group**: Wave 2
- **Blocks**: Task 14 (Third-party fallbacks)
- **Blocked By**: None

**References**:
- Detection: Research findings on CAPTCHA detection patterns
- Services: 2Captcha API documentation, Anti-CAPTCHA documentation
- Error: Add structured error type for CAPTCHA_REQUIRED

**Acceptance Criteria**:
- [ ] CAPTCHA detection working (95%+ accuracy)
- [ ] 2Captcha integration (configurable)
- [ ] Human-in-the-loop queue implemented
- [ ] Metrics collection operational
- [ ] Legal compliance verified

**Commit**:
- Message: `feat(common): add CAPTCHA detection and solving integration`
- Files: `src/Platforms/Ghost.Platform.Common/AntiBot/CaptchaHandler.cs`

---

### Phase 3: Parsing (Week 3-4)

#### Task 9: Implement Multi-Strategy Parser for Indeed

**What to do**:
- [ ] Refactor IndeedJobParser to support multiple strategies
- [ ] Add fallback property path lookups
- [ ] Implement schema evolution handling
- [ ] Add structured logging for parsing decisions
- [ ] Capture raw JSON samples on parse failures

**Must NOT do**:
- Do NOT break existing parser behavior (backward compatible)
- Do NOT use reflection for property access (performance)
- Do NOT silently skip parse errors

**Recommended Agent Profile**:
- **Category**: `unspecified-low`
- **Skills**: `git-master`
- **Reason**: Refactoring existing parser

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 10-11)
- **Parallel Group**: Wave 3
- **Blocks**: Task 17 (Integration Tests)
- **Blocked By**: Task 1

**References**:
- Current Parser: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedJobParser.cs`
- Tests: `/tests/Ghost.Platform.Indeed.Tests/IndeedJobParserTests.cs`
- JSON: Sample Indeed responses from logs

**Acceptance Criteria**:
- [ ] Multiple parsing strategies implemented
- [ ] Fallback paths work for changed schemas
- [ ] Structured logging captures parsing path
- [ ] Raw samples captured on failures
- [ ] All existing tests pass

**Commit**:
- Message: `feat(indeed): implement multi-strategy parser with fallback`
- Files: `src/Platforms/Ghost.Platform.Indeed/Internal/IndeedJobParser.cs`

---

#### Task 10: Implement Multi-Strategy Parser for Glassdoor

**What to do**:
- [ ] Refactor GlassdoorJobParser with multiple strategies
- [ ] Add recursive JSON scanning with fallback paths
- [ ] Implement heuristic job array detection
- [ ] Add structured logging
- [ ] Capture response samples

**Must NOT do**:
- Do NOT rely solely on property name heuristics
- Do NOT silently discard parse exceptions
- Do NOT break existing tests

**Recommended Agent Profile**:
- **Category**: `unspecified-low`
- **Skills**: `git-master`
- **Reason**: Similar to Task 9

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 9, 11)
- **Parallel Group**: Wave 3
- **Blocks**: Task 17
- **Blocked By**: Task 2

**References**:
- Parser: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorJobParser.cs`
- Tests: `/tests/Platforms/Ghost.Platform.Glassdoor.Tests/GlassdoorJobParserTests.cs`

**Acceptance Criteria**:
- [ ] Multi-strategy parser implemented
- [ ] Heuristic detection works for job arrays
- [ ] Structured logging added
- [ ] Samples captured on failures

**Commit**:
- Message: `feat(glassdoor): implement multi-strategy parser with fallback`
- Files: `src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorJobParser.cs`

---

#### Task 11: Implement Multi-Strategy Parser for Google Jobs

**What to do**:
- [ ] Refactor GoogleJobsParser with DOM-based primary strategy
- [ ] Add AngleSharp integration for HTML parsing
- [ ] Keep legacy strategies as fallbacks
- [ ] Add structured logging
- [ ] Remove fixed index positions (brittle)

**Must NOT do**:
- Do NOT remove widget key parsing (keep as fallback)
- Do NOT break existing parser tests
- Do NOT use regex for primary parsing

**Recommended Agent Profile**:
- **Category**: `unspecified-low`
- **Skills**: `git-master`
- **Reason**: DOM parsing requires HTML parser integration

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 9-10)
- **Parallel Group**: Wave 3
- **Blocks**: Task 17
- **Blocked By**: Task 3

**References**:
- Parser: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsParser.cs`
- AngleSharp: NuGet package for HTML parsing
- Tests: `/tests/Ghost.Platform.Google.Tests/GoogleJobsParserTests.cs`

**Acceptance Criteria**:
- [ ] AngleSharp-based parsing implemented
- [ ] Legacy strategies preserved as fallbacks
- [ ] Fixed index positions removed
- [ ] Structured logging added
- [ ] All parser tests pass

**Commit**:
- Message: `feat(google): implement DOM-based multi-strategy parser`
- Files: `src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsParser.cs`

---

#### Task 12: Add Structured Logging

**What to do**:
- [ ] Create structured logging middleware
- [ ] Add logging for all anti-bot events (consent, CAPTCHA, blocks)
- [ ] Log parsing strategy selection and results
- [ ] Add request/response logging (sanitized)
- [ ] Implement log correlation IDs

**Must NOT do**:
- Do NOT log sensitive data (API keys, credentials)
- Do NOT log PII from job listings
- Do NOT use synchronous logging (performance)

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Standalone logging component

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 9-11)
- **Parallel Group**: Wave 3
- **Blocks**: Task 17
- **Blocked By**: None

**References**:
- Logging: Microsoft.Extensions.Logging
- Sanitization: Extension methods for sensitive data

**Acceptance Criteria**:
- [ ] Structured logging middleware implemented
- [ ] Anti-bot events logged
- [ ] Parsing decisions logged
- [ ] Sensitive data sanitized
- [ ] Correlation IDs working

**Commit**:
- Message: `feat(common): add structured logging for scraping operations`
- Files: `src/Platforms/Ghost.Platform.Common/Logging/ScraperLogger.cs`

---

### Phase 4: Integration (Week 4-5)

#### Task 13: Implement Session Token Synchronization

**What to do**:
- [ ] Create token extraction service for browser sessions
- [ ] Implement cookie synchronization between browser and HTTP clients
- [ ] Add token validation and refresh logic
- [ ] Create session state store
- [ ] Integrate with all three platforms

**Must NOT do**:
- Do NOT store tokens indefinitely (security)
- Do NOT share sessions between different proxy locations
- Do NOT ignore token expiration

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `playwright`, `git-master`
- **Reason**: Complex coordination between browser and HTTP

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 14-16)
- **Parallel Group**: Wave 4
- **Blocks**: Task 17
- **Blocked By**: Tasks 1-3, 5

**References**:
- Token Extraction: Glassdoor CSRF extraction logic
- Browser: Playwright cookie APIs
- Storage: In-memory with TTL

**Acceptance Criteria**:
- [ ] Token extraction from browser works
- [ ] Cookie sync between browser/HTTP works
- [ ] Token refresh on expiration
- [ ] Session state store operational
- [ ] All platforms use sync

**Commit**:
- Message: `feat(common): implement session token synchronization`
- Files: `src/Platforms/Ghost.Platform.Common/Session/TokenSynchronizer.cs`

---

#### Task 14: Add Third-Party API Fallbacks

**What to do**:
- [ ] Implement SerpApi integration for Google Jobs
- [ ] Add Apify actor integration for all platforms
- [ ] Create fallback orchestration logic
- [ ] Add cost tracking for API usage
- [ ] Implement fallback decision logic

**Must NOT do**:
- Do NOT make third-party APIs mandatory
- Do NOT proceed without user consent for external services
- Do NOT ignore rate limits of third-party APIs

**Recommended Agent Profile**:
- **Category**: `ultrabrain`
- **Skills**: `git-master`
- **Reason**: External API integration

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 13, 15-16)
- **Parallel Group**: Wave 4
- **Blocks**: Task 17
- **Blocked By**: Task 8 (CAPTCHA - related to fallback triggers)

**References**:
- SerpApi: Official documentation
- Apify: Actor marketplace
- Cost: Usage tracking

**Acceptance Criteria**:
- [ ] SerpApi integration working
- [ ] Apify integration working
- [ ] Fallback orchestration implemented
- [ ] Cost tracking operational
- [ ] Decision logic tested

**Commit**:
- Message: `feat(common): add third-party API fallback providers`
- Files: `src/Platforms/Ghost.Platform.Common/Fallbacks/`

---

#### Task 15: Implement Circuit Breaker Patterns

**What to do**:
- [ ] Add Polly circuit breaker policies
- [ ] Implement half-open state handling
- [ ] Add circuit breaker metrics
- [ ] Configure platform-specific thresholds
- [ ] Add circuit breaker dashboards

**Must NOT do**:
- Do NOT use circuit breakers for transient errors
- Do NOT ignore half-open state in testing
- Do NOT hardcode thresholds

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Polly library integration

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 13-14, 16)
- **Parallel Group**: Wave 4
- **Blocks**: Task 17
- **Blocked By**: None

**References**:
- Polly: Circuit breaker documentation
- Metrics: Prometheus/Grafana

**Acceptance Criteria**:
- [ ] Circuit breakers implemented
- [ ] Half-open state works
- [ ] Metrics collection operational
- [ ] Thresholds configurable
- [ ] Dashboards created

**Commit**:
- Message: `feat(common): implement circuit breaker patterns for resilience`
- Files: `src/Platforms/Ghost.Platform.Common/Resilience/CircuitBreakers.cs`

---

#### Task 16: Add Monitoring and Alerting

**What to do**:
- [ ] Implement metrics collection (success rate, errors, latency)
- [ ] Create health check endpoints
- [ ] Add alerting rules for critical thresholds
- [ ] Create monitoring dashboards
- [ ] Add log aggregation

**Must NOT do**:
- Do NOT collect PII in metrics
- Do NOT ignore metric cardinality (performance)
- Do NOT use blocking metric reporting

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Monitoring infrastructure

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 13-15)
- **Parallel Group**: Wave 4
- **Blocks**: Task 20 (Deployment monitoring)
- **Blocked By**: Task 12 (Structured logging)

**References**:
- Metrics: Prometheus client library
- Health: ASP.NET Core health checks
- Alerting: AlertManager integration

**Acceptance Criteria**:
- [ ] Metrics collection implemented
- [ ] Health checks operational
- [ ] Alerting rules configured
- [ ] Dashboards created
- [ ] Log aggregation working

**Commit**:
- Message: `feat(common): add monitoring and alerting infrastructure`
- Files: `src/Platforms/Ghost.Platform.Common/Monitoring/`

---

### Phase 5: Testing & Deployment (Week 5-6)

#### Task 17: Comprehensive Integration Testing

**What to do**:
- [ ] Write integration tests for all platforms
- [ ] Test proxy rotation scenarios
- [ ] Test consent page handling
- [ ] Test CAPTCHA detection
- [ ] Test fallback strategies
- [ ] Test error recovery
- [ ] Test parsing strategies
- [ ] Benchmark performance

**Must NOT do**:
- Do NOT skip mocking external services
- Do NOT test against production without consent
- Do NOT ignore test flakiness

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`, `playwright`
- **Reason**: Comprehensive testing required

**Parallelization**:
- **Can Run In Parallel**: NO (sequential testing)
- **Parallel Group**: Wave 5
- **Blocks**: Task 20
- **Blocked By**: Tasks 9-16

**References**:
- Tests: All existing test projects
- Mocking: Moq, WireMock
- Benchmarking: BenchmarkDotNet

**Acceptance Criteria**:
- [ ] >90% code coverage
- [ ] All integration tests pass
- [ ] Proxy rotation tested
- [ ] Consent handling tested
- [ ] Fallbacks tested
- [ ] Performance benchmarks recorded

**Commit**:
- Message: `test(all): add comprehensive integration tests`
- Files: `tests/**/*Tests.cs`

---

#### Task 18: Performance Benchmarking

**What to do**:
- [ ] Benchmark request latency
- [ ] Benchmark parsing performance
- [ ] Benchmark proxy rotation overhead
- [ ] Compare before/after metrics
- [ ] Document performance characteristics

**Must NOT do**:
- Do NOT benchmark in production
- Do NOT ignore cold start times
- Do NOT use unrealistic test data

**Recommended Agent Profile**:
- **Category**: `quick`
- **Skills**: `git-master`
- **Reason**: Benchmarking

**Parallelization**:
- **Can Run In Parallel**: YES (with Task 17)
- **Parallel Group**: Wave 5
- **Blocks**: None
- **Blocked By**: None

**Acceptance Criteria**:
- [ ] Latency benchmarks recorded
- [ ] Parsing performance measured
- [ ] Overhead quantified
- [ ] Comparison documented
- [ ] Performance goals met

**Commit**:
- Message: `perf(all): add performance benchmarks and documentation`
- Files: `benchmarks/`

---

#### Task 19: Documentation and Runbooks

**What to do**:
- [ ] Write architecture documentation
- [ ] Create troubleshooting runbooks
- [ ] Document configuration options
- [ ] Add deployment guides
- [ ] Create incident response procedures

**Must NOT do**:
- Do NOT skip security considerations
- Do NOT ignore legal/ethical guidelines
- Do NOT use outdated screenshots

**Recommended Agent Profile**:
- **Category**: `writing`
- **Skills**: `git-master`
- **Reason**: Documentation

**Parallelization**:
- **Can Run In Parallel**: YES (with Tasks 17-18)
- **Parallel Group**: Wave 5
- **Blocks**: Task 20
- **Blocked By**: None

**Acceptance Criteria**:
- [ ] Architecture docs complete
- [ ] Runbooks created
- [ ] Configuration documented
- [ ] Deployment guides ready
- [ ] Incident procedures defined

**Commit**:
- Message: `docs(all): add comprehensive documentation and runbooks`
- Files: `docs/`

---

#### Task 20: Production Deployment with Canary

**What to do**:
- [ ] Create canary deployment configuration
- [ ] Set up feature flags
- [ ] Configure rollback procedures
- [ ] Deploy to production
- [ ] Monitor metrics during rollout
- [ ] Gradually increase traffic
- [ ] Validate success metrics

**Must NOT do**:
- Do NOT deploy without rollback plan
- Do NOT skip monitoring during rollout
- Do NOT ignore error rate spikes

**Recommended Agent Profile**:
- **Category**: `unspecified-high`
- **Skills**: `git-master`, `playwright`
- **Reason**: Production deployment

**Parallelization**:
- **Can Run In Parallel**: NO
- **Parallel Group**: Wave 5
- **Blocks**: None (final task)
- **Blocked By**: Tasks 17-19

**Acceptance Criteria**:
- [ ] Canary deployed successfully
- [ ] Feature flags operational
- [ ] Rollback tested
- [ ] Metrics validated
- [ ] Full deployment complete
- [ ] Success metrics met (>95% success rate)

**Commit**:
- Message: `deploy(all): production deployment with canary rollout`
- Files: `deploy/`

---

## Success Criteria

### Verification Commands

```bash
# Run all tests
dotnet test tests/Ghost.Platform.Indeed.Tests
dotnet test tests/Ghost.Platform.Glassdoor.Tests
dotnet test tests/Ghost.Platform.Google.Tests

# Run integration tests
dotnet test tests/Integration.Tests

# Check code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run benchmarks
dotnet run --project benchmarks/Ghost.Benchmarks

# Verify metrics endpoint
curl http://localhost:5000/metrics
```

### Final Checklist

- [ ] All TODOs complete
- [ ] All tests passing
- [ ] Code coverage >90%
- [ ] Documentation complete
- [ ] Production deployed
- [ ] Success rate >95% (measured over 7 days)
- [ ] Error rate <1%
- [ ] Consent handling >90% success
- [ ] Monitoring operational
- [ ] Runbooks tested

---

## Appendix: Implementation Details

### Platform-Specific Code Locations

**Indeed**:
- API Client: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedApiClient.cs`
- Parser: `/src/Platforms/Ghost.Platform.Indeed/Internal/IndeedJobParser.cs`
- Options: `/src/Platforms/Ghost.Platform.Indeed/IndeedOptions.cs`
- Tests: `/tests/Ghost.Platform.Indeed.Tests/`

**Glassdoor**:
- API Client: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorApiClient.cs`
- Browser Client: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorBrowserClient.cs`
- Parser: `/src/Platforms/Ghost.Platform.Glassdoor/Internal/GlassdoorJobParser.cs`
- Tests: `/tests/Platforms/Ghost.Platform.Glassdoor.Tests/`

**Google Jobs**:
- API Client: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsApiClient.cs`
- Browser Client: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsBrowserClient.cs`
- Parser: `/src/Platforms/Ghost.Platform.Google/Jobs/Internal/GoogleJobsParser.cs`
- Tests: `/tests/Ghost.Platform.Google.Tests/`

**Common**:
- Session: `/src/Platforms/Ghost.Platform.Common/Session/`
- Proxies: `/src/Platforms/Ghost.Platform.Common/Proxies/`
- Monitoring: `/src/Platforms/Ghost.Platform.Common/Monitoring/`

### Commercial Service Integration

**Proxy Providers** (choose one or more):
- Bright Data: `brd.superproxy.io:22225`
- Oxylabs: `pr.oxylabs.io:7777`
- SmartProxy: `gate.smartproxy.com:7000`

**CAPTCHA Solving**:
- 2Captcha API endpoint
- Anti-CAPTCHA API endpoint

**Third-Party APIs**:
- SerpApi for Google Jobs
- Apify actors for all platforms

### Configuration Schema

```json
{
  "JobScrapers": {
    "Indeed": {
      "Enabled": true,
      "ApiKey": "...",
      "Strategy": "HttpFirst",
      "ProxyEnabled": true,
      "DelayMinMs": 1000,
      "DelayMaxMs": 3000,
      "MaxRetries": 3
    },
    "Glassdoor": {
      "Enabled": true,
      "Strategy": "BrowserFirst",
      "ProxyEnabled": true,
      "DelayMinMs": 2000,
      "DelayMaxMs": 5000,
      "MaxRetries": 4
    },
    "GoogleJobs": {
      "Enabled": true,
      "Strategy": "BrowserFirst",
      "ProxyEnabled": true,
      "DelayMinMs": 500,
      "DelayMaxMs": 1500,
      "MaxRetries": 3
    },
    "Proxies": {
      "Provider": "BrightData",
      "Username": "...",
      "Password": "...",
      "RotationIntervalMinutes": 10
    },
    "Captcha": {
      "Provider": "2Captcha",
      "ApiKey": "...",
      "Enabled": true
    }
  }
}
```

---

## Notes

### Anti-Patterns Avoided

1. **No single point of failure**: Multiple proxy providers, fallback strategies
2. **No hardcoded secrets**: All credentials externalized to configuration
3. **No tight coupling**: Platforms independent, common infrastructure shared
4. **No silent failures**: Structured logging and error reporting throughout
5. **No breaking changes**: Backward compatible with existing APIs

### Performance Considerations

- Connection pooling via RotatingProxySession
- Async/await throughout
- Minimal allocations in hot paths
- Batch processing where possible
- Caching of session tokens

### Legal/Ethical Considerations

- Respect robots.txt
- Implement rate limiting
- Consider official APIs where available
- Document scraping activities
- Comply with platform ToS
- Handle consent appropriately

---

**Ready to execute**: Run `/start-work` to begin implementation